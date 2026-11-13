using System.Windows;

namespace SimoNotes.Notes;

public partial class NotesManagerWindow : Window
{
    private sealed record NoteListItem(NoteState State, string Title, string Preview, bool IsVisible)
    {
        public string VisibilityLabel => IsVisible ? "Visible" : "Hidden";
        public string ActionLabel => IsVisible ? "Open" : "Show";
    }

    private readonly NoteManager _noteManager;

    internal event EventHandler? CreateNoteRequested;

    internal NotesManagerWindow(NoteManager noteManager)
    {
        InitializeComponent();
        _noteManager = noteManager;
        _noteManager.NotesChanged += OnNotesChanged;
        Closed += OnClosed;
        RefreshNotes();
    }

    private void OnNotesChanged(object? sender, EventArgs e) => RefreshNotes();

    private void OnClosed(object? sender, EventArgs e)
    {
        _noteManager.NotesChanged -= OnNotesChanged;
        Closed -= OnClosed;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        RefreshNotes();

    private void OpenNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NoteListItem item })
        {
            _noteManager.BringNoteToFront(item.State);
        }
    }

    private void CreateNote_Click(object sender, RoutedEventArgs e) =>
        CreateNoteRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NoteListItem item })
        {
            _noteManager.RemoveNote(item.State);
        }
    }

    private void RefreshNotes()
    {
        var searchText = SearchBox?.Text.Trim();
        var notes = _noteManager.GetNoteStates()
            .Select(state => new NoteListItem(
                state,
                string.IsNullOrWhiteSpace(state.Title) ? "Untitled note" : state.Title,
                state.Text,
                state.IsVisible))
            .Where(item => string.IsNullOrEmpty(searchText) ||
                item.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.Preview.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        NotesList.ItemsSource = notes;
        EmptyResultsText.Visibility = notes.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteCountText.Text = $"{notes.Length} of {_noteManager.NoteCount} notes";
    }
}
