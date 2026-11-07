namespace SimoNotes.Desktop;

internal sealed class DesktopInterop
{
    private readonly DesktopWindowFinder _windowFinder = new();

    public IntPtr FindHostWindow() => _windowFinder.FindDesktopHost();

    public void FitToVirtualScreen(IntPtr hostWindow)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (!NativeMethods.SetWindowPos(
                hostWindow,
                IntPtr.Zero,
                left,
                top,
                width,
                height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder | NativeMethods.SwpShowWindow))
        {
            throw new InvalidOperationException("Could not fit the desktop host to the virtual screen.");
        }
    }
}