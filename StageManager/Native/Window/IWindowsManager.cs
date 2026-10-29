using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StageManager.Native.Window
{
    public delegate void WindowFocusDelegate(IWindow window);

    /// <summary>
    /// The window tracking that <see cref="SceneManager"/> depends on; faked in the tests.
    /// </summary>
    public interface IWindowsManager
    {
        event WindowCreateDelegate WindowCreated;
        event WindowDelegate WindowDestroyed;
        event WindowUpdateDelegate WindowUpdated;
        event EventHandler<IntPtr> UntrackedFocus;

        IEnumerable<IWindow> Windows { get; }

        Task Start();
        void Stop();
    }
}
