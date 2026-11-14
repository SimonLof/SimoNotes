namespace SimoNotes.Notes;

internal interface INotePersistence
{
    List<NoteModel>? Load();
    void Save(IEnumerable<NoteModel> notes);
}
