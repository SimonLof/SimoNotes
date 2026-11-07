namespace SimoNotes.Notes;

internal sealed record NoteAppearance(string Color, double Opacity)
{
    public static NoteAppearance Default => new("#FFF4B942", 1.0);

    public static IReadOnlyList<(string Name, string Value)> Colors { get; } =
    [
        ("Yellow", "#FFF4B942"),
        ("Rose", "#FFF3A6A6"),
        ("Mint", "#FFA8D5BA"),
        ("Sky", "#FFA7D8F0"),
        ("Lilac", "#FFC5B4E2")
    ];

    public bool IsValid =>
        Colors.Any(option => option.Value == Color) &&
        double.IsFinite(Opacity) && Opacity is >= 0.25 and <= 1.0;
}