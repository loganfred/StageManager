using System;

namespace StageManager
{
	/// <summary>
	/// Desktop icons and the desktop "view" (the wallpaper window); see <see cref="Desktop"/>.
	/// </summary>
	internal interface IDesktop
	{
		void ShowIcons();
		void HideIcons();
		void RestoreIcons();

		void TrySetDesktopView(IntPtr handle);
		void ResetDesktopView();
		bool HasDesktopView { get; }
		IntPtr DesktopViewHandle { get; }
	}
}
