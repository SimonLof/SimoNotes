using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SimoNotes.Desktop;
using WpfPoint = System.Windows.Point;

namespace SimoNotes.Notes;

internal sealed class NoteManager : IDisposable
{
    private readonly Canvas _surface;
    private readonly List<NoteControl> _notes = [];
    private readonly Dictionary<NoteControl, Window> _noteWindows = [];
    private readonly Dictionary<NoteControl, Window> _floatingNoteWindows = [];
    private readonly HashSet<NoteControl> _notesBeingDragged = [];
    private readonly HashSet<NoteControl> _deactivatedDuringDrag = [];
    private Dictionary<NoteControl, WpfPoint>? _screenPositionsBeforeDesktopChange;

    public NoteManager(Canvas surface)
    {
        _surface = surface;
    }

    public event EventHandler? NotesChanged;

    public WpfPoint ScreenToSurface(WpfPoint screenPoint) => _surface.PointFromScreen(screenPoint);

    public NoteControl CreateNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        Canvas.SetLeft(note, model.Left);
        Canvas.SetTop(note, model.Top);
        _surface.Children.Add(note);
        UpdateDesktopNoteScale(note);
        if (note.AlwaysOnTop)
        {
            MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
        }
        OnNotesChanged();
        return note;
    }

    public NoteControl CreateFloatingNote(NoteModel model)
    {
        var note = CreateNoteControl(model);
        Canvas.SetLeft(note, model.Left);
        Canvas.SetTop(note, model.Top);
        _surface.Children.Add(note);
        MoveToFloatingLayer(note, moveCaretToEnd: true);
        return note;
    }

    public List<NoteModel> GetNoteModels()
    {
        return _notes.Select(note =>
        {
            var surfacePosition = _floatingNoteWindows.TryGetValue(note, out var window)
                ? _surface.PointFromScreen(window.PointToScreen(new WpfPoint(0, 0)))
                : new WpfPoint(Canvas.GetLeft(note), Canvas.GetTop(note));

            return new NoteModel
            {
                Left = double.IsFinite(surfacePosition.X) ? surfacePosition.X : 0,
                Top = double.IsFinite(surfacePosition.Y) ? surfacePosition.Y : 0,
                Width = note.Width,
                Height = note.Height,
                Title = note.Title,
                Text = note.NoteText,
                Appearance = note.Appearance,
                AlwaysOnTop = note.AlwaysOnTop
            };
        }).ToList();
    }

    public void CaptureDesktopNotePositions(object? sender, EventArgs e)
    {
        _screenPositionsBeforeDesktopChange = [];
        foreach (var note in _notes)
        {
            if (_floatingNoteWindows.ContainsKey(note))
            {
                continue;
            }

            var left = Canvas.GetLeft(note);
            var top = Canvas.GetTop(note);
            var surfacePosition = new WpfPoint(
                double.IsFinite(left) ? left : 0,
                double.IsFinite(top) ? top : 0);
            _screenPositionsBeforeDesktopChange[note] = _surface.PointToScreen(surfacePosition);
        }
    }

    public void RestoreDesktopNotePositions(object? sender, EventArgs e)
    {
        if (_screenPositionsBeforeDesktopChange is null)
        {
            return;
        }

        foreach (var (note, screenPosition) in _screenPositionsBeforeDesktopChange)
        {
            if (!_notes.Contains(note) || _floatingNoteWindows.ContainsKey(note))
            {
                continue;
            }

            var surfacePosition = _surface.PointFromScreen(screenPosition);
            var maximumLeft = Math.Max(0, _surface.ActualWidth - note.Width);
            var maximumTop = Math.Max(0, _surface.ActualHeight - note.Height);
            Canvas.SetLeft(note, Math.Clamp(surfacePosition.X, 0, maximumLeft));
            Canvas.SetTop(note, Math.Clamp(surfacePosition.Y, 0, maximumTop));
            UpdateDesktopNoteScale(note, screenPosition);
        }

        _screenPositionsBeforeDesktopChange = null;
        OnNotesChanged();
    }

    public void RemoveAllNotes()
    {
        foreach (var note in _notes.ToArray())
        {
            note.CancelDrag();
            note.CloseRequested -= OnCloseRequested;
            note.NoteChanged -= OnNoteChanged;
            note.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
            note.ForegroundRequested -= OnNoteForegroundRequested;
            note.DragRequested -= OnNoteDragRequested;
            note.DragCompleted -= OnNoteDragCompleted;
            _surface.Children.Remove(note);
            _notesBeingDragged.Remove(note);
            _deactivatedDuringDrag.Remove(note);

            if (_noteWindows.Remove(note, out var window))
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
            note.CancelDrag();
            note.CloseRequested -= OnCloseRequested;
            note.NoteChanged -= OnNoteChanged;
            note.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
            note.ForegroundRequested -= OnNoteForegroundRequested;
            note.DragRequested -= OnNoteDragRequested;
            note.DragCompleted -= OnNoteDragCompleted;
        }

        foreach (var (note, window) in _noteWindows.ToArray())
        {
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Content = null;
            window.Close();
            _noteWindows.Remove(note);
        }

        _floatingNoteWindows.Clear();
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
            Title = model.Title,
            NoteText = model.Text
        };
        note.SetAppearance(model.Appearance, notify: false);
        note.SetAlwaysOnTop(model.AlwaysOnTop, notify: false);

        note.CloseRequested += OnCloseRequested;
        note.NoteChanged += OnNoteChanged;
        note.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        note.ForegroundRequested += OnNoteForegroundRequested;
        note.DragRequested += OnNoteDragRequested;
        note.DragCompleted += OnNoteDragCompleted;
        _notes.Add(note);
        return note;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (sender is not NoteControl note)
        {
            return;
        }

        note.CancelDrag();
        note.CloseRequested -= OnCloseRequested;
        note.NoteChanged -= OnNoteChanged;
        note.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
        note.ForegroundRequested -= OnNoteForegroundRequested;
        note.DragRequested -= OnNoteDragRequested;
        note.DragCompleted -= OnNoteDragCompleted;
        _notes.Remove(note);
        _notesBeingDragged.Remove(note);
        _deactivatedDuringDrag.Remove(note);
        _surface.Children.Remove(note);

        if (_noteWindows.Remove(note, out var window))
        {
            _floatingNoteWindows.Remove(note);
            _noteWindows.Remove(note);
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Content = null;
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

        if (_notesBeingDragged.Contains(note))
        {
            _deactivatedDuringDrag.Add(note);
            return;
        }

        if (note.AlwaysOnTop)
        {
            return;
        }

        MoveToDesktopLayer(note, window);
    }

    private void OnFloatingNoteClosed(object? sender, EventArgs e)
    {
        if (sender is not Window { Content: NoteControl note })
        {
            return;
        }

        note.CancelDrag();
        note.CloseRequested -= OnCloseRequested;
        note.NoteChanged -= OnNoteChanged;
        note.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
        note.ForegroundRequested -= OnNoteForegroundRequested;
        note.DragRequested -= OnNoteDragRequested;
        note.DragCompleted -= OnNoteDragCompleted;
        _notes.Remove(note);
        _notesBeingDragged.Remove(note);
        _deactivatedDuringDrag.Remove(note);
        _floatingNoteWindows.Remove(note);
        _noteWindows.Remove(note);
        OnNotesChanged();
    }

    private void OnAlwaysOnTopChanged(object? sender, EventArgs e)
    {
        if (sender is not NoteControl note || !_notes.Contains(note))
        {
            return;
        }

        if (note.AlwaysOnTop)
        {
            if (_floatingNoteWindows.TryGetValue(note, out var window))
            {
                window.Topmost = true;
            }
            else
            {
                MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
            }
        }
        else if (_floatingNoteWindows.TryGetValue(note, out var window))
        {
            window.Topmost = false;
            window.Activate();
        }
    }

    private void OnNoteForegroundRequested(object? sender, EventArgs e)
    {
        if (sender is not NoteControl note || _floatingNoteWindows.ContainsKey(note))
        {
            return;
        }

        _surface.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_notes.Contains(note) && !_floatingNoteWindows.ContainsKey(note))
            {
                MoveToFloatingLayer(note, moveCaretToEnd: false);
            }
        }));
    }

    private void OnNoteDragRequested(NoteControl note)
    {
        _notesBeingDragged.Add(note);
        if (_notes.Contains(note) && !_floatingNoteWindows.ContainsKey(note))
        {
            MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
        }
    }

    private void OnNoteDragCompleted(NoteControl note)
    {
        _notesBeingDragged.Remove(note);
        if (_deactivatedDuringDrag.Remove(note) &&
            _floatingNoteWindows.TryGetValue(note, out var window) &&
            !window.IsActive)
        {
            window.Activate();
        }
    }

    private void MoveToFloatingLayer(NoteControl note, bool moveCaretToEnd, bool focusEditor = true)
    {
        if (_floatingNoteWindows.ContainsKey(note))
        {
            return;
        }

        var left = Canvas.GetLeft(note);
        var top = Canvas.GetTop(note);
        var surfacePosition = new WpfPoint(
            double.IsFinite(left) ? left : 0,
            double.IsFinite(top) ? top : 0);
        var screenPosition = _surface.PointToScreen(surfacePosition);
        _surface.Children.Remove(note);
        note.SetDesktopDpiScale(1);

        if (!_noteWindows.TryGetValue(note, out var window))
        {
            window = new Window
            {
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Left = 0,
                Top = 0,
                ShowInTaskbar = false,
                ShowActivated = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStyle = WindowStyle.None
            };
            _noteWindows.Add(note, window);
        }

        window.Content = note;

        _floatingNoteWindows.Add(note, window);
        window.Deactivated += OnFloatingNoteDeactivated;
        window.Closed += OnFloatingNoteClosed;
        var windowHandle = new WindowInteropHelper(window).EnsureHandle();
        if (!NativeMethods.SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                (int)Math.Round(screenPosition.X),
                (int)Math.Round(screenPosition.Y),
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder))
        {
            _floatingNoteWindows.Remove(note);
            window.Deactivated -= OnFloatingNoteDeactivated;
            window.Closed -= OnFloatingNoteClosed;
            window.Content = null;
            window.Close();
            Canvas.SetLeft(note, surfacePosition.X);
            Canvas.SetTop(note, surfacePosition.Y);
            _surface.Children.Add(note);
            UpdateDesktopNoteScale(note, screenPosition);
            throw new InvalidOperationException("Could not position the note in the foreground layer.");
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        window.Activate();
        if (focusEditor)
        {
            note.FocusEditor(moveCaretToEnd);
        }
        OnNotesChanged();
    }

    private void MoveToDesktopLayer(NoteControl note, Window window)
    {
        if (!_floatingNoteWindows.Remove(note))
        {
            return;
        }

        _deactivatedDuringDrag.Remove(note);
        window.Deactivated -= OnFloatingNoteDeactivated;
        window.Closed -= OnFloatingNoteClosed;
        var screenPosition = window.PointToScreen(new WpfPoint(0, 0));
        var surfacePosition = _surface.PointFromScreen(screenPosition);
        Canvas.SetLeft(note, surfacePosition.X);
        Canvas.SetTop(note, surfacePosition.Y);
        window.Content = null;
        _surface.Children.Add(note);
        UpdateDesktopNoteScale(note, screenPosition);
        window.Hide();
        OnNotesChanged();
    }

    private void OnNoteChanged(object? sender, EventArgs e)
    {
        if (sender is NoteControl note && !_floatingNoteWindows.ContainsKey(note))
        {
            UpdateDesktopNoteScale(note);
        }

        OnNotesChanged();
    }

    private void UpdateDesktopNoteScale(NoteControl note)
    {
        var left = Canvas.GetLeft(note);
        var top = Canvas.GetTop(note);
        var surfacePosition = new WpfPoint(
            double.IsFinite(left) ? left : 0,
            double.IsFinite(top) ? top : 0);
        UpdateDesktopNoteScale(note, _surface.PointToScreen(surfacePosition));
    }

    private void UpdateDesktopNoteScale(NoteControl note, WpfPoint screenPosition)
    {
        var surfaceDpiScale = VisualTreeHelper.GetDpi(_surface).DpiScaleX;
        var monitorDpiScale = NativeMethods.GetDpiScaleAt(
            (int)Math.Round(screenPosition.X),
            (int)Math.Round(screenPosition.Y));
        note.SetDesktopDpiScale(monitorDpiScale / surfaceDpiScale);
    }

    private void OnNotesChanged()
    {
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }
}