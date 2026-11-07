using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SimoNotes.Desktop;
using WpfPoint = System.Windows.Point;

namespace SimoNotes.Notes;

internal sealed class NoteManager : IDisposable
{
    private sealed class ManagedNote
    {
        public ManagedNote(NoteModel model)
        {
            State = new NoteState(model);
            DesktopView = new NoteControl();
            FloatingView = new NoteControl();
            Window = new Window
            {
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Left = 0,
                Top = 0,
                ShowInTaskbar = false,
                ShowActivated = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                Content = FloatingView
            };
        }

        public NoteState State { get; }
        public NoteControl DesktopView { get; }
        public NoteControl FloatingView { get; }
        public Window Window { get; }
        public NoteControl ActiveView { get; set; } = null!;
        public bool IsFloating => ActiveView == FloatingView;
    }

    private readonly Canvas _surface;
    private readonly List<ManagedNote> _notes = [];
    private readonly Dictionary<NoteControl, ManagedNote> _managedByView = [];
    private readonly HashSet<ManagedNote> _notesBeingDragged = [];
    private readonly HashSet<ManagedNote> _notesBeingResized = [];
    private readonly HashSet<ManagedNote> _deactivatedDuringManipulation = [];
    private bool _temporaryAllNotesToFront;
    private bool _bringingAllNotesToFront;
    private Dictionary<ManagedNote, WpfPoint>? _screenPositionsBeforeDesktopChange;

    public NoteManager(Canvas surface)
    {
        _surface = surface;
    }

    public event EventHandler? NotesChanged;

    public WpfPoint ScreenToSurface(WpfPoint screenPoint) => _surface.PointFromScreen(screenPoint);

    public void BringAllNotesToFront()
    {
        var notesToBringForward = _notes
            .Where(note => !note.State.AlwaysOnTop && !note.IsFloating)
            .ToArray();
        if (notesToBringForward.Length == 0)
        {
            return;
        }

        _temporaryAllNotesToFront = true;
        _bringingAllNotesToFront = true;
        try
        {
            foreach (var note in notesToBringForward)
            {
                MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false, keepDesktopViewVisible: true);
            }
        }
        finally
        {
            _bringingAllNotesToFront = false;
        }
    }

    public void CreateNote(NoteModel model)
    {
        var note = CreateManagedNote(model);
        if (note.State.AlwaysOnTop)
        {
            MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
        }

        OnNotesChanged();
    }

    public void CreateFloatingNote(NoteModel model)
    {
        var note = CreateManagedNote(model);
        MoveToFloatingLayer(note, moveCaretToEnd: true);
    }

    public List<NoteModel> GetNoteModels()
    {
        foreach (var note in _notes.Where(note => note.IsFloating))
        {
            UpdateFloatingPosition(note);
        }

        return _notes.Select(note => note.State.ToModel()).ToList();
    }

    public void CaptureDesktopNotePositions(object? sender, EventArgs e)
    {
        _screenPositionsBeforeDesktopChange = [];
        foreach (var note in _notes.Where(note => !note.IsFloating))
        {
            _screenPositionsBeforeDesktopChange[note] = _surface.PointToScreen(GetSurfacePosition(note));
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
            if (!_notes.Contains(note) || note.IsFloating)
            {
                continue;
            }

            var surfacePosition = _surface.PointFromScreen(screenPosition);
            note.State.Left = Math.Clamp(surfacePosition.X, 0, Math.Max(0, _surface.ActualWidth - note.State.Width));
            note.State.Top = Math.Clamp(surfacePosition.Y, 0, Math.Max(0, _surface.ActualHeight - note.State.Height));
            UpdateDesktopNoteScale(note, screenPosition);
        }

        _screenPositionsBeforeDesktopChange = null;
        OnNotesChanged();
    }

    public void RemoveAllNotes()
    {
        foreach (var note in _notes.ToArray())
        {
            RemoveManagedNote(note, closeWindow: true);
        }

        _surface.Children.Clear();
        OnNotesChanged();
    }

    public void Dispose()
    {
        foreach (var note in _notes.ToArray())
        {
            RemoveManagedNote(note, closeWindow: true);
        }

        _surface.Children.Clear();
    }

    private ManagedNote CreateManagedNote(NoteModel model)
    {
        var note = new ManagedNote(model);
        note.State.PropertyChanged += OnNoteStateChanged;
        note.DesktopView.AttachState(note.State);
        note.FloatingView.AttachState(note.State);
        note.ActiveView = note.DesktopView;
        note.FloatingView.Visibility = Visibility.Hidden;
        note.FloatingView.SetInteractive(false);
        AttachView(note.DesktopView);
        AttachView(note.FloatingView);
        note.Window.Deactivated += OnFloatingNoteDeactivated;
        note.Window.Closed += OnFloatingNoteClosed;
        _notes.Add(note);
        _managedByView.Add(note.DesktopView, note);
        _managedByView.Add(note.FloatingView, note);

        Canvas.SetLeft(note.DesktopView, note.State.Left);
        Canvas.SetTop(note.DesktopView, note.State.Top);
        _surface.Children.Add(note.DesktopView);
        UpdateDesktopNoteScale(note);
        return note;
    }

    private void AttachView(NoteControl view)
    {
        view.CloseRequested += OnCloseRequested;
        view.ForegroundRequested += OnNoteForegroundRequested;
        view.DragRequested += OnNoteDragRequested;
        view.DragCompleted += OnNoteDragCompleted;
        view.ResizeStarted += OnNoteResizeStarted;
        view.ResizeCompleted += OnNoteResizeCompleted;
    }

    private void DetachView(NoteControl view)
    {
        view.CancelDrag();
        view.CloseRequested -= OnCloseRequested;
        view.ForegroundRequested -= OnNoteForegroundRequested;
        view.DragRequested -= OnNoteDragRequested;
        view.DragCompleted -= OnNoteDragCompleted;
        view.ResizeStarted -= OnNoteResizeStarted;
        view.ResizeCompleted -= OnNoteResizeCompleted;
        view.SetInteractive(false);
        view.DetachState();
    }

    private void RemoveManagedNote(ManagedNote note, bool closeWindow)
    {
        if (!_notes.Remove(note))
        {
            return;
        }

        note.State.PropertyChanged -= OnNoteStateChanged;
        DetachView(note.DesktopView);
        DetachView(note.FloatingView);
        _managedByView.Remove(note.DesktopView);
        _managedByView.Remove(note.FloatingView);
        _notesBeingDragged.Remove(note);
        _notesBeingResized.Remove(note);
        _deactivatedDuringManipulation.Remove(note);
        _surface.Children.Remove(note.DesktopView);
        note.Window.Deactivated -= OnFloatingNoteDeactivated;
        note.Window.Closed -= OnFloatingNoteClosed;
        note.Window.Content = null;
        if (closeWindow)
        {
            note.Window.Close();
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (sender is NoteControl view &&
            _managedByView.TryGetValue(view, out var note) &&
            note.ActiveView == view)
        {
            RemoveManagedNote(note, closeWindow: true);
            OnNotesChanged();
        }
    }

    private void OnFloatingNoteDeactivated(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var note = _notes.FirstOrDefault(candidate => candidate.Window == window && candidate.IsFloating);
        if (note is null)
        {
            return;
        }

        if (_notesBeingDragged.Contains(note) || _notesBeingResized.Contains(note))
        {
            _deactivatedDuringManipulation.Add(note);
            return;
        }

        if (_temporaryAllNotesToFront && !note.State.AlwaysOnTop)
        {
            ScheduleTemporaryLayerReconciliation();
            return;
        }

        if (!note.State.AlwaysOnTop)
        {
            MoveToDesktopLayer(note);
        }
    }

    private void OnFloatingNoteClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            var note = _notes.FirstOrDefault(candidate => candidate.Window == window);
            if (note is not null)
            {
                RemoveManagedNote(note, closeWindow: false);
                OnNotesChanged();
            }
        }
    }

    private void OnNoteStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not NoteState state)
        {
            return;
        }

        var note = _notes.FirstOrDefault(candidate => candidate.State == state);
        if (note is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NoteState.Left):
                Canvas.SetLeft(note.DesktopView, state.Left);
                if (!note.IsFloating)
                {
                    UpdateDesktopNoteScale(note);
                }
                break;
            case nameof(NoteState.Top):
                Canvas.SetTop(note.DesktopView, state.Top);
                if (!note.IsFloating)
                {
                    UpdateDesktopNoteScale(note);
                }
                break;
            case nameof(NoteState.AlwaysOnTop):
                UpdateAlwaysOnTop(note);
                break;
        }

        OnNotesChanged();
    }

    private void UpdateAlwaysOnTop(ManagedNote note)
    {
        if (note.State.AlwaysOnTop)
        {
            if (note.IsFloating)
            {
                note.Window.Topmost = true;
            }
            else
            {
                MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
            }
        }
        else if (note.IsFloating)
        {
            note.Window.Topmost = false;
            note.Window.Activate();
        }
    }

    private void OnNoteForegroundRequested(NoteControl view, int? titleCaretIndex)
    {
        if (!_managedByView.TryGetValue(view, out var note) ||
            note.ActiveView != view)
        {
            return;
        }

        if (_temporaryAllNotesToFront)
        {
            ScheduleTemporaryLayerReconciliation();
            return;
        }

        _surface.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_notes.Contains(note) && !note.IsFloating)
            {
                MoveToFloatingLayer(
                    note,
                    moveCaretToEnd: false,
                    focusEditor: false,
                    keepDesktopViewVisible: true,
                    focusTitleAt: titleCaretIndex);
            }
        }));
    }

    private void ScheduleTemporaryLayerReconciliation()
    {
        if (!_temporaryAllNotesToFront || _bringingAllNotesToFront)
        {
            return;
        }

        _surface.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (!_temporaryAllNotesToFront || _bringingAllNotesToFront)
            {
                return;
            }

            var focusedNote = _notes
                .FirstOrDefault(note => note.IsFloating && note.Window.IsActive && !note.State.AlwaysOnTop);
            foreach (var note in _notes.Where(note => note.IsFloating).ToArray())
            {
                if (!note.State.AlwaysOnTop && note != focusedNote)
                {
                    MoveToDesktopLayer(note);
                }
            }

            if (focusedNote is null)
            {
                _temporaryAllNotesToFront = false;
            }
        }));
    }

    private Window? OnNoteDragRequested(NoteControl view)
    {
        if (!_managedByView.TryGetValue(view, out var note) || note.ActiveView != view)
        {
            return null;
        }

        _notesBeingDragged.Add(note);
        if (!note.IsFloating)
        {
            MoveToFloatingLayer(note, moveCaretToEnd: false, focusEditor: false);
        }
        else
        {
            note.DesktopView.Visibility = Visibility.Hidden;
        }

        return note.Window;
    }

    private void OnNoteDragCompleted(NoteControl view)
    {
        if (!_managedByView.TryGetValue(view, out var note) || !_notesBeingDragged.Remove(note))
        {
            return;
        }

        if (note.IsFloating)
        {
            UpdateFloatingPosition(note);
            UpdateDesktopNoteScale(note);
            note.DesktopView.Visibility = Visibility.Visible;
            _surface.UpdateLayout();
        }

        if (_deactivatedDuringManipulation.Remove(note) && !note.Window.IsActive)
        {
            note.Window.Activate();
        }

        RedrawDesktopHost();
    }

    private void OnNoteResizeStarted(NoteControl view)
    {
        if (!_managedByView.TryGetValue(view, out var note) ||
            note.ActiveView != view ||
            !note.IsFloating)
        {
            return;
        }

        _notesBeingResized.Add(note);
        note.DesktopView.Visibility = Visibility.Hidden;
    }

    private void OnNoteResizeCompleted(NoteControl view)
    {
        if (!_managedByView.TryGetValue(view, out var note) ||
            note.ActiveView != view ||
            !_notesBeingResized.Remove(note))
        {
            return;
        }

        if (note.IsFloating)
        {
            UpdateDesktopNoteScale(note);
            note.DesktopView.Visibility = Visibility.Visible;
            _surface.UpdateLayout();
        }

        if (_deactivatedDuringManipulation.Remove(note) && !note.Window.IsActive)
        {
            note.Window.Activate();
        }

        RedrawDesktopHost();
    }

    private void RedrawDesktopHost()
    {
        _surface.InvalidateVisual();
        if (PresentationSource.FromVisual(_surface) is HwndSource source)
        {
            NativeMethods.RedrawWindow(
                source.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.RdwInvalidate | NativeMethods.RdwAllChildren | NativeMethods.RdwUpdateNow);
        }
    }

    private void MoveToFloatingLayer(
        ManagedNote note,
        bool moveCaretToEnd,
        bool focusEditor = true,
        bool keepDesktopViewVisible = false,
        int? focusTitleAt = null)
    {
        if (note.IsFloating)
        {
            return;
        }

        var screenPosition = _surface.PointToScreen(GetSurfacePosition(note));
        var windowHandle = new WindowInteropHelper(note.Window).EnsureHandle();
        PositionWindow(windowHandle, screenPosition);
        note.FloatingView.Visibility = Visibility.Visible;
        note.Window.UpdateLayout();
        if (!note.Window.IsVisible)
        {
            note.Window.Show();
        }

        note.Window.UpdateLayout();
        PositionWindow(windowHandle, screenPosition);
        note.Window.Activate();
        SetActiveView(note, note.FloatingView, focusEditor, moveCaretToEnd, keepDesktopViewVisible, focusTitleAt);
        OnNotesChanged();
    }

    private void MoveToDesktopLayer(ManagedNote note)
    {
        if (!note.IsFloating)
        {
            return;
        }

        var screenPosition = note.Window.PointToScreen(new WpfPoint(0, 0));
        var surfacePosition = _surface.PointFromScreen(screenPosition);
        note.State.Left = surfacePosition.X;
        note.State.Top = surfacePosition.Y;
        UpdateDesktopNoteScale(note, screenPosition);
        SetActiveView(note, note.DesktopView, focusEditor: false, moveCaretToEnd: false);
        note.Window.Hide();
        _deactivatedDuringManipulation.Remove(note);
        OnNotesChanged();
    }

    private void SetActiveView(
        ManagedNote note,
        NoteControl nextView,
        bool focusEditor,
        bool moveCaretToEnd,
        bool keepPreviousVisible = false,
        int? focusTitleAt = null)
    {
        var previousView = note.ActiveView;
        if (previousView == nextView)
        {
            if (focusEditor)
            {
                nextView.FocusEditor(moveCaretToEnd);
            }
            return;
        }

        var hadKeyboardFocus = previousView.HasKeyboardFocusWithin;
        var selection = previousView.CaptureEditorSelection();
        nextView.Visibility = Visibility.Visible;
        if (nextView == note.DesktopView)
        {
            _surface.UpdateLayout();
        }
        else
        {
            note.Window.UpdateLayout();
        }

        previousView.SetInteractive(false);
        note.ActiveView = nextView;
        nextView.SetInteractive(true);
        nextView.RestoreEditorSelection(selection);
        if (hadKeyboardFocus && !focusEditor)
        {
            Keyboard.ClearFocus();
        }

        if (focusEditor)
        {
            nextView.FocusEditor(moveCaretToEnd);
        }
        else if (focusTitleAt is int caretIndex)
        {
            nextView.FocusTitleAt(caretIndex);
        }

        if (!keepPreviousVisible)
        {
            previousView.Visibility = Visibility.Hidden;
        }
    }

    private static void PositionWindow(IntPtr windowHandle, WpfPoint screenPosition)
    {
        if (!NativeMethods.SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                (int)Math.Round(screenPosition.X),
                (int)Math.Round(screenPosition.Y),
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder))
        {
            throw new InvalidOperationException("Could not position the note in the foreground layer.");
        }
    }

    private void UpdateFloatingPosition(ManagedNote note)
    {
        var surfacePosition = _surface.PointFromScreen(note.Window.PointToScreen(new WpfPoint(0, 0)));
        note.State.Left = surfacePosition.X;
        note.State.Top = surfacePosition.Y;
    }

    private WpfPoint GetSurfacePosition(ManagedNote note) => new(
        double.IsFinite(note.State.Left) ? note.State.Left : 0,
        double.IsFinite(note.State.Top) ? note.State.Top : 0);

    private void UpdateDesktopNoteScale(ManagedNote note) =>
        UpdateDesktopNoteScale(note, _surface.PointToScreen(GetSurfacePosition(note)));

    private void UpdateDesktopNoteScale(ManagedNote note, WpfPoint screenPosition)
    {
        var surfaceDpiScale = VisualTreeHelper.GetDpi(_surface).DpiScaleX;
        var monitorDpiScale = NativeMethods.GetDpiScaleAt(
            (int)Math.Round(screenPosition.X),
            (int)Math.Round(screenPosition.Y));
        note.DesktopView.SetDesktopDpiScale(monitorDpiScale / surfaceDpiScale);
    }

    private void OnNotesChanged()
    {
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }
}