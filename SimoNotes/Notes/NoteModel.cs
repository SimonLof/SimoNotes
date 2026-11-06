namespace SimoNotes.Notes;

internal sealed class NoteModel
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string Text { get; init; } = string.Empty;
}