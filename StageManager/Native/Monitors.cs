using System;
using System.Runtime.InteropServices;

namespace StageManager.Native
{
	/// <summary>
	/// Which monitor a window belongs to, including windows that are minimized (judged by their restore position).
	/// </summary>
	internal static class Monitors
	{
		private const uint MONITOR_DEFAULTTONULL = 0;
		private const uint MONITOR_DEFAULTTOPRIMARY = 1;
		private const uint MONITOR_DEFAULTTONEAREST = 2;

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int X, Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct WINDOWPLACEMENT
		{
			public int length;
			public int flags;
			public int showCmd;
			public POINT ptMinPosition;
			public POINT ptMaxPosition;
			public RECT rcNormalPosition;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsIconic(IntPtr hWnd);

		/// <summary>The monitor at (0,0), where the sidebar lives.</summary>
		public static IntPtr Primary => MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);

		/// <summary>True when some monitor extends left of the primary one, so x &lt; 0 is on screen.</summary>
		public static bool AnyLeftOfPrimary => System.Windows.Forms.SystemInformation.VirtualScreen.Left < 0;

		/// <summary>The monitor a window is on, or (when minimized) the one it restores to.</summary>
		public static IntPtr Of(IntPtr hwnd)
		{
			if (IsIconic(hwnd))
			{
				// rcNormalPosition is in workspace coordinates; off by the taskbar at most, which doesn't change the monitor
				var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
				if (GetWindowPlacement(hwnd, ref placement))
					return MonitorFromRect(ref placement.rcNormalPosition, MONITOR_DEFAULTTONEAREST);
			}

			return MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
		}

		public static IntPtr OfRect(int left, int top, int right, int bottom)
		{
			var rect = new RECT { Left = left, Top = top, Right = right, Bottom = bottom };
			return MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
		}

		/// <summary>The monitor showing the window, or zero if it is entirely off-screen (e.g. parked).</summary>
		public static IntPtr Showing(IntPtr hwnd) => MonitorFromWindow(hwnd, MONITOR_DEFAULTTONULL);
	}
}
