namespace SimoNotes.Notes;

internal sealed record NoteAppearance(string Color, double Opacity)
{
    public static NoteAppearance Default => new(NoteStyleCatalog.Note.DefaultColor, 1.0)
    {
        StyleId = NoteStyleCatalog.NoteId,
        CaretColor = NoteStyleCatalog.Note.CaretColor
    };

    public string StyleId { get; init; } = NoteStyleCatalog.NoteId;
    public string? CaretColor { get; init; }
    public string NormalizedStyleId => StyleId == NoteStyleCatalog.LegacyOrangeTerminalId
        ? NoteStyleCatalog.TerminalId
        : StyleId;

    public bool IsValid =>
        NoteStyleCatalog.All.Any(style => style.Id == NormalizedStyleId &&
            style.Colors.Any(color => color.Value == Color)) &&
        double.IsFinite(Opacity) && Opacity is >= 0.25 and <= 1.0;

    public NoteAppearance Normalize()
    {
        if (!IsValid)
        {
            return Default;
        }

        var style = NoteStyleCatalog.Get(NormalizedStyleId);
        return this with
        {
            StyleId = NormalizedStyleId,
            CaretColor = style.GetCaretColor(Color)
        };
    }
}