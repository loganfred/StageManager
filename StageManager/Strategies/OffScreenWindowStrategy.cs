using StageManager.Diagnostics;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace StageManager.Strategies
{
	/// <summary>
	/// Hides windows by parking them just below the virtual screen, keeping their x position so Windows still
	/// associates them with their own monitor (and DPI). Compared with minimizing there is no animation,
	/// maximized and snapped sizes survive, and sidebar thumbnails stay live. Parked windows stay in the
	/// taskbar and Alt+Tab; activating one from there switches to its scene, which moves it back.
	/// Parked positions are journaled so a killed session can be undone on the next start.
	/// </summary>
	internal class OffScreenWindowStrategy : IWindowStrategy
	{
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindow(IntPtr hWnd);

		// ASYNCWINDOWPOS: never block on a hung app's message queue
		private const Win32.SetWindowPosFlags MoveOnly = Win32.SetWindowPosFlags.IgnoreResize | Win32.SetWindowPosFlags.IgnoreZOrder
			| Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.DoNotChangeOwnerZOrder | Win32.SetWindowPosFlags.AsynchronousWindowPosition;

		private const int ParkingMargin = 100;

		private static string JournalPath => Path.Combine(Log.Directory, "parked.txt");

		private readonly Dictionary<IntPtr, ParkedWindow> _parked = new();
		private readonly object _lock = new();

		private readonly record struct ParkedWindow(int Left, int Top, int ParkedTop);

		public void Show(IWindow window)
		{
			ParkedWindow parked;
			bool wasParked;

			lock (_lock)
			{
				wasParked = _parked.Remove(window.Handle, out parked);
				if (wasParked)
					SaveJournal();
			}

			// like the minimize strategy: everything in the shown scene comes back, including windows the user minimized.
			// Restore before moving: a window minimized while parked would otherwise restore to its parked position.
			if (Win32.IsIconic(window.Handle))
				Win32.ShowWindow(window.Handle, Win32.SW.SW_SHOWNOACTIVATE);

			if (wasParked)
				Win32.SetWindowPos(window.Handle, IntPtr.Zero, parked.Left, parked.Top, 0, 0, MoveOnly);
		}

		public void Hide(IWindow window)
		{
			// minimized windows are already out of sight; Show restores them
			if (Win32.IsIconic(window.Handle))
				return;

			var rect = new Win32.Rect();
			if (!Win32.GetWindowRect(window.Handle, ref rect))
				return;

			var parkedTop = GetParkingTop();

			lock (_lock)
			{
				if (_parked.ContainsKey(window.Handle))
					return;

				_parked[window.Handle] = new ParkedWindow(rect.Left, rect.Top, parkedTop);
				SaveJournal();
			}

			Win32.SetWindowPos(window.Handle, IntPtr.Zero, rect.Left, parkedTop, 0, 0, MoveOnly);
		}

		public void RestoreAll()
		{
			KeyValuePair<IntPtr, ParkedWindow>[] parked;

			lock (_lock)
			{
				parked = _parked.ToArray();
				_parked.Clear();
			}

			foreach (var (handle, p) in parked)
			{
				if (IsWindow(handle))
					Win32.SetWindowPos(handle, IntPtr.Zero, p.Left, p.Top, 0, 0, MoveOnly);
			}

			DeleteJournal();
		}

		/// <summary>
		/// Moves back windows that a killed session left parked. Runs at startup whatever the configured strategy.
		/// </summary>
		public static void RecoverFromUncleanExit()
		{
			try
			{
				if (!File.Exists(JournalPath))
					return;

				var restored = 0;
				foreach (var line in File.ReadAllLines(JournalPath))
				{
					var parts = line.Split(' ');
					if (parts.Length != 4
						|| !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var handleValue)
						|| !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
						|| !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top)
						|| !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parkedTop))
						continue;

					var handle = new IntPtr(handleValue);
					var rect = new Win32.Rect();

					// only move windows that are still where we parked them (the handle may have been reused since)
					if (IsWindow(handle) && Win32.GetWindowRect(handle, ref rect) && rect.Top == parkedTop)
					{
						Win32.SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, MoveOnly);
						restored++;
					}
				}

				Log.Info($"Previous session did not exit cleanly; moved {restored} parked window(s) back");
				DeleteJournal();
			}
			catch (Exception ex)
			{
				Log.Error("Could not restore parked windows from the last session", ex);
			}
		}

		private static int GetParkingTop() => System.Windows.Forms.SystemInformation.VirtualScreen.Bottom + ParkingMargin;

		// called under _lock
		private void SaveJournal()
		{
			try
			{
				Directory.CreateDirectory(Log.Directory);
				File.WriteAllLines(JournalPath, _parked.Select(p => string.Create(CultureInfo.InvariantCulture,
					$"{p.Key.ToInt64()} {p.Value.Left} {p.Value.Top} {p.Value.ParkedTop}")));
			}
			catch (Exception ex)
			{
				Log.Error("Could not write the parked-windows journal", ex);
			}
		}

		private static void DeleteJournal()
		{
			try
			{
				File.Delete(JournalPath);
			}
			catch (Exception ex)
			{
				Log.Error("Could not delete the parked-windows journal", ex);
			}
		}
	}
}
