namespace SimoNotes.Desktop;

internal sealed class DesktopInterop
{
    private readonly DesktopWindowFinder _windowFinder = new();

    public IntPtr FindHostWindow() => _windowFinder.FindDesktopHost();

    public void FitToDesktopWindow(IntPtr hostWindow, IntPtr desktopWindow)
    {
        if (!NativeMethods.GetWindowRect(desktopWindow, out var rectangle))
        {
            throw new InvalidOperationException("Could not read the desktop window's bounds.");
        }

        if (!NativeMethods.SetWindowPos(
                hostWindow,
                IntPtr.Zero,
                rectangle.Left,
                rectangle.Top,
                rectangle.Width,
                rectangle.Height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder | NativeMethods.SwpShowWindow))
        {
            throw new InvalidOperationException("Could not position the desktop host window.");
        }
    }
}