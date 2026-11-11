using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;
using SimoNotes.Notes;
using Button = System.Windows.Controls.Button;

namespace SimoNotes.Settings;

public partial class SettingsWindow : Window
{
    private HotkeyBinding _currentHotkey;
    private HotkeyBinding _currentBringAllNotesHotkey;
    private NoteAppearance _newNoteAppearance;
    private Button? _capturingHotkeyButton;
    private bool _updatingDefaultOpacity;
    private bool _isInitialized;
    private bool _autostartEnabled;

    internal SettingsWindow(
        HotkeyBinding currentHotkey,
        HotkeyBinding currentBringAllNotesHotkey,
        NoteAppearance newNoteAppearance,
        int noteCount,
        bool autostartEnabled)
    {
        InitializeComponent();
        DefaultStyleSelector.ItemsSource = NoteStyleCatalog.All;
        _currentHotkey = currentHotkey;
        _currentBringAllNotesHotkey = currentBringAllNotesHotkey;
        _newNoteAppearance = newNoteAppearance;
        _autostartEnabled = autostartEnabled;
        NewNoteHotkeyButton.Content = currentHotkey.DisplayName;
        BringAllNotesHotkeyButton.Content = currentBringAllNotesHotkey.DisplayName;
        SetNoteCount(noteCount);
        UpdateAutostartControls();
        UpdateNewNoteAppearanceControls();
        _isInitialized = true;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    internal event Action<HotkeyBinding>? HotkeyChangeRequested;
    internal event Action<HotkeyBinding>? BringAllNotesHotkeyChangeRequested;
    internal event Action<NoteAppearance>? NewNoteAppearanceChangeRequested;
    internal event Action? ResetSettingsRequested;
    internal event Action? RemoveAllNotesRequested;
    internal event Action<bool>? AutostartChangeRequested;

    internal void SetAutostartEnabled(bool enabled)
    {
        _autostartEnabled = enabled;
        UpdateAutostartControls();
    }

    internal void ShowAutostartError(string message)
    {
        AutostartStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x9A, 0x3D, 0x2F));
        AutostartStatusText.Text = $"Could not update autostart: {message}";
    }

    internal void SetHotkey(HotkeyBinding hotkey)
    {
        _currentHotkey = hotkey;
        NewNoteHotkeyButton.Content = hotkey.DisplayName;
        StatusText.Text = string.Empty;
    }

    internal void SetBringAllNotesHotkey(HotkeyBinding hotkey)
    {
        _currentBringAllNotesHotkey = hotkey;
        BringAllNotesHotkeyButton.Content = hotkey.DisplayName;
        StatusText.Text = string.Empty;
    }

    internal void SetNewNoteAppearance(NoteAppearance appearance)
    {
        _newNoteAppearance = appearance;
        UpdateNewNoteAppearanceControls();
    }

    internal void SetNoteCount(int noteCount)
    {
           NoteCountText.Text = $"Notes: {noteCount}";
    }

    internal void ShowRegistrationError(int error, bool bringAllNotesHotkey = false)
    {
        var currentHotkey = bringAllNotesHotkey ? _currentBringAllNotesHotkey : _currentHotkey;
        var button = bringAllNotesHotkey ? BringAllNotesHotkeyButton : NewNoteHotkeyButton;
        button.Content = currentHotkey.DisplayName;
        StatusText.Text = $"That shortcut is unavailable (Windows error {error}).";
    }

    internal void ShowSaveError()
    {
        StatusText.Text = "The shortcut changed for this session but could not be saved.";
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _capturingHotkeyButton = button;
        button.Content = "Press a shortcut...";
        StatusText.Text = string.Empty;
        Focus();
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        ResetSettingsRequested?.Invoke();
    }

    private void FontLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        new FontLicenseWindow { Owner = this }.ShowDialog();
    }

    private void AutostartButton_Click(object sender, RoutedEventArgs e)
    {
        AutostartChangeRequested?.Invoke(!_autostartEnabled);
    }

    private void UpdateAutostartControls()
    {
        AutostartButton.Content = _autostartEnabled
            ? "Remove from autostart"
            : "Set to autostart";
        AutostartStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x77));
        AutostartStatusText.Text = _autostartEnabled
            ? "SimoNotes will start when you sign in to Windows."
            : "SimoNotes will not start automatically.";
    }

    private void CreatorLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void DefaultStyleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized && !_updatingDefaultOpacity && DefaultStyleSelector.SelectedValue is string styleId)
        {
            var style = NoteStyleCatalog.Get(styleId);
            SetDefaultAppearance(_newNoteAppearance with
            {
                StyleId = style.Id,
                Color = style.DefaultColor
            });
        }
    }

    private void DefaultOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitialized && !_updatingDefaultOpacity)
        {
            SetDefaultAppearance(_newNoteAppearance with { Opacity = e.NewValue });
        }
    }

    private void DefaultColorSwatchSelected(string color)
    {
        SetDefaultAppearance(_newNoteAppearance with { Color = color });
    }

    private void SetDefaultAppearance(NoteAppearance appearance)
    {
        _newNoteAppearance = appearance;
        UpdateNewNoteAppearanceControls();
        NewNoteAppearanceChangeRequested?.Invoke(appearance);
    }

    private void UpdateNewNoteAppearanceControls()
    {
        _updatingDefaultOpacity = true;
        DefaultStyleSelector.SelectedValue = _newNoteAppearance.StyleId;
        var style = NoteStyleCatalog.Get(_newNoteAppearance.StyleId);
        NoteStyleCatalog.PopulateSwatches(
            DefaultColorSwatchGrid,
            style,
            _newNoteAppearance.Color,
            DefaultColorSwatchSelected);
        DefaultOpacitySlider.Value = _newNoteAppearance.Opacity;
        DefaultOpacityValueText.Text = $"{Math.Round(_newNoteAppearance.Opacity * 100)}%";
        _updatingDefaultOpacity = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RemoveAllNotes_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Remove every note? This cannot be undone.",
            "Remove all notes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            RemoveAllNotesRequested?.Invoke();
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_capturingHotkeyButton is not { } button)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _capturingHotkeyButton = null;
            RestoreHotkeyButton(button);
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        var modifiers = GetNativeModifiers(Keyboard.Modifiers);
        if (modifiers == 0)
        {
            StatusText.Text = "Include Ctrl, Alt, or Shift in the shortcut.";
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var hotkey = new HotkeyBinding(modifiers, (uint)virtualKey);
        if (!hotkey.IsValid)
        {
            StatusText.Text = "That key cannot be used for a shortcut.";
            return;
        }

        _capturingHotkeyButton = null;
        RestoreHotkeyButton(button);
        if (button == BringAllNotesHotkeyButton)
        {
            BringAllNotesHotkeyChangeRequested?.Invoke(hotkey);
        }
        else
        {
            HotkeyChangeRequested?.Invoke(hotkey);
        }
    }

    private void RestoreHotkeyButton(Button button)
    {
        button.Content = button == BringAllNotesHotkeyButton
            ? _currentBringAllNotesHotkey.DisplayName
            : _currentHotkey.DisplayName;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private static uint GetNativeModifiers(ModifierKeys modifiers)
    {
        uint nativeModifiers = 0;
        if ((modifiers & ModifierKeys.Control) != 0) nativeModifiers |= 0x0002;
        if ((modifiers & ModifierKeys.Alt) != 0) nativeModifiers |= 0x0001;
        if ((modifiers & ModifierKeys.Shift) != 0) nativeModifiers |= 0x0004;
        return nativeModifiers;
    }
}