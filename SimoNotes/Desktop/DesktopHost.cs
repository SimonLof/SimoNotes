using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Threading;
using SimoNotes.Settings;

namespace SimoNotes.Desktop;

internal sealed class DesktopHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int FirstNewNoteHotkeyId = 1;
    private const int SecondNewNoteHotkeyId = 2;
    private const int FirstBringAllNotesHotkeyId = 3;
    private const int SecondBringAllNotesHotkeyId = 4;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly HwndSource _source;
    private readonly DesktopInterop _interop = new();
    private readonly IntPtr _parentWindow;
    private readonly HwndSourceHook _messageHook;
    private int _newNoteHotkeyId = FirstNewNoteHotkeyId;
    private int _bringAllNotesHotkeyId = FirstBringAllNotesHotkeyId;
    private bool _disposed;

    public DesktopHost(HotkeyBindings initialHotkeys)
    {
        _parentWindow = _interop.FindHostWindow();
        NoteSurface = new Canvas();

        var parameters = new HwndSourceParameters("SimoNotesDesktopHost")
        {
            ParentWindow = _parentWindow,
            WindowStyle = WsPopup,
            ExtendedWindowStyle = WsExToolWindow | WsExNoActivate,
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
        CurrentHotkey = initialHotkeys.NewNote;
        HotkeyRegistered = NativeMethods.RegisterHotKey(
            _source.Handle,
            _newNoteHotkeyId,
            initialHotkeys.NewNote.Modifiers,
            initialHotkeys.NewNote.VirtualKey);
        HotkeyRegistrationError = HotkeyRegistered ? 0 : Marshal.GetLastWin32Error();
        CurrentBringAllNotesHotkey = initialHotkeys.BringAllNotesToFront;
        BringAllNotesHotkeyRegistered = NativeMethods.RegisterHotKey(
            _source.Handle,
            _bringAllNotesHotkeyId,
            initialHotkeys.BringAllNotesToFront.Modifiers,
            initialHotkeys.BringAllNotesToFront.VirtualKey);
        BringAllNotesHotkeyRegistrationError = BringAllNotesHotkeyRegistered ? 0 : Marshal.GetLastWin32Error();

        _interop.FitToVirtualScreen(_source.Handle);
    }

    public Canvas NoteSurface { get; }
    public event EventHandler? NewNoteRequested;
    public event EventHandler? BringAllNotesToFrontRequested;
    public event EventHandler? DesktopBoundsChanging;
    public event EventHandler? DesktopBoundsChanged;
    public bool HotkeyRegistered { get; private set; }
    public int HotkeyRegistrationError { get; private set; }
    public bool BringAllNotesHotkeyRegistered { get; private set; }
    public int BringAllNotesHotkeyRegistrationError { get; private set; }
    public HotkeyBinding CurrentHotkey { get; private set; }
    public HotkeyBinding CurrentBringAllNotesHotkey { get; private set; }

    public bool TrySetNewNoteHotkey(HotkeyBinding hotkey, out int error)
    {
        if (!hotkey.IsValid)
        {
            error = 87;
            return false;
        }

        if (hotkey == CurrentBringAllNotesHotkey)
        {
            error = ErrorHotkeyAlreadyRegistered;
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

    public bool TrySetBringAllNotesHotkey(HotkeyBinding hotkey, out int error)
    {
        if (!hotkey.IsValid)
        {
            error = 87;
            return false;
        }

        if (hotkey == CurrentHotkey)
        {
            error = ErrorHotkeyAlreadyRegistered;
            return false;
        }

        if (BringAllNotesHotkeyRegistered && CurrentBringAllNotesHotkey == hotkey)
        {
            error = 0;
            return true;
        }

        var candidateId = BringAllNotesHotkeyRegistered
            ? (_bringAllNotesHotkeyId == FirstBringAllNotesHotkeyId
                ? SecondBringAllNotesHotkeyId
                : FirstBringAllNotesHotkeyId)
            : _bringAllNotesHotkeyId;

        if (!NativeMethods.RegisterHotKey(_source.Handle, candidateId, hotkey.Modifiers, hotkey.VirtualKey))
        {
            error = Marshal.GetLastWin32Error();
            BringAllNotesHotkeyRegistrationError = error;
            return false;
        }

        if (BringAllNotesHotkeyRegistered &&
            !NativeMethods.UnregisterHotKey(_source.Handle, _bringAllNotesHotkeyId))
        {
            error = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterHotKey(_source.Handle, candidateId);
            BringAllNotesHotkeyRegistrationError = error;
            return false;
        }

        _bringAllNotesHotkeyId = candidateId;
        CurrentBringAllNotesHotkey = hotkey;
        BringAllNotesHotkeyRegistered = true;
        BringAllNotesHotkeyRegistrationError = 0;
        error = 0;
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _source.RemoveHook(_messageHook);
        if (HotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, _newNoteHotkeyId);
        }
        if (BringAllNotesHotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, _bringAllNotesHotkeyId);
        }

        _source.Dispose();
    }

    private IntPtr HandleWindowMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmMouseActivate)
        {
            handled = true;
            return new IntPtr(NativeMethods.MaNoActivate);
        }

        if (message is NativeMethods.WmDisplayChange or NativeMethods.WmDpiChanged)
        {
            DesktopBoundsChanging?.Invoke(this, EventArgs.Empty);
            _source.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (_disposed)
                {
                    return;
                }

                _interop.FitToVirtualScreen(_source.Handle);
                NoteSurface.UpdateLayout();
                DesktopBoundsChanged?.Invoke(this, EventArgs.Empty);
            }));
        }

        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == _newNoteHotkeyId)
        {
            NewNoteRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (message == NativeMethods.WmHotkey && wParam.ToInt32() == _bringAllNotesHotkeyId)
        {
            BringAllNotesToFrontRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}