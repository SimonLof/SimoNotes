using SimoNotes.Notes;

using System.IO;
using System.Text.Json;

namespace SimoNotes.Settings;

internal sealed class ConfigPersistenceToFile : IConfigPersistence
{
    private const string HotkeyFileName = "settings.json";
    private const string NoteAppearanceFileName = "note-defaults.json";

    private readonly string _directoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimoNotes");

    public HotkeyBindings LoadHotkeyBindings()
    {
        try
        {
            var json = File.ReadAllText(GetFilePath(HotkeyFileName));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if(root.ValueKind != JsonValueKind.Object)
            {
                return HotkeyBindings.Default;
            }

            if(root.TryGetProperty(nameof(HotkeyBinding.Modifiers), out _))
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
        catch(IOException)
        {
            return HotkeyBindings.Default;
        }
        catch(JsonException)
        {
            return HotkeyBindings.Default;
        }
    }

    public void SaveHotkeyBindings(HotkeyBindings bindings) =>
        Save(HotkeyFileName, JsonSerializer.Serialize(bindings));

    public NoteAppearance LoadNoteAppearance()
    {
        try
        {
            var appearance = JsonSerializer.Deserialize<NoteAppearance>(
                File.ReadAllText(GetFilePath(NoteAppearanceFileName)));
            return appearance?.Normalize() ?? NoteAppearance.Default;
        }
        catch(IOException)
        {
            return NoteAppearance.Default;
        }
        catch(JsonException)
        {
            return NoteAppearance.Default;
        }
    }

    public void SaveNoteAppearance(NoteAppearance appearance) =>
        Save(NoteAppearanceFileName, JsonSerializer.Serialize(appearance));

    private void Save(string fileName, string contents)
    {
        Directory.CreateDirectory(_directoryPath);
        var filePath = GetFilePath(fileName);
        var temporaryPath = filePath + ".tmp";
        File.WriteAllText(temporaryPath, contents);
        File.Move(temporaryPath, filePath, true);
    }

    private string GetFilePath(string fileName) => Path.Combine(_directoryPath, fileName);
}
