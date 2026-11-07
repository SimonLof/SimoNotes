using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;

namespace SimoNotes.Notes;

public partial class NoteControl : UserControl
{
    private NoteAppearance _appearance = NoteAppearance.Default;
    private bool _updatingAppearance;
    private bool _isInitialized;

    public NoteControl()
    {
        InitializeComponent();
        StyleSelector.ItemsSource = NoteStyleCatalog.All;
        DragThumb.DragDelta += DragThumb_DragDelta;
        SetAppearance(_appearance, notify: false);
        _isInitialized = true;
    }

    public event EventHandler? CloseRequested;
    public event EventHandler? NoteChanged;
    internal NoteAppearance Appearance => _appearance;

    public string NoteText
    {
        get => Editor.Text;
        set => Editor.Text = value;
    }

    public void FocusEditor()
    {
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    internal void SetAppearance(NoteAppearance appearance, bool notify = true)
    {
        appearance = appearance.Normalize();

        _appearance = appearance;
        var style = NoteStyleCatalog.Get(appearance.StyleId);
        var fontFamily = style.CreateFontFamily();
        var backgroundColor = ParseColor(style.GetBackground(appearance.Color));
        var foregroundColor = ParseColor(style.GetForeground(appearance.Color));
        var borderColor = style.BorderColor is not null
            ? ParseColor(style.BorderColor)
            : style.ColorTarget == NoteStyleColorTarget.Foreground
                ? foregroundColor
                : System.Windows.Media.Color.FromArgb(
                backgroundColor.A,
                (byte)(backgroundColor.R * 0.68),
                (byte)(backgroundColor.G * 0.68),
                (byte)(backgroundColor.B * 0.68));

        NoteBorder.Background = new SolidColorBrush(backgroundColor);
        NoteBorder.BorderBrush = new SolidColorBrush(borderColor);
        NoteBorder.CornerRadius = new CornerRadius(style.BorderRadius);
        NoteBorder.Opacity = appearance.Opacity;
        AppearanceSwatch.Background = new SolidColorBrush(
            style.ColorTarget == NoteStyleColorTarget.Foreground ? foregroundColor : backgroundColor);
        NoteTitle.Foreground = new SolidColorBrush(foregroundColor);
        NoteTitle.FontFamily = fontFamily;
        NoteTitle.FontSize = style.FontSize;
        CloseButton.Foreground = new SolidColorBrush(foregroundColor);
        Editor.Foreground = new SolidColorBrush(foregroundColor);
        Editor.FontFamily = fontFamily;
        Editor.FontSize = style.FontSize;

        _updatingAppearance = true;
        StyleSelector.SelectedValue = style.Id;
        NoteStyleCatalog.PopulateSwatches(ColorSwatchGrid, style, appearance.Color, color =>
            SetAppearance(_appearance with { Color = color }));
        OpacitySlider.Value = appearance.Opacity;
        OpacityValueText.Text = $"{Math.Round(appearance.Opacity * 100)}%";
        _updatingAppearance = false;

        if (notify)
        {
            NoteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DragThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            window.Left += e.HorizontalChange;
            window.Top += e.VerticalChange;
            NoteChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        Canvas.SetLeft(this, Canvas.GetLeft(this) + e.HorizontalChange);
        Canvas.SetTop(this, Canvas.GetTop(this) + e.VerticalChange);
        NoteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
        NoteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        NoteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AppearanceButton_Click(object sender, RoutedEventArgs e)
    {
        AppearancePopup.IsOpen = !AppearancePopup.IsOpen;
    }

    private void StyleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized && !_updatingAppearance && StyleSelector.SelectedValue is string styleId)
        {
            var style = NoteStyleCatalog.Get(styleId);
            SetAppearance(_appearance with
            {
                StyleId = style.Id,
                Color = style.DefaultColor
            });
        }
    }

    private static System.Windows.Media.Color ParseColor(string color) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingAppearance || OpacityValueText is null)
        {
            return;
        }

        OpacityValueText.Text = $"{Math.Round(e.NewValue * 100)}%";
        SetAppearance(_appearance with { Opacity = e.NewValue });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}