using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;

namespace StageManager.Strategies
{
    internal class NormalizeAndMinimizeWindowStrategy : IWindowStrategy
	{
		public void Show(IWindow window)
		{
			Win32.ShowWindow(window.Handle, Win32.SW.SW_SHOWNOACTIVATE);
		}

		public void Hide(IWindow window)
		{
			Win32.ShowWindow(window.Handle, Win32.SW.SW_MINIMIZE);
		}

		// minimized windows that left their scene stay reachable from the taskbar
		public void RestoreAll()
		{
		}

		public System.IntPtr GetMonitor(IWindow window) => Monitors.Of(window.Handle);
	}
}
