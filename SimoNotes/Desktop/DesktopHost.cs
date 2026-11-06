using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;

namespace SimoNotes.Desktop;

internal sealed class DesktopHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int NewNoteHotkeyId = 1;

    private readonly HwndSource _source;
    private readonly DesktopInterop _interop = new();
    private readonly IntPtr _parentWindow;
    private readonly HwndSourceHook _messageHook;

    public DesktopHost()
    {
        _parentWindow = _interop.FindHostWindow();
        NoteSurface = new Canvas();

        var parameters = new HwndSourceParameters("SimoNotesDesktopHost")
        {
            ParentWindow = _parentWindow,
            WindowStyle = WsPopup,
            ExtendedWindowStyle = WsExToolWindow,
            UsesPerPixelOpacity = true,
            Width = (int)SystemParameters.VirtualScreenWidth,
            Height = (int)SystemParameters.VirtualScreenHeight
        };

        _source = new HwndSource(parameters)
        {
            RootVisual = NoteSurface
        };

        _messageHook = HandleWindowMessage;
        _source.AddHook(_messageHook);
        HotkeyRegistered = NativeMethods.RegisterHotKey(
            _source.Handle,
            NewNoteHotkeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt,
            NativeMethods.VkN);
        HotkeyRegistrationError = HotkeyRegistered ? 0 : Marshal.GetLastWin32Error();

        _interop.FitToDesktopWindow(_source.Handle, _parentWindow);
    }

    public Canvas NoteSurface { get; }
    public event EventHandler? NewNoteRequested;
    public bool HotkeyRegistered { get; }
    public int HotkeyRegistrationError { get; }

    public void Dispose()
    {
        _source.RemoveHook(_messageHook);
        if (HotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, NewNoteHotkeyId);
        }

        _source.Dispose();
    }

    private IntPtr HandleWindowMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == NewNoteHotkeyId)
        {
            NewNoteRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}