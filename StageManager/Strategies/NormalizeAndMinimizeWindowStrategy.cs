using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace StageManager.Strategies
{
    internal class NormalizeAndMinimizeWindowStrategy : IWindowStrategy
	{
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindow(IntPtr hWnd);

		// windows this strategy minimized and hasn't shown since, so RestoreAll can also bring back the ones
		// that left every scene (e.g. they're on a virtual desktop that isn't current)
		private readonly HashSet<IntPtr> _minimized = new();
		private readonly object _lock = new();

		public void Show(IWindow window)
		{
			lock (_lock)
				_minimized.Remove(window.Handle);

			Win32.ShowWindow(window.Handle, Win32.SW.SW_SHOWNOACTIVATE);
		}

		public void Hide(IWindow window)
		{
			// don't take credit for windows the user minimized
			if (!Win32.IsIconic(window.Handle))
			{
				lock (_lock)
					_minimized.Add(window.Handle);
			}

			Win32.ShowWindow(window.Handle, Win32.SW.SW_MINIMIZE);
		}

		public void RestoreAll()
		{
			IntPtr[] minimized;
			lock (_lock)
			{
				minimized = _minimized.ToArray();
				_minimized.Clear();
			}

			foreach (var handle in minimized)
			{
				if (IsWindow(handle) && Win32.IsIconic(handle))
					Win32.ShowWindow(handle, Win32.SW.SW_SHOWNOACTIVATE);
			}
		}

		public IntPtr GetMonitor(IWindow window) => Monitors.Of(window.Handle);
	}
}
