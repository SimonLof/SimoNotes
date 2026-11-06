using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using SimoNotes.Settings;

namespace SimoNotes.Desktop;

internal sealed class DesktopHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int FirstNewNoteHotkeyId = 1;
    private const int SecondNewNoteHotkeyId = 2;

    private readonly HwndSource _source;
    private readonly DesktopInterop _interop = new();
    private readonly IntPtr _parentWindow;
    private readonly HwndSourceHook _messageHook;
    private int _newNoteHotkeyId = FirstNewNoteHotkeyId;

    public DesktopHost(HotkeyBinding initialHotkey)
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
        CurrentHotkey = initialHotkey;
        HotkeyRegistered = NativeMethods.RegisterHotKey(
            _source.Handle,
            _newNoteHotkeyId,
            initialHotkey.Modifiers,
            initialHotkey.VirtualKey);
        HotkeyRegistrationError = HotkeyRegistered ? 0 : Marshal.GetLastWin32Error();

        _interop.FitToDesktopWindow(_source.Handle, _parentWindow);
    }

    public Canvas NoteSurface { get; }
    public event EventHandler? NewNoteRequested;
    public bool HotkeyRegistered { get; private set; }
    public int HotkeyRegistrationError { get; private set; }
    public HotkeyBinding CurrentHotkey { get; private set; }

    public bool TrySetNewNoteHotkey(HotkeyBinding hotkey, out int error)
    {
        if (!hotkey.IsValid)
        {
            error = 87;
            return false;
        }

        if (HotkeyRegistered && CurrentHotkey == hotkey)
        {
            error = 0;
            return true;
        }

        var candidateId = HotkeyRegistered
            ? (_newNoteHotkeyId == FirstNewNoteHotkeyId ? SecondNewNoteHotkeyId : FirstNewNoteHotkeyId)
            : _newNoteHotkeyId;

        if (!NativeMethods.RegisterHotKey(_source.Handle, candidateId, hotkey.Modifiers, hotkey.VirtualKey))
        {
            error = Marshal.GetLastWin32Error();
            HotkeyRegistrationError = error;
            return false;
        }

        if (HotkeyRegistered && !NativeMethods.UnregisterHotKey(_source.Handle, _newNoteHotkeyId))
        {
            error = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterHotKey(_source.Handle, candidateId);
            HotkeyRegistrationError = error;
            return false;
        }

        _newNoteHotkeyId = candidateId;
        CurrentHotkey = hotkey;
        HotkeyRegistered = true;
        HotkeyRegistrationError = 0;
        error = 0;
        return true;
    }

    public void Dispose()
    {
        _source.RemoveHook(_messageHook);
        if (HotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, _newNoteHotkeyId);
        }

        _source.Dispose();
    }

    private IntPtr HandleWindowMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == _newNoteHotkeyId)
        {
            NewNoteRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}