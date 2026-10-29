// This file contains two methods taken from MahApps.Metro to be able to remove the NuGet dependency.

// Taken from
//   MahApps.Metro/src/MahApps.Metro/Controls/WinApiHelper.cs
//   MahApps.Metro/src/MahApps.Metro/Controls/TreeHelper.cs

// Original license header:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StageManager.Native
{
	public static class VisualHelper
	{
		private const uint MONITOR_DEFAULTTONEAREST = 2;

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		[StructLayout(LayoutKind.Sequential)]
		private struct MONITORINFO
		{
			public int cbSize;
			public RECT rcMonitor;
			public RECT rcWork;
			public uint dwFlags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		/// <summary>
		/// Get the working area size of the monitor from where the visual stays.
		/// </summary>
		/// <param name="visual">The visual element to get the monitor information.</param>
		/// <returns>The working area size of the monitor.</returns>
		public static Size GetMonitorWorkSize(this Visual visual)
		{
			if (visual != null)
			{
				var hwndSource = PresentationSource.FromVisual(visual) as HwndSource;
				if (hwndSource != null && !hwndSource.IsDisposed && hwndSource.RootVisual != null && hwndSource.Handle != IntPtr.Zero)
				{
					IntPtr monitor = MonitorFromWindow(hwndSource.Handle, MONITOR_DEFAULTTONEAREST);
					var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
					if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
						return new Size(info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
				}
			}

			return default;
		}

		/// <summary>
		/// This method is an alternative to WPF's <see cref="VisualTreeHelper.GetParent"/> method, which also supports content elements. Keep in mind that for content element, this method falls back to the logical tree of the element!
		/// </summary>
		/// <param name="child">The item to be processed.</param>
		/// <returns>The submitted item's parent, if available. Otherwise null.</returns>
		public static DependencyObject? GetParentObject(this DependencyObject? child)
		{
			if (child is null)
				return null;

			// handle content elements separately
			if (child is ContentElement contentElement)
			{
				DependencyObject parent = ContentOperations.GetParent(contentElement);
				if (parent is not null)
					return parent;

				return contentElement is FrameworkContentElement fce ? fce.Parent : null;
			}

			var childParent = VisualTreeHelper.GetParent(child);
			if (childParent is not null)
				return childParent;

			// also try searching for parent in framework elements (such as DockPanel, etc)
			if (child is FrameworkElement frameworkElement)
			{
				DependencyObject parent = frameworkElement.Parent;
				if (parent is not null)
					return parent;
			}

			return null;
		}
	}
}
