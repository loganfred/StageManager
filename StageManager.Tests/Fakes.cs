using StageManager.Native;
using StageManager.Native.Window;
using StageManager.Strategies;
using System.Drawing;

namespace StageManager.Tests
{
	internal sealed class FakeWindow : IWindow
	{
		private static int _nextHandle = 0x1000;

		public FakeWindow(string processName, string? title = null)
		{
			Handle = new IntPtr(Interlocked.Increment(ref _nextHandle));
			ProcessName = processName;
			Title = title ?? $"{processName} {Handle}";
		}

		public event IWindowDelegate? WindowClosed { add { } remove { } }
		public event IWindowDelegate? WindowUpdated { add { } remove { } }
		public event IWindowDelegate? WindowFocused { add { } remove { } }

		public IntPtr Handle { get; }
		public string Title { get; }
		public string Class => "FakeWindowClass";
		public IWindowLocation Location => new WindowLocation(0, 0, 800, 600, Native.Window.WindowState.Normal);
		public Rectangle Offset => Rectangle.Empty;
		public int ProcessId => 42;
		public string ProcessFileName => ProcessName + ".exe";
		public string ProcessName { get; }
		public bool CanLayout => true;
		public bool IsFocused => false;
		public bool IsMinimized => false;
		public bool IsMaximized => false;
		public bool IsMouseMoving => false;

		public void Focus() { }
		public void Hide() { }
		public void ShowNormal() { }
		public void ShowMaximized() { }
		public void ShowMinimized() { }
		public void ShowInCurrentState() { }
		public void BringToTop() { }
		public void Close() { }
		public void NotifyUpdated() { }

		public override string ToString() => Title;
	}

	internal sealed class FakeWindowsManager : IWindowsManager
	{
		private readonly List<IWindow> _windows = new();

		public event WindowCreateDelegate? WindowCreated;
		public event WindowDelegate? WindowDestroyed;
		public event WindowUpdateDelegate? WindowUpdated;
		public event EventHandler<IntPtr>? UntrackedFocus;

		public IEnumerable<IWindow> Windows => _windows;

		public bool Stopped { get; private set; }

		public Task Start() => Task.CompletedTask;

		public void Stop() => Stopped = true;

		/// <summary>A window that already existed when Stage Manager started.</summary>
		public FakeWindow Existing(string processName)
		{
			var window = new FakeWindow(processName);
			_windows.Add(window);
			return window;
		}

		public FakeWindow Open(string processName, bool firstCreate = true)
		{
			var window = Existing(processName);
			WindowCreated?.Invoke(window, firstCreate);
			return window;
		}

		public void Close(IWindow window)
		{
			_windows.Remove(window);
			WindowDestroyed?.Invoke(window);
		}

		public void Activate(IWindow window) => WindowUpdated?.Invoke(window, WindowUpdateType.Foreground);

		public void FocusUntracked(IntPtr handle) => UntrackedFocus?.Invoke(this, handle);
	}

	internal sealed class FakeDesktop : IDesktop
	{
		public FakeDesktop(bool iconsVisible = true)
		{
			OriginalIconsVisible = IconsVisible = iconsVisible;
		}

		public bool OriginalIconsVisible { get; }
		public bool IconsVisible { get; private set; }

		public void ShowIcons() => IconsVisible = true;
		public void HideIcons() => IconsVisible = false;
		public void RestoreIcons() => IconsVisible = OriginalIconsVisible;

		public void TrySetDesktopView(IntPtr handle) => DesktopViewHandle = handle;
		public void ResetDesktopView() => DesktopViewHandle = IntPtr.Zero;
		public bool HasDesktopView => DesktopViewHandle != IntPtr.Zero;
		public IntPtr DesktopViewHandle { get; private set; }
	}

	/// <summary>Tracks which windows are visible instead of touching real ones.</summary>
	internal sealed class FakeWindowStrategy : IWindowStrategy
	{
		public HashSet<IWindow> Hidden { get; } = new();

		public void Show(IWindow window) => Hidden.Remove(window);

		public void Hide(IWindow window) => Hidden.Add(window);

		public void RestoreAll() => Hidden.Clear();

		public bool IsVisible(IWindow window) => !Hidden.Contains(window);
	}

	/// <summary>A <see cref="SceneManager"/> wired to fakes.</summary>
	internal sealed class Harness
	{
		public Harness(Settings? settings = null, GroupMemory? groups = null)
		{
			Settings = settings ?? new Settings();
			Groups = groups ?? new GroupMemory();
			Scenes = new SceneManager(Windows, Desktop, Strategy, Settings, Groups) { ReentrancyWindow = TimeSpan.Zero };
		}

		public Settings Settings { get; }
		public GroupMemory Groups { get; }
		public FakeWindowsManager Windows { get; } = new();
		public FakeDesktop Desktop { get; } = new();
		public FakeWindowStrategy Strategy { get; } = new();
		public SceneManager Scenes { get; }

		/// <summary>Builds the initial scenes from the existing windows and starts listening for events.</summary>
		public Harness Start()
		{
			Scenes.GetScenes();
			Scenes.Start().GetAwaiter().GetResult();
			return this;
		}

		public Scene SceneOf(IWindow window) => Scenes.FindSceneForWindow(window) ?? throw new InvalidOperationException($"{window} has no scene");

		public void SwitchTo(Scene? scene) => Scenes.SwitchTo(scene).GetAwaiter().GetResult();

		public void Move(IWindow window, Scene target) => Scenes.MoveWindow(SceneOf(window), window, target).GetAwaiter().GetResult();
	}
}
