using System.Windows;
using SimoNotes2.Desktop;
using SimoNotes2.Notes;

namespace SimoNotes2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private DesktopHost? _desktopHost;
        private NoteManager? _noteManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _desktopHost = new DesktopHost();
            _noteManager = new NoteManager(_desktopHost.NoteSurface);
            _noteManager.CreateNote(new NoteModel
            {
                Left = 80,
                Top = 80,
                Width = 300,
                Height = 210,
                Text = "Welcome to SimoNotes"
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _noteManager?.Dispose();
            _desktopHost?.Dispose();
            base.OnExit(e);
        }
    }
}
