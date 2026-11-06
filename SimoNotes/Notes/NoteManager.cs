using System.Windows.Controls;

namespace SimoNotes2.Notes;

internal sealed class NoteManager : IDisposable
{
    private readonly Canvas _surface;
    private readonly List<NoteControl> _notes = [];

    public NoteManager(Canvas surface)
    {
        _surface = surface;
    }

    public NoteControl CreateNote(NoteModel model)
    {
        var note = new NoteControl
        {
            Width = model.Width,
            Height = model.Height,
            NoteText = model.Text
        };

        Canvas.SetLeft(note, model.Left);
        Canvas.SetTop(note, model.Top);
        note.CloseRequested += OnCloseRequested;
        _notes.Add(note);
        _surface.Children.Add(note);
        return note;
    }

    public void Dispose()
    {
        foreach (var note in _notes)
        {
            note.CloseRequested -= OnCloseRequested;
        }

        _notes.Clear();
        _surface.Children.Clear();
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
    }
}