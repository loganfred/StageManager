using AsyncAwaitBestPractices;
using Microsoft.Xaml.Behaviors.Core;
using SharpHook;
using SharpHook.Data;
using StageManager.Diagnostics;
using StageManager.Model;
using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Interop;
using StageManager.Native.Window;
using StageManager.Strategies;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StageManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const int TIMERINTERVAL_MILLISECONDS = 500;
		private const string APP_NAME = "StageManager";
		private const int PAUSE_HOTKEY_ID = 0x5347; // arbitrary, unique within this window
		private static readonly int WM_TASKBARCREATED = (int)Win32.RegisterWindowMessage("TaskbarCreated"); // broadcast when Explorer (re)starts
		private IntPtr _thisHandle;
		private TaskPoolGlobalHook _hook;
		private WindowMode _mode;
		private double _lastWidth;
		private double _sidebarWidthPx; // _lastWidth in physical pixels, which is what the mouse hook and GetWindowRect report
		private double _flyInEdgePx = Settings.Current.FlyInEdgeWidth;
		private readonly DispatcherTimer _overlapCheckTimer;
		private Point _mouse = new Point(0, 0);
		private SceneModel _removedCurrentScene;
		private SceneModel? _mouseDownScene;
		private WindowModel? _mouseDownWindow;

		private readonly bool EnableWindowDropToScene = Settings.Current.EnableWindowDropToScene;
		private readonly bool EnableWindowPullToScene = Settings.Current.EnableWindowPullToScene;

		public MainWindow()
		{
			InitializeComponent();

			DataContext = this;

			// on the UI thread, so it never races with the WinEvent callbacks that mutate scenes
			_overlapCheckTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(TIMERINTERVAL_MILLISECONDS) };
			_overlapCheckTimer.Tick += (_, _) => OverlapCheck();

			// Shift+click adds the scene's windows to the current stage instead of switching to it (as on macOS)
			SwitchSceneCommand = new ActionCommand(async model =>
			{
				var scene = ((SceneModel)model).Scene;
				if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
					await SceneManager!.AddToCurrent(scene);
				else
					await SceneManager!.SwitchTo(scene);
			});
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			_thisHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			_lastWidth = Width;
			UpdatePhysicalSizes();

			StartHook();	
		}

		protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			base.OnDpiChanged(oldDpi, newDpi);
			UpdatePhysicalSizes();
		}

		private void UpdatePhysicalSizes()
		{
			var dpi = VisualTreeHelper.GetDpi(this);
			_sidebarWidthPx = _lastWidth * dpi.DpiScaleX;
			_flyInEdgePx = Settings.Current.FlyInEdgeWidth * dpi.DpiScaleX;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			var handle = new WindowInteropHelper(this).Handle;
			HwndSource.FromHwnd(handle)?.AddHook(WndProc);

			// an empty PauseHotkey disables it; the tray menu always works
			var hotkey = Settings.Current.PauseHotkey;
			if (string.IsNullOrWhiteSpace(hotkey))
				return;

			if (!Settings.Current.TryGetPauseHotkey(out var modifiers, out var key))
				Log.Error($"Ignoring PauseHotkey \"{hotkey}\": expected modifiers and a key, e.g. \"Ctrl+Alt+Shift+P\"");
			else if (!Win32.RegisterHotKey(handle, PAUSE_HOTKEY_ID, modifiers | Win32.MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key)))
				Log.Error($"Could not register the {hotkey} hotkey (Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}); pause is still available from the tray menu");
			else
				pauseMenuItem.Header = $"Pause ({hotkey})";
		}

		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == PAUSE_HOTKEY_ID)
			{
				TogglePause();
				handled = true;
			}
			else if (msg == WM_TASKBARCREATED && WM_TASKBARCREATED != 0)
			{
				SceneManager?.OnExplorerRestarted();
			}

			return IntPtr.Zero;
		}

		private void TogglePause()
		{
			if (SceneManager is null)
				return;

			if (SceneManager.IsPaused)
			{
				Show();
				SceneManager.Resume();
			}
			else
			{
				SceneManager.Pause();
				Hide();
			}

			pauseMenuItem.IsChecked = SceneManager.IsPaused;
		}

		protected override void OnClosed(EventArgs e)
		{
			_overlapCheckTimer.Stop();
			Win32.UnregisterHotKey(new WindowInteropHelper(this).Handle, PAUSE_HOTKEY_ID);
			StopHook();

			trayIcon.Dispose();

			SceneManager?.Stop();

			base.OnClosed(e);

			Environment.Exit(0);
		}

		protected override async void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			_thisHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

			var windowsManager = new WindowsManager();
			IWindowStrategy strategy = Settings.Current.HideStrategy == HideStrategies.OffScreen ? new OffScreenWindowStrategy() : new NormalizeAndMinimizeWindowStrategy();
			SceneManager = new SceneManager(windowsManager, new Desktop(), strategy, Settings.Current, GroupMemory.Load(GroupMemory.DefaultFilePath));
			await SceneManager.Start().ConfigureAwait(true);

			SceneManager.SceneChanged += SceneManager_SceneChanged;
			SceneManager.CurrentSceneSelectionChanged += SceneManager_CurrentSceneSelectionChanged;

			AddInitialScenes();

			var foreground = Win32.GetForegroundWindow();
			var foregroundScene = SceneManager.FindSceneForWindow(foreground);
			if (foregroundScene is object)
				await SceneManager.SwitchTo(foregroundScene).ConfigureAwait(true);

			UpdatePhysicalSizes();
			_overlapCheckTimer.Start();
		}

		private void AddInitialScenes()
		{
			var initialScenes = SceneManager.GetScenes().ToArray();
			for (int i = 0; i < initialScenes.Length; i++)
			{
				var model = SceneModel.FromScene(initialScenes[i]);
				model.IsVisible = i <= Settings.Current.MaxScenes; // i is zero based, so it should be i+1 but one scene gets selected (and removed from the sidebar) that makes i+0 again
				Scenes.Add(model);
			}
		}

		private void SceneManager_CurrentSceneSelectionChanged(object? sender, CurrentSceneSelectionChangedEventArgs args)
		{
			var currentModel = args.Current is null ? null : Scenes.FirstOrDefault(m => m.Id == args.Current.Id);

			if (currentModel is object)
			{
				var currentIndex = Scenes.IndexOf(currentModel);
				Scenes.RemoveAt(currentIndex);

				if (_removedCurrentScene is object)
					Scenes.Insert(currentIndex, _removedCurrentScene);
			}
			else
			{
				if (_removedCurrentScene is object)
					Scenes.Add(_removedCurrentScene);
			}

			_removedCurrentScene = currentModel;

			SyncVisibilityByUpdatedTimeStamp();
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			var area = this.GetMonitorWorkSize();
			this.Left = 0;
			this.Top = 0;
			this.Height = area.Height;
		}

		private void SceneManager_SceneChanged(object sender, SceneChangedEventArgs e)
		{
			this.Dispatcher.Invoke(() =>
			{
				switch (e.Change)
				{
					case ChangeType.Created:
						Scenes.Add(SceneModel.FromScene(e.Scene));
						SyncVisibilityByUpdatedTimeStamp();
						break;
					case ChangeType.Updated:
						if (AllScenes.FirstOrDefault(s => s.Id == e.Scene.Id) is SceneModel toUpdate)
							toUpdate.UpdateFromScene(e.Scene);
						break;
					case ChangeType.Removed:
						if (AllScenes.FirstOrDefault(s => s.Id == e.Scene.Id) is SceneModel toRemove)
						{
							if (toRemove.Equals(_removedCurrentScene))
								_removedCurrentScene = null;
							else
								Scenes.Remove(toRemove);
						}
						SyncVisibilityByUpdatedTimeStamp();
						break;
				}
			});
		}

		private void OnMousePressed(object? sender, MouseHookEventArgs e)
		{
			// if it's allowed to drag windows into scenes, we cannot hide the scenes
			if (EnableWindowDropToScene)
				Dispatcher.BeginInvoke(_overlapCheckTimer.Stop);

			// a press that doesn't start on the sidebar must not leave an earlier press's scene behind for the release to pull from
			_mouseDownScene = null;
			_mouseDownWindow = null;

			var foregroundWindow = Win32.GetForegroundWindow();
			if (foregroundWindow != _thisHandle)
				return;

			if (EnableWindowPullToScene)
			{
				var screenPoint = new Point(e.Data.X, e.Data.Y);
				this.Dispatcher.Invoke(() =>
				{
					(_mouseDownScene, _mouseDownWindow) = FindModelsByPoint(screenPoint);
				});
			}
		}

		private void OnMouseReleased(object? sender, MouseHookEventArgs e)
		{
			// if it's allowed to drag windows into scenes, we cannot hide the scenes
			if (EnableWindowDropToScene)
			{
				Dispatcher.BeginInvoke(_overlapCheckTimer.Start);

				var foregroundWindow = Win32.GetForegroundWindow();

				if (foregroundWindow == _thisHandle)
					return;

				var screenPoint = new Point(e.Data.X, e.Data.Y);
				this.Dispatcher.Invoke(() =>
				{
					var sceneModel = FindSceneByPoint(screenPoint);

					if (sceneModel is object)
						SceneManager.MoveWindow(foregroundWindow, sceneModel.Scene).SafeFireAndForget();
				});
			}

			if (EnableWindowPullToScene)
			{
				var scene = _mouseDownScene;
				var window = _mouseDownWindow;
				_mouseDownScene = null;
				_mouseDownWindow = null;

				// dropped outside the sidebar: pull the thumbnail that was grabbed onto the stage
				if (e.Data.X > _sidebarWidthPx && scene is object)
				{
					this.Dispatcher.Invoke(() =>
					{
						SceneManager.PopWindowFrom(scene.Scene, window?.Window).SafeFireAndForget();
					});
				}
			}
		}

		private SceneModel? FindSceneByPoint(Point p) => FindModelsByPoint(p).Scene;

		/// <summary>
		/// The sidebar scene, and the window thumbnail within it, under a screen point (physical pixels).
		/// </summary>
		private (SceneModel? Scene, WindowModel? Window) FindModelsByPoint(Point p)
		{
			var thisWindow = new WindowsWindow(_thisHandle);
			var pointOnWindow = new Point(p.X - thisWindow.Location.X, p.Y - thisWindow.Location.Y);

			var dpi = VisualTreeHelper.GetDpi(this);

			pointOnWindow.X /= dpi.DpiScaleX;
			pointOnWindow.Y /= dpi.DpiScaleY;

			WindowModel? window = null;

			var element = VisualTreeHelper.HitTest(this, pointOnWindow)?.VisualHit;

			while (element is not null)
			{
				if (element is FrameworkElement { DataContext: WindowModel w })
					window ??= w;

				if (element is FrameworkElement { DataContext: SceneModel m })
					return (m, window);

				element = element.GetParentObject();
			}

			return (null, null);
		}
		private void SyncVisibilityByUpdatedTimeStamp()
		{
			var scenes = Scenes.OrderByDescending(s => s.Updated).ToArray();
			for (int i = 0; i < scenes.Length; i++)
				scenes[i].IsVisible = i < Settings.Current.MaxScenes;
		}

		public ObservableCollection<SceneModel> Scenes { get; } = new ObservableCollection<SceneModel>();

		public IEnumerable<SceneModel> AllScenes => Scenes.Union(new[] { _removedCurrentScene });

		public ICommand SwitchSceneCommand { get; }

		public SceneManager SceneManager { get; private set; }

		public IntPtr Handle => _thisHandle;

		public WindowMode Mode
		{
			get => _mode;
			set
			{
				if (value == _mode)
					return;

				_mode = value;

				this.Topmost = value == WindowMode.Flyover;

				ApplyWindowMode();
			}
		}

		private void ApplyWindowMode()
		{
			var newLeft = Mode == StageManager.WindowMode.OffScreen ? (-1 * Width) : 0.0;
			if (Left == newLeft)
				return;

			var isIncoming = newLeft > Left;
			var easingMode = isIncoming ? EasingMode.EaseOut : EasingMode.EaseIn;

			var animation = new DoubleAnimationUsingKeyFrames();
			animation.Duration = new Duration(TimeSpan.FromSeconds(0.5));
			var easingFunction = new PowerEase { EasingMode = easingMode };
			animation.KeyFrames.Add(new EasingDoubleKeyFrame(Left, KeyTime.FromPercent(0)));
			animation.KeyFrames.Add(new EasingDoubleKeyFrame(newLeft, KeyTime.FromPercent(1.0), easingFunction));

			BeginAnimation(LeftProperty, animation);
		}

		private void StartHook()
		{
			_hook = new TaskPoolGlobalHook();

			_hook.MousePressed += OnMousePressed;
			_hook.MouseReleased += OnMouseReleased;
			_hook.MouseMoved += _hook_MouseMoved;

			// mouse only: we never need keystrokes, so don't install a global keyboard hook
			_hook.RunAsync(GlobalHookType.Mouse, useBackgroundThread: true).SafeFireAndForget();
		}

		private void StopHook()
		{
			_hook.MousePressed -= OnMousePressed;
			_hook.MouseReleased -= OnMouseReleased;
			_hook.MouseMoved -= _hook_MouseMoved;

			try
			{
				_hook.Dispose();
			}
			catch (HookException)
			{
			}
		}

		private void _hook_MouseMoved(object? sender, MouseHookEventArgs e)
		{
			_mouse.X = e.Data.X;
			_mouse.Y = e.Data.Y;

			if (Mode == WindowMode.OffScreen && e.Data.X <= _flyInEdgePx)
			{
				Dispatcher.Invoke(() => Mode = WindowMode.Flyover);
			}
		}

		private void OverlapCheck()
		{
			var sceneManager = SceneManager;
			if (sceneManager is null || sceneManager.IsPaused)
				return;

			try
			{
				var currentWindows = sceneManager.GetCurrentWindows().ToArray(); // in case the enumeration changes
				UpdateModeByWindows(currentWindows);
			}
			catch (Exception ex)
			{
				Log.Error("Overlap check failed", ex);
			}
		}

		private void UpdateModeByWindows(IEnumerable<IWindow> windows)
		{
			bool doesOverlap(IWindowLocation loc) => loc.State == Native.Window.WindowState.Maximized || (loc.State == Native.Window.WindowState.Normal && (loc.X * 2) < _sidebarWidthPx);

			var anyOverlappingWindows = windows.Any(w => doesOverlap(w.Location));

			var containsMouse = _mouse.X <= _sidebarWidthPx;
			var setMode = Mode == WindowMode.OnScreen && !containsMouse
							|| Mode == WindowMode.OffScreen
							|| (Mode == WindowMode.Flyover && !containsMouse);

			if (setMode)
				Mode = anyOverlappingWindows ? WindowMode.OffScreen : WindowMode.OnScreen;
		}

		private void NavigateToProjectPage()
		{
			Process.Start(new ProcessStartInfo("https://github.com/awaescher/StageManager")
			{
				UseShellExecute = true
			});
		}

		public static bool StartsWithWindows
		{ 
			get => AutoStart.IsStartup(APP_NAME);
			set => AutoStart.SetStartup(APP_NAME, value);
		}

		private void MenuItem_ProjectPage_Click(object sender, RoutedEventArgs e)
		{
			NavigateToProjectPage();
		}

		private void MenuItem_Settings_Click(object sender, RoutedEventArgs e)
		{
			// the file is created on startup; open it in the user's .json editor (falls back to the "Open with" dialog)
			try
			{
				Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				Log.Error($"Could not open {Settings.FilePath}", ex);
			}
		}

		private void MenuItem_ForgetSets_Click(object sender, RoutedEventArgs e)
		{
			SceneManager?.ForgetLearnedGroups();
		}

		private void MenuItem_Pause_Click(object sender, RoutedEventArgs e)
		{
			TogglePause();
		}

		private void MenuItem_Quit_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void ContextMenu_Closed(object sender, RoutedEventArgs e)
		{
			StartHook();
		}

		private void ContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			StopHook();
		}
	}

	public enum WindowMode
	{
		OnScreen,
		OffScreen,
		Flyover
	}
}
