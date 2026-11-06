using SimoNotes.Desktop;
using SimoNotes.Notes;

using System.Windows;

namespace SimoNotes;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private DesktopHost? _desktopHost;
    private NoteManager? _noteManager;
    private int _createdNoteCount;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _desktopHost = new DesktopHost();
        _desktopHost.NewNoteRequested += OnNewNoteRequested;
        _noteManager = new NoteManager(_desktopHost.NoteSurface);
        _noteManager.CreateNote(new NoteModel
        {
            Left = 80,
            Top = 80,
            Width = 300,
            Height = 210,
            Text = "Welcome to SimoNotes"
        });

        if(!_desktopHost.HotkeyRegistered)
        {
            MessageBox.Show(
                $"Could not register Ctrl+Alt+N. Another application may already be using this shortcut. (Windows error {_desktopHost.HotkeyRegistrationError})",
                "SimoNotes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if(_desktopHost is not null)
        {
            _desktopHost.NewNoteRequested -= OnNewNoteRequested;
        }

        _noteManager?.Dispose();
        _desktopHost?.Dispose();
        base.OnExit(e);
    }

    private void OnNewNoteRequested(object? sender, EventArgs e)
    {
        if(_noteManager is null)
        {
            return;
        }

        const double offset = 32;
        var position = 80 + (_createdNoteCount++ % 10) * offset;
        _noteManager.CreateFloatingNote(new NoteModel
        {
            Left = position,
            Top = position,
            Width = 300,
            Height = 210,
            Text = string.Empty
        });
    }
}
