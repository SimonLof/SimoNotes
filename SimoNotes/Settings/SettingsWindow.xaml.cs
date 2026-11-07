using System.Windows;
using System.Windows.Input;
using SimoNotes.Notes;
using WpfButton = System.Windows.Controls.Button;

namespace SimoNotes.Settings;

public partial class SettingsWindow : Window
{
    private HotkeyBinding _currentHotkey;
    private NoteAppearance _newNoteAppearance;
    private bool _isCapturingHotkey;
    private bool _updatingDefaultOpacity;
    private bool _isInitialized;

    internal SettingsWindow(HotkeyBinding currentHotkey, NoteAppearance newNoteAppearance)
    {
        InitializeComponent();
        _currentHotkey = currentHotkey;
        _newNoteAppearance = newNoteAppearance;
        HotkeyButton.Content = currentHotkey.DisplayName;
        UpdateDefaultOpacityDisplay();
        _isInitialized = true;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    internal event Action<HotkeyBinding>? HotkeyChangeRequested;
    internal event Action<NoteAppearance>? NewNoteAppearanceChangeRequested;
    internal event Action? ResetSettingsRequested;
    internal event Action? RemoveAllNotesRequested;

    internal void SetHotkey(HotkeyBinding hotkey)
    {
        _currentHotkey = hotkey;
        HotkeyButton.Content = hotkey.DisplayName;
        StatusText.Text = string.Empty;
    }

    internal void SetNewNoteAppearance(NoteAppearance appearance)
    {
        _newNoteAppearance = appearance;
        UpdateDefaultOpacityDisplay();
    }

    internal void ShowRegistrationError(int error)
    {
        HotkeyButton.Content = _currentHotkey.DisplayName;
        StatusText.Text = $"That shortcut is unavailable (Windows error {error}).";
    }

    internal void ShowSaveError()
    {
        StatusText.Text = "The shortcut changed for this session but could not be saved.";
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        HotkeyButton.Content = "Press a shortcut...";
        StatusText.Text = string.Empty;
        Focus();
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        ResetSettingsRequested?.Invoke();
    }

    private void DefaultColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string color })
        {
            SetDefaultAppearance(_newNoteAppearance with { Color = color });
        }
    }

    private void DefaultOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitialized && !_updatingDefaultOpacity)
        {
            SetDefaultAppearance(_newNoteAppearance with { Opacity = e.NewValue });
        }
    }

    private void SetDefaultAppearance(NoteAppearance appearance)
    {
        _newNoteAppearance = appearance;
        UpdateDefaultOpacityDisplay();
        NewNoteAppearanceChangeRequested?.Invoke(appearance);
    }

    private void UpdateDefaultOpacityDisplay()
    {
        _updatingDefaultOpacity = true;
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
        if (!_isCapturingHotkey)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _isCapturingHotkey = false;
            HotkeyButton.Content = _currentHotkey.DisplayName;
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

        _isCapturingHotkey = false;
        HotkeyButton.Content = _currentHotkey.DisplayName;
        HotkeyChangeRequested?.Invoke(hotkey);
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