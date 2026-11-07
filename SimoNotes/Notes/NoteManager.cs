using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    public event EventHandler? NotesChanged;

    public NoteControl CreateNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        Canvas.SetLeft(note, model.Left);
        Canvas.SetTop(note, model.Top);
        _surface.Children.Add(note);
        OnNotesChanged();
        return note;
    }

    public NoteControl CreateFloatingNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        var window = new Window
        {
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
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
        OnNotesChanged();
        return note;
    }

    public List<NoteModel> GetNoteModels()
    {
        return _notes.Select(note =>
        {
            var left = _floatingNoteWindows.TryGetValue(note, out var window) ? window.Left : Canvas.GetLeft(note);
            var top = _floatingNoteWindows.TryGetValue(note, out window) ? window.Top : Canvas.GetTop(note);

            return new NoteModel
            {
                Left = double.IsFinite(left) ? left : 0,
                Top = double.IsFinite(top) ? top : 0,
                Width = note.Width,
                Height = note.Height,
                Text = note.NoteText,
                Appearance = note.Appearance
            };
        }).ToList();
    }

    public void RemoveAllNotes()
    {
        foreach (var note in _notes.ToArray())
        {
            note.CloseRequested -= OnCloseRequested;
            note.NoteChanged -= OnNoteChanged;
            _surface.Children.Remove(note);

            if (_floatingNoteWindows.Remove(note, out var window))
            {
                window.Deactivated -= OnFloatingNoteDeactivated;
                window.Closed -= OnFloatingNoteClosed;
                window.Content = null;
                window.Close();
            }
        }

        _notes.Clear();
        _surface.Children.Clear();
        OnNotesChanged();
    }

    public void Dispose()
    {
        foreach (var note in _notes)
        {
            note.CloseRequested -= OnCloseRequested;
            note.NoteChanged -= OnNoteChanged;
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
        note.SetAppearance(model.Appearance, notify: false);

        note.CloseRequested += OnCloseRequested;
        note.NoteChanged += OnNoteChanged;
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
        note.NoteChanged -= OnNoteChanged;
        _notes.Remove(note);
        _surface.Children.Remove(note);

        if (_floatingNoteWindows.Remove(note, out var window))
        {
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Close();
        }

        OnNotesChanged();
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
        OnNotesChanged();
    }

    private void OnFloatingNoteClosed(object? sender, EventArgs e)
    {
        if (sender is not Window { Content: NoteControl note })
        {
            return;
        }

        note.CloseRequested -= OnCloseRequested;
        note.NoteChanged -= OnNoteChanged;
        _notes.Remove(note);
        _floatingNoteWindows.Remove(note);
        OnNotesChanged();
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

    private void OnNoteChanged(object? sender, EventArgs e)
    {
        OnNotesChanged();
    }

    private void OnNotesChanged()
    {
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }
}