using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SimoNotes2.Notes;

public partial class NoteControl : UserControl
{
    public NoteControl()
    {
        InitializeComponent();
        DragThumb.DragDelta += DragThumb_DragDelta;
    }

    public event EventHandler? CloseRequested;

    public string NoteText
    {
        get => Editor.Text;
        set => Editor.Text = value;
    }

    private void DragThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Canvas.SetLeft(this, Canvas.GetLeft(this) + e.HorizontalChange);
        Canvas.SetTop(this, Canvas.GetTop(this) + e.VerticalChange);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}