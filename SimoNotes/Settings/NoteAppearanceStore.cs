using System.IO;
using System.Text.Json;
using SimoNotes.Notes;

namespace SimoNotes.Settings;

internal sealed class NoteAppearanceStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimoNotes",
        "note-defaults.json");

    public NoteAppearance Load()
    {
        try
        {
            var appearance = JsonSerializer.Deserialize<NoteAppearance>(File.ReadAllText(_filePath));
            return appearance?.Normalize() ?? NoteAppearance.Default;
        }
        catch (IOException)
        {
            return NoteAppearance.Default;
        }
        catch (JsonException)
        {
            return NoteAppearance.Default;
        }
    }

    public void Save(NoteAppearance appearance)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(appearance));
        File.Move(temporaryPath, _filePath, true);
    }
}