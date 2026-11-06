using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;

namespace SimoNotes2.Desktop;

internal sealed class DesktopHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;

    private readonly HwndSource _source;
    private readonly DesktopInterop _interop = new();
    private readonly IntPtr _parentWindow;

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

        _interop.FitToDesktopWindow(_source.Handle, _parentWindow);
    }

    public Canvas NoteSurface { get; }

    public void Dispose()
    {
        _source.Dispose();
    }
}