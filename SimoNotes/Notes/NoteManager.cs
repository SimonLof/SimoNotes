using System.Windows;
using System.Windows.Controls;

namespace SimoNotes.Notes;

internal sealed class NoteManager : IDisposable
{
    private readonly Canvas _surface;
    private readonly List<NoteControl> _notes = [];
    private readonly Dictionary<NoteControl, Window> _floatingNoteWindows = [];

    public NoteManager(Canvas surface)
    {
        _surface = surface;
    }

    public NoteControl CreateNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        Canvas.SetLeft(note, model.Left);
        Canvas.SetTop(note, model.Top);
        _surface.Children.Add(note);
        return note;
    }

    public NoteControl CreateFloatingNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        var window = new Window
        {
            Content = note,
            Left = model.Left,
            Top = model.Top,
            ShowInTaskbar = false,
            ShowActivated = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            WindowStyle = WindowStyle.None
        };

        _floatingNoteWindows.Add(note, window);
        window.Deactivated += OnFloatingNoteDeactivated;
        window.Closed += OnFloatingNoteClosed;
        window.Show();
        window.Activate();
        note.FocusEditor();
        return note;
    }

    public void Dispose()
    {
        foreach (var note in _notes)
        {
            note.CloseRequested -= OnCloseRequested;
        }

        foreach (var note in _floatingNoteWindows.Keys.ToArray())
        {
            CloseFloatingNoteWindow(note);
        }

        _floatingNoteWindows.Clear();
        _notes.Clear();
        _surface.Children.Clear();
    }

    private NoteControl CreateNoteControl(NoteModel model)
    {
        var note = new NoteControl
        {
            Width = model.Width,
            Height = model.Height,
            NoteText = model.Text
        };

        note.CloseRequested += OnCloseRequested;
        _notes.Add(note);
        return note;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (sender is not NoteControl note)
        {
            return;
        }

        note.CloseRequested -= OnCloseRequested;
        _notes.Remove(note);
        _surface.Children.Remove(note);

        if (_floatingNoteWindows.Remove(note, out var window))
        {
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Close();
        }
    }

    private void OnFloatingNoteDeactivated(object? sender, EventArgs e)
    {
        if (sender is not Window window || window.Content is not NoteControl note)
        {
            return;
        }

        if (!_floatingNoteWindows.Remove(note))
        {
            return;
        }

        window.Deactivated -= OnFloatingNoteDeactivated;
        window.Closed -= OnFloatingNoteClosed;
        Canvas.SetLeft(note, window.Left);
        Canvas.SetTop(note, window.Top);
        window.Content = null;
        _surface.Children.Add(note);
        window.Close();
    }

    private void OnFloatingNoteClosed(object? sender, EventArgs e)
    {
        if (sender is not Window { Content: NoteControl note })
        {
            return;
        }

        note.CloseRequested -= OnCloseRequested;
        _notes.Remove(note);
        _floatingNoteWindows.Remove(note);
    }

    private void CloseFloatingNoteWindow(NoteControl note)
    {
        if (_floatingNoteWindows.Remove(note, out var window))
        {
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Close();
        }
    }
}