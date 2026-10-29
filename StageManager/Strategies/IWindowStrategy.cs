using StageManager.Native.Window;
using System;

namespace StageManager.Strategies
{
    internal interface IWindowStrategy
	{
		void Show(IWindow window);

		void Hide(IWindow window);

		/// <summary>
		/// Undoes every hide this strategy still has in effect, including windows that are no longer in any scene.
		/// </summary>
		void RestoreAll();

		/// <summary>
		/// The monitor the window is on when shown, even while it is hidden.
		/// </summary>
		IntPtr GetMonitor(IWindow window);
	}
}