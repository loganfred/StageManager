using AsyncAwaitBestPractices;
using StageManager.Diagnostics;
using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using StageManager.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StageManager
{
	public class SceneManager
	{
		private readonly Desktop _desktop;
		private List<Scene> _scenes;
		private Scene? _current;
		private bool _suspend = false;
		private volatile bool _paused = false;
		private int _stopped = 0;
		private Guid? _reentrancyLockSceneId;

		public event EventHandler<SceneChangedEventArgs> SceneChanged;
		public event EventHandler<CurrentSceneSelectionChangedEventArgs> CurrentSceneSelectionChanged;

		private IWindowStrategy WindowStrategy { get; } = new NormalizeAndMinimizeWindowStrategy(); // new WindowNormalizeStrategy/OpacityWindowStrategy/ShowAndHideWindowStrategy

		public WindowsManager WindowsManager { get; }

		/// <summary>
		/// The running instance, for last-resort cleanup from the unhandled-exception handlers in <see cref="App"/>.
		/// </summary>
		internal static SceneManager? Active { get; private set; }

		public SceneManager(WindowsManager windowsManager)
		{
			WindowsManager = windowsManager ?? throw new ArgumentNullException(nameof(windowsManager));
			_desktop = new Desktop();
			Active = this;
			_desktop.HideIcons();
		}

		public async Task Start()
		{
			if (Thread.CurrentThread.ManagedThreadId != 1)
				throw new NotSupportedException("Start has to be called on the main thread, otherwise events won't be fired.");

			WindowsManager.WindowCreated += WindowsManager_WindowCreated;
			WindowsManager.WindowUpdated += WindowsManager_WindowUpdated;
			WindowsManager.WindowDestroyed += WindowsManager_WindowDestroyed;
			WindowsManager.UntrackedFocus += WindowsManager_UntrackedFocus;

			await WindowsManager.Start();
		}

		/// <summary>
		/// Stops tracking and puts the desktop back the way it was. Safe to call more than once and from any thread
		/// (it is also the last-resort cleanup of the unhandled-exception handlers).
		/// </summary>
		internal void Stop()
		{
			if (Interlocked.Exchange(ref _stopped, 1) == 1)
				return;

			try
			{
				WindowsManager.Stop();
			}
			catch (Exception ex)
			{
				Log.Error("Stopping the windows manager failed", ex);
			}

			RestoreDesktop();
			RecoveryJournal.End();
		}

		public bool IsPaused => _paused;

		/// <summary>
		/// Shows all windows and restores the desktop icons, then ignores window events until <see cref="Resume"/>.
		/// </summary>
		public void Pause()
		{
			if (_paused)
				return;

			_paused = true;
			RestoreDesktop();
			Log.Info("Paused");
		}

		/// <summary>
		/// Re-applies the stage that was current when <see cref="Pause"/> was called.
		/// </summary>
		public void Resume()
		{
			if (!_paused)
				return;

			_paused = false;
			Log.Info("Resumed");

			try
			{
				_suspend = true;
				ApplyStage(_current);
			}
			finally
			{
				_suspend = false;
			}
		}

		/// <summary>
		/// Explorer was restarted: the desktop window was recreated and re-read the icon setting from the registry.
		/// </summary>
		public void OnExplorerRestarted()
		{
			Log.Info("Explorer restarted");
			_desktop.ResetDesktopView();

			if (_paused || Volatile.Read(ref _stopped) == 1)
				return;

			if (_current is null)
				_desktop.ShowIcons();
			else
				_desktop.HideIcons();
		}

		private void RestoreDesktop()
		{
			foreach (var scene in _scenes?.ToArray() ?? Array.Empty<Scene>())
			{
				foreach (var w in scene.Windows.ToArray())
				{
					try
					{
						WindowStrategy.Show(w);
					}
					catch (Exception ex)
					{
						Log.Error($"Could not restore window {w.Handle}", ex);
					}
				}
			}

			try
			{
				_desktop.RestoreIcons();
			}
			catch (Exception ex)
			{
				Log.Error("Could not restore desktop icons", ex);
			}
		}

		private void WindowsManager_WindowUpdated(IWindow window, WindowUpdateType type)
		{
			if (_suspend || _paused)
				return;

			if (type == WindowUpdateType.Foreground)
				SwitchToSceneByWindow(window).SafeFireAndForget();
		}

		private void WindowsManager_UntrackedFocus(object? sender, IntPtr e)
		{
			if (_suspend || _paused)
				return;

			if (!_desktop.HasDesktopView)
				_desktop.TrySetDesktopView(e);

			if (_desktop.HasDesktopView && _desktop.DesktopViewHandle == e)
				SwitchTo(null).SafeFireAndForget();
		}

		private void WindowsManager_WindowDestroyed(IWindow window)
		{
			var scene = FindSceneForWindow(window);

			if (scene is not null)
			{
				scene.Remove(window);

				if (scene.Windows.Any())
				{
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Updated));
				}
				else
				{
					_scenes.Remove(scene);
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Removed));
				}
			}
		}

		public Scene FindSceneForWindow(IWindow window) => FindSceneForWindow(window.Handle);

		public Scene FindSceneForWindow(IntPtr handle) => _scenes?.FirstOrDefault(s => s.Windows.Any(w => w.Handle == handle));

		private Scene FindSceneForProcess(string processName) => _scenes.FirstOrDefault(s => string.Equals(s.Key, processName, StringComparison.OrdinalIgnoreCase));

		private async void WindowsManager_WindowCreated(IWindow window, bool firstCreate)
		{
			SwitchToSceneByNewWindow(window).SafeFireAndForget();
		}

		private async Task SwitchToSceneByWindow(IWindow window)
		{
			var scene = FindSceneForWindow(window);
			if (scene is null)
			{
				scene = new Scene(GetWindowGroupKey(window), window);
				_scenes.Add(scene);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Created));
			}

			await SwitchTo(scene);
		}

		private async Task SwitchToSceneByNewWindow(IWindow window)
		{
			var existentScene = FindSceneForProcess(GetWindowGroupKey(window));
			var scene = existentScene ?? new Scene(window.ProcessName, window);

			if (existentScene is null)
			{
				_scenes.Add(scene);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Created));
			}
			else
			{
				scene.Add(window);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Updated));
			}

			await SwitchTo(scene).ConfigureAwait(true);
		}

		/// <summary>
		/// Determines if a scene is switched back to shortly after it has been hidden.
		/// This can happen if an app activates one of it's windows after being hidde,
		/// like Microsoft Teams does if there's a small floating window for a current call.
		/// </summary>
		/// <param name="scene"></param>
		/// <returns></returns>
		private bool IsReentrancy(Scene? scene)
		{
			if (scene is null)
				return false;

			if (Guid.Equals(scene.Id, _reentrancyLockSceneId))
				return true;

			if (_current is object)
			{
				_reentrancyLockSceneId = _current.Id;

				Task.Run(async () =>
				{
					await Task.Delay(1000).ConfigureAwait(false);
					_reentrancyLockSceneId = null;
				}).SafeFireAndForget();
			}

			return false;
		}

		public async Task SwitchTo(Scene? scene)
		{
			if (_paused || object.Equals(scene, _current))
				return;

			if (IsReentrancy(scene))
				return;

			try
			{
				_suspend = true;

				var prior = _current;
				_current = scene;

				foreach (var s in _scenes)
					s.IsSelected = s.Equals(scene);

				ApplyStage(scene);

				CurrentSceneSelectionChanged?.Invoke(this, new CurrentSceneSelectionChangedEventArgs(prior, _current));
			}
			finally
			{
				_suspend = false;
			}
		}

		/// <summary>
		/// Shows the windows of <paramref name="scene"/>, hides all others, and shows the desktop icons only for the desktop view (null).
		/// </summary>
		private void ApplyStage(Scene? scene)
		{
			var otherWindows = GetSceneableWindows().Except(scene?.Windows ?? Array.Empty<IWindow>()).ToArray();

			if (scene is object)
			{
				foreach (var w in scene.Windows)
					WindowStrategy.Show(w);
			}

			foreach (var o in otherWindows)
				WindowStrategy.Hide(o);

			if (scene is null)
				_desktop.ShowIcons();
			else
				_desktop.HideIcons();
		}

		public Task MoveWindow(Scene sourceScene, IWindow window, Scene targetScene)
		{
			try
			{
				_suspend = true;

				if (sourceScene is null || sourceScene.Equals(targetScene))
					return Task.CompletedTask;

				sourceScene.Remove(window);
				targetScene.Add(window);

				SceneChanged?.Invoke(this, new SceneChangedEventArgs(sourceScene, window, ChangeType.Updated));
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(targetScene, window, ChangeType.Updated));

				if (!sourceScene.Windows.Any())
				{
					_scenes.Remove(sourceScene);
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(sourceScene, window, ChangeType.Removed));
				}

				if (targetScene.Equals(_current))
				{
					WindowStrategy.Show(window);
					window.Focus();
				}
				else
				{
					WindowStrategy.Hide(window);

					// reset window position after move so that the window is back at the starting position on the new scene
					if (window is WindowsWindow w && w.PopLastLocation() is IWindowLocation l)
						Win32.SetWindowPos(window.Handle, IntPtr.Zero, l.X, l.Y, 0, 0, Win32.SetWindowPosFlags.IgnoreResize);
				}

				return Task.CompletedTask;
			}
			finally
			{
				_suspend = false;
			}
		}

		public async Task MoveWindow(IntPtr handle, Scene targetScene)
		{
			var source = FindSceneForWindow(handle);

			if (source is null || source.Equals(targetScene))
				return;

			var window = source.Windows.First(w => w.Handle == handle);
			await MoveWindow(source, window, targetScene);
		}

		public async Task PopWindowFrom(Scene sourceScene)
		{
			if (sourceScene is null || _current is null || sourceScene.Equals(_current))
				return;

			var window = sourceScene.Windows.LastOrDefault();

			if (window is object)
				await MoveWindow(sourceScene, window, _current).ConfigureAwait(false);
		}

		private IEnumerable<IWindow> GetSceneableWindows() => WindowsManager?.Windows?.Where(w => !string.IsNullOrEmpty(w.ProcessFileName) && !string.IsNullOrEmpty(w.Title));

		public IEnumerable<Scene> GetScenes()
		{
			if (_scenes is null)
			{
				_scenes = GetSceneableWindows()
							.GroupBy(GetWindowGroupKey)
							.Select(group => new Scene(group.Key, group.ToArray()))
							.ToList();
			}

			return _scenes;
		}

		public IEnumerable<IWindow> GetCurrentWindows() => _current?.Windows ?? GetSceneableWindows();

		private string GetWindowGroupKey(IWindow window) => window.ProcessName;
	}
}
