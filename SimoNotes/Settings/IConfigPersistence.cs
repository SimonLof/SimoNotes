using SimoNotes.Notes;

namespace SimoNotes.Settings;

internal interface IConfigPersistence
{
    HotkeyBindings LoadHotkeyBindings();
    void SaveHotkeyBindings(HotkeyBindings bindings);
    NoteAppearance LoadNoteAppearance();
    void SaveNoteAppearance(NoteAppearance appearance);
}
