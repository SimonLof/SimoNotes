using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;
using WpfButton = System.Windows.Controls.Button;

namespace SimoNotes.Notes;

public partial class NoteControl : UserControl
{
    private NoteAppearance _appearance = NoteAppearance.Default;
    private bool _updatingAppearance;

    public NoteControl()
    {
        InitializeComponent();
        DragThumb.DragDelta += DragThumb_DragDelta;
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
        if (!appearance.IsValid)
        {
            appearance = NoteAppearance.Default;
        }

        _appearance = appearance;
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(appearance.Color);
        NoteBorder.Background = new SolidColorBrush(color);
        NoteBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
            color.A,
            (byte)(color.R * 0.68),
            (byte)(color.G * 0.68),
            (byte)(color.B * 0.68)));
        NoteBorder.Opacity = appearance.Opacity;
        AppearanceSwatch.Background = new SolidColorBrush(color);

        _updatingAppearance = true;
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

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string color })
        {
            SetAppearance(_appearance with { Color = color });
        }
    }

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