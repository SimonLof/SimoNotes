using System.Windows;
using System.Windows.Controls;

using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUniformGrid = System.Windows.Controls.Primitives.UniformGrid;

namespace SimoNotes.Notes;

internal enum NoteStyleColorTarget
{
    Background,
    Foreground
}

internal sealed record NoteStyleColor(string Name, string Value);

internal sealed record NoteStyleDefinition(
    string Id,
    string Name,
    string BackgroundColor,
    string? BorderColor,
    string ForegroundColor,
    string CaretColor,
    string FontFamilyName,
    string? FontFamilyResource,
    double FontSize,
    double BorderRadius,
    NoteStyleColorTarget ColorTarget,
    IReadOnlyList<NoteStyleColor> Colors)
{
    public string DefaultColor => Colors[0].Value;

    public string GetBackground(string selectedColor) =>
        ColorTarget == NoteStyleColorTarget.Background ? selectedColor : BackgroundColor;

    public string GetForeground(string selectedColor) =>
        ColorTarget == NoteStyleColorTarget.Foreground ? selectedColor : ForegroundColor;

    public string GetCaretColor(string selectedColor) =>
        ColorTarget == NoteStyleColorTarget.Foreground ? selectedColor : CaretColor;

    public WpfFontFamily CreateFontFamily() => FontFamilyResource is null
        ? new WpfFontFamily(FontFamilyName)
        : new WpfFontFamily(
            new Uri("pack://application:,,,/SimoNotes;component/"),
            FontFamilyResource);
}

internal static class NoteStyleCatalog
{
    public const string NoteId = "note";
    public const string TerminalId = "terminal";
    public const string LegacyOrangeTerminalId = "terminal-orange";

    private static readonly IReadOnlyList<NoteStyleColor> NoteColors =
    [
        new("Yellow", "#FFF4B942"),
        new("Rose", "#FFF3A6A6"),
        new("Mint", "#FFA8D5BA"),
        new("Sky", "#FFA7D8F0"),
        new("Lilac", "#FFC5B4E2")
    ];

    private static readonly IReadOnlyList<NoteStyleColor> TerminalColors =
    [
        new("Green", "#FF65FF80"),
        new("Blue", "#FF58B8FF"),
        new("Orange", "#FFFF9A3D"),
        new("Amber", "#FFFFC247")
    ];

    public static NoteStyleDefinition Note { get; } = new(
        NoteId,
        "Note",
        "#FFF4B942",
        null,
        "#FF30291F",
        "#FF30291F",
        "Segoe UI",
        null,
        15,
        3,
        NoteStyleColorTarget.Background,
        NoteColors);

    public static NoteStyleDefinition Terminal { get; } = new(
        TerminalId,
        "Terminal",
        "#FF0D120F",
        null,
        "#FF65FF80",
        "#FFFFFFFF",
        "Press Start 2P",
        "./Assets/#Press Start 2P",
        9,
        0,
        NoteStyleColorTarget.Foreground,
        TerminalColors);

    public static IReadOnlyList<NoteStyleDefinition> All { get; } = [Note, Terminal];

    public static NoteStyleDefinition Get(string? styleId) =>
        All.FirstOrDefault(style => style.Id == styleId ||
            style.Id == TerminalId && styleId == LegacyOrangeTerminalId) ?? Note;

    public static void PopulateSwatches(
        WpfUniformGrid grid,
        NoteStyleDefinition style,
        string selectedColor,
        Action<string> onSelected)
    {
        grid.Children.Clear();
        grid.Columns = style.Colors.Count;

        foreach(var color in style.Colors)
        {
            var selected = color.Value == selectedColor;
            var swatch = new Border
            {
                Width = 34,
                Height = 24,
                Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(color.Value)),
                BorderBrush = System.Windows.Media.Brushes.Black,
                BorderThickness = new Thickness(selected ? 2 : 1),
                CornerRadius = new CornerRadius(2)
            };
            var button = new WpfButton
            {
                Width = 42,
                Height = 32,
                Margin = new Thickness(1),
                Padding = new Thickness(2),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = swatch,
                ToolTip = color.Name
            };

            button.Click += (_, _) => onSelected(color.Value);
            grid.Children.Add(button);
        }
    }
}