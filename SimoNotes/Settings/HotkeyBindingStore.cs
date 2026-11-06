using System.IO;
using System.Text.Json;

namespace SimoNotes.Settings;

internal sealed class HotkeyBindingStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimoNotes",
        "settings.json");

    public HotkeyBinding Load()
    {
        try
        {
            var binding = JsonSerializer.Deserialize<HotkeyBinding>(File.ReadAllText(_filePath));
            return binding is { IsValid: true } ? binding : HotkeyBinding.Default;
        }
        catch (IOException)
        {
            return HotkeyBinding.Default;
        }
        catch (JsonException)
        {
            return HotkeyBinding.Default;
        }
    }

    public void Save(HotkeyBinding binding)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(binding));
        File.Move(temporaryPath, _filePath, true);
    }
}