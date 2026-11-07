using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimoNotes.Notes;

internal sealed class NoteState : INotifyPropertyChanged
{
    private double _left;
    private double _top;
    private double _width;
    private double _height;
    private string _title;
    private string _text;
    private NoteAppearance _appearance;
    private bool _alwaysOnTop;

    public NoteState(NoteModel model)
    {
        _left = model.Left;
        _top = model.Top;
        _width = model.Width;
        _height = model.Height;
        _title = model.Title;
        _text = model.Text;
        _appearance = model.Appearance.Normalize();
        _alwaysOnTop = model.AlwaysOnTop;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Left { get => _left; set => SetField(ref _left, value); }
    public double Top { get => _top; set => SetField(ref _top, value); }
    public double Width { get => _width; set => SetField(ref _width, value); }
    public double Height { get => _height; set => SetField(ref _height, value); }
    public string Title { get => _title; set => SetField(ref _title, value); }
    public string Text { get => _text; set => SetField(ref _text, value); }
    public NoteAppearance Appearance { get => _appearance; set => SetField(ref _appearance, value.Normalize()); }
    public bool AlwaysOnTop { get => _alwaysOnTop; set => SetField(ref _alwaysOnTop, value); }

    public NoteModel ToModel() => new()
    {
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height,
        Title = Title,
        Text = Text,
        Appearance = Appearance,
        AlwaysOnTop = AlwaysOnTop
    };

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}