using System.IO;
using System.Text.Json;

namespace SimoNotes.Settings;

internal sealed record HotkeyBindings(HotkeyBinding NewNote, HotkeyBinding BringAllNotesToFront)
{
    public static HotkeyBindings Default => new(
        HotkeyBinding.Default,
        HotkeyBinding.BringAllNotesToFrontDefault);
}

internal sealed class HotkeyBindingStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimoNotes",
        "settings.json");

    public HotkeyBindings Load()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HotkeyBindings.Default;
            }

            if (root.TryGetProperty(nameof(HotkeyBinding.Modifiers), out _))
            {
                var legacyBinding = JsonSerializer.Deserialize<HotkeyBinding>(json);
                return new HotkeyBindings(
                    legacyBinding is { IsValid: true } ? legacyBinding : HotkeyBinding.Default,
                    HotkeyBinding.BringAllNotesToFrontDefault);
            }

            var bindings = JsonSerializer.Deserialize<HotkeyBindings>(json);
            return new HotkeyBindings(
                bindings?.NewNote is { IsValid: true } newNote ? newNote : HotkeyBinding.Default,
                bindings?.BringAllNotesToFront is { IsValid: true } bringAllNotes
                    ? bringAllNotes
                    : HotkeyBinding.BringAllNotesToFrontDefault);
        }
        catch (IOException)
        {
            return HotkeyBindings.Default;
        }
        catch (JsonException)
        {
            return HotkeyBindings.Default;
        }
    }

    public void Save(HotkeyBindings bindings)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(bindings));
        File.Move(temporaryPath, _filePath, true);
    }
}