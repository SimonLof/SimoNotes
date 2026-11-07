using System.IO;
using DrawingIcon = System.Drawing.Icon;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using SimoNotes.Desktop;
using SimoNotes.Notes;
using SimoNotes.Settings;

using System.Windows;
using Forms = System.Windows.Forms;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfPoint = System.Windows.Point;

namespace SimoNotes;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private DesktopHost? _desktopHost;
    private NoteManager? _noteManager;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private DrawingIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private readonly HotkeyBindingStore _hotkeyBindingStore = new();
    private readonly NoteStateStore _noteStateStore = new();
    private readonly NoteAppearanceStore _noteAppearanceStore = new();
    private readonly DispatcherTimer _noteSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private NoteAppearance _newNoteAppearance = NoteAppearance.Default;
    private int _createdNoteCount;
    private bool _noteSaveErrorShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _desktopHost = new DesktopHost(_hotkeyBindingStore.Load());
        _newNoteAppearance = _noteAppearanceStore.Load();
        _desktopHost.NewNoteRequested += OnNewNoteRequested;
        _noteManager = new NoteManager(_desktopHost.NoteSurface);
        _desktopHost.DesktopBoundsChanging += _noteManager.CaptureDesktopNotePositions;
        _desktopHost.DesktopBoundsChanged += _noteManager.RestoreDesktopNotePositions;
        _noteManager.NotesChanged += OnNotesChanged;
        _noteSaveTimer.Tick += OnNoteSaveTimerTick;
        InitializeTrayIcon();

        var savedNotes = _noteStateStore.Load();
        if (savedNotes is null)
        {
            var position = GetPrimaryScreenNotePosition(80);
            _noteManager.CreateNote(new NoteModel
            {
                Left = position.X,
                Top = position.Y,
                Width = 300,
                Height = 210,
                Text = "Welcome to SimoNotes"
            });
        }
        else
        {
            foreach (var note in savedNotes)
            {
                _noteManager.CreateNote(note);
            }
        }

        if(!_desktopHost.HotkeyRegistered)
        {
            MessageBox.Show(
                $"Could not register {_desktopHost.CurrentHotkey.DisplayName}. Another application may already be using this shortcut. (Windows error {_desktopHost.HotkeyRegistrationError})",
                "SimoNotes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _noteSaveTimer.Stop();
        _noteSaveTimer.Tick -= OnNoteSaveTimerTick;
        if (_noteManager is not null)
        {
            _noteManager.NotesChanged -= OnNotesChanged;
            SaveNotesNow();
        }

        if(_desktopHost is not null)
        {
            _desktopHost.NewNoteRequested -= OnNewNoteRequested;
        }

        _noteManager?.Dispose();
        _desktopHost?.Dispose();
        _notifyIcon?.Dispose();
        _trayMenu?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void InitializeTrayIcon()
    {
        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add("New note", null, (_, _) => OnNewNoteRequested(this, EventArgs.Empty));
        _trayMenu.Items.Add("Exit", null, (_, _) => Shutdown());
        _trayMenu.Items.Add("Settings", null, (_, _) => ShowSettings());

        _trayIcon = new DrawingIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SimoNotes.ico"));
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _trayMenu,
            Icon = _trayIcon,
            Text = "SimoNotes",
            Visible = true
        };
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        if (_desktopHost is null)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_desktopHost.CurrentHotkey, _newNoteAppearance);
        settingsWindow.HotkeyChangeRequested += OnHotkeyChangeRequested;
        settingsWindow.NewNoteAppearanceChangeRequested += OnNewNoteAppearanceChangeRequested;
        settingsWindow.ResetSettingsRequested += OnResetSettingsRequested;
        settingsWindow.RemoveAllNotesRequested += OnRemoveAllNotesRequested;
        settingsWindow.Closed += (_, _) =>
        {
            settingsWindow.HotkeyChangeRequested -= OnHotkeyChangeRequested;
            settingsWindow.NewNoteAppearanceChangeRequested -= OnNewNoteAppearanceChangeRequested;
            settingsWindow.ResetSettingsRequested -= OnResetSettingsRequested;
            settingsWindow.RemoveAllNotesRequested -= OnRemoveAllNotesRequested;
            _settingsWindow = null;
        };

        _settingsWindow = settingsWindow;
        settingsWindow.Show();
    }

    private void OnHotkeyChangeRequested(HotkeyBinding hotkey)
    {
        ApplyHotkey(hotkey);
    }

    private void OnResetSettingsRequested()
    {
        ApplyHotkey(HotkeyBinding.Default);
        OnNewNoteAppearanceChangeRequested(NoteAppearance.Default);
        _settingsWindow?.SetNewNoteAppearance(NoteAppearance.Default);
    }

    private void OnNewNoteAppearanceChangeRequested(NoteAppearance appearance)
    {
        _newNoteAppearance = appearance;
        try
        {
            _noteAppearanceStore.Save(appearance);
        }
        catch (IOException)
        {
            _settingsWindow?.ShowSaveError();
        }
        catch (UnauthorizedAccessException)
        {
            _settingsWindow?.ShowSaveError();
        }
    }

    private void ApplyHotkey(HotkeyBinding hotkey)
    {
        if (_desktopHost is null || _settingsWindow is null)
        {
            return;
        }

        if (!_desktopHost.TrySetNewNoteHotkey(hotkey, out var error))
        {
            _settingsWindow.ShowRegistrationError(error);
            return;
        }

        _settingsWindow.SetHotkey(hotkey);
        try
        {
            _hotkeyBindingStore.Save(hotkey);
        }
        catch (IOException)
        {
            _settingsWindow.ShowSaveError();
        }
        catch (UnauthorizedAccessException)
        {
            _settingsWindow.ShowSaveError();
        }
    }

    private void OnRemoveAllNotesRequested()
    {
        _noteManager?.RemoveAllNotes();
        _noteSaveTimer.Stop();
        SaveNotesNow();
    }

    private void OnNotesChanged(object? sender, EventArgs e)
    {
        _noteSaveTimer.Stop();
        _noteSaveTimer.Start();
    }

    private void OnNoteSaveTimerTick(object? sender, EventArgs e)
    {
        _noteSaveTimer.Stop();
        SaveNotesNow();
    }

    private void SaveNotesNow()
    {
        if (_noteManager is null)
        {
            return;
        }

        try
        {
            _noteStateStore.Save(_noteManager.GetNoteModels());
        }
        catch (IOException)
        {
            ShowNoteSaveError();
        }
        catch (UnauthorizedAccessException)
        {
            ShowNoteSaveError();
        }
    }

    private void ShowNoteSaveError()
    {
        if (_noteSaveErrorShown)
        {
            return;
        }

        _noteSaveErrorShown = true;
        MessageBox.Show(
            "SimoNotes could not save your notes. Check that your user profile is writable.",
            "SimoNotes",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnNewNoteRequested(object? sender, EventArgs e)
    {
        if(_noteManager is null)
        {
            return;
        }

        const double offset = 32;
        var position = GetPrimaryScreenNotePosition(80 + (_createdNoteCount++ % 10) * offset);
        _noteManager.CreateFloatingNote(new NoteModel
        {
            Left = position.X,
            Top = position.Y,
            Width = 300,
            Height = 210,
            Text = string.Empty,
            Appearance = _newNoteAppearance
        });
    }

    private WpfPoint GetPrimaryScreenNotePosition(double offset)
    {
        var primaryScreen = Forms.Screen.PrimaryScreen;
        if (primaryScreen is null || _noteManager is null)
        {
            return new WpfPoint(offset, offset);
        }

        return _noteManager.ScreenToSurface(new WpfPoint(
            primaryScreen.Bounds.Left + offset,
            primaryScreen.Bounds.Top + offset));
    }
}
