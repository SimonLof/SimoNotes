using System.IO;
using System.Text.Json;

namespace SimoNotes.Notes;

internal sealed class NoteStateStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimoNotes",
        "notes.json");

    public List<NoteModel>? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<NoteModel>>(File.ReadAllText(_filePath)) ?? [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<NoteModel> notes)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(notes));
        File.Move(temporaryPath, _filePath, true);
    }
}