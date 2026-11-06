namespace SimoNotes2.Desktop;

internal sealed class DesktopWindowFinder
{
    private const string WorkerWindowClass = "WorkerW";
    private const string ShellViewClass = "SHELLDLL_DefView";

    public IntPtr FindDesktopHost()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            throw new InvalidOperationException("Windows Explorer's Progman desktop window was not found.");
        }

        NativeMethods.SendMessageTimeout(
            progman,
            NativeMethods.ProgmanWorkerMessage,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.SmtoAbortIfHung,
            1000,
            out _);

        var shellView = FindShellView();
        if (shellView != IntPtr.Zero)
        {
            var shellViewTopLevel = FindTopLevelParent(NativeMethods.GetParent(shellView));
            var workerWindow = NativeMethods.FindWindowEx(IntPtr.Zero, shellViewTopLevel, WorkerWindowClass, null);
            if (IsUsableDesktopHost(workerWindow, progman))
            {
                return workerWindow;
            }
        }

        return progman;
    }

    private static IntPtr FindShellView()
    {
        var shellView = IntPtr.Zero;
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            NativeMethods.EnumChildWindows(windowHandle, (childHandle, _) =>
            {
                if (GetClassName(childHandle) != ShellViewClass)
                {
                    return true;
                }

                shellView = childHandle;
                return false;
            }, IntPtr.Zero);

            return shellView == IntPtr.Zero;
        }, IntPtr.Zero);

        return shellView;
    }

    private static IntPtr FindTopLevelParent(IntPtr windowHandle)
    {
        var parent = NativeMethods.GetParent(windowHandle);
        while (parent != IntPtr.Zero)
        {
            windowHandle = parent;
            parent = NativeMethods.GetParent(windowHandle);
        }

        return windowHandle;
    }

    private static bool IsUsableDesktopHost(IntPtr windowHandle, IntPtr progman)
    {
        return windowHandle != IntPtr.Zero
            && NativeMethods.IsWindowVisible(windowHandle)
            && NativeMethods.GetClientRect(windowHandle, out var rectangle)
            && NativeMethods.GetClientRect(progman, out var desktopRectangle)
            && rectangle.Width > 0
            && rectangle.Height > 0
            && rectangle.Width >= desktopRectangle.Width / 2
            && rectangle.Height >= desktopRectangle.Height / 2;
    }

    private static string? GetClassName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        var className = new System.Text.StringBuilder(128);
        return NativeMethods.GetClassName(windowHandle, className, className.Capacity) > 0
            ? className.ToString()
            : null;
    }
}