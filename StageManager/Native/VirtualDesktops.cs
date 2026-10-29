using StageManager.Diagnostics;
using StageManager.Native.Window;
using System;
using System.Runtime.InteropServices;

namespace StageManager.Native
{
	internal interface IVirtualDesktops
	{
		/// <summary>False only for windows known to be on another virtual desktop.</summary>
		bool IsOnCurrentDesktop(IWindow window);
	}

	/// <summary>
	/// Wraps the documented IVirtualDesktopManager shell interface (Windows 10+). If it is unavailable,
	/// every window counts as being on the current desktop, which is the behavior without this check.
	/// Call from the UI thread (the COM object is created there).
	/// </summary>
	internal sealed class VirtualDesktops : IVirtualDesktops
	{
		[ComImport]
		[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IVirtualDesktopManager
		{
			[PreserveSig]
			int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

			[PreserveSig]
			int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

			[PreserveSig]
			int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
		}

		[ComImport]
		[Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
		private class VirtualDesktopManagerClass
		{
		}

		private IVirtualDesktopManager? _manager;
		private bool _unavailable;

		public bool IsOnCurrentDesktop(IWindow window)
		{
			if (_unavailable)
				return true;

			try
			{
				_manager ??= (IVirtualDesktopManager)new VirtualDesktopManagerClass();

				// fails for windows that are gone or not top-level; treat those as current
				return _manager.IsWindowOnCurrentVirtualDesktop(window.Handle, out var onCurrent) != 0 || onCurrent;
			}
			catch (Exception ex)
			{
				_unavailable = true;
				Log.Error("Virtual desktop manager unavailable; treating all windows as on the current desktop", ex);
				return true;
			}
		}
	}
}
