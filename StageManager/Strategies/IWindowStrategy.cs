using StageManager.Native.Window;

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
	}
}