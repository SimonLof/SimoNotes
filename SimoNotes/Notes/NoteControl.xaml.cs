using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SimoNotes.Notes;

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

    public void FocusEditor()
    {
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    private void DragThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            window.Left += e.HorizontalChange;
            window.Top += e.VerticalChange;
            return;
        }

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