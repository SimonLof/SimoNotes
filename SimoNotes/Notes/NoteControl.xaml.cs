using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using SimoNotes.Desktop;
using UserControl = System.Windows.Controls.UserControl;
using WpfPoint = System.Windows.Point;

namespace SimoNotes.Notes;

public partial class NoteControl : UserControl
{
    private NoteAppearance _appearance = NoteAppearance.Default;
    private bool _alwaysOnTop;
    private bool _updatingAppearance;
    private bool _isInitialized;
    private bool _applyingState;
    private bool _isInteractive = true;
    private bool _isDragging;
    private readonly DispatcherTimer _dragTimer = new(DispatcherPriority.Input)
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };
    private Window? _dragWindow;
    private IntPtr _dragWindowHandle;
    private WpfPoint _dragStartPointer;
    private WpfPoint _dragStartWindow;

    public NoteControl()
    {
        InitializeComponent();
        _dragTimer.Tick += DragTimer_Tick;
        StyleSelector.ItemsSource = NoteStyleCatalog.All;
        PreviewMouseDown += NoteControl_PreviewMouseDown;
        SetAppearance(_appearance, notify: false);
        _isInitialized = true;
    }

    public event EventHandler? CloseRequested;
    internal event Action<NoteControl, int?>? ForegroundRequested;
    internal event Func<NoteControl, Window?>? DragRequested;
    internal event Action<NoteControl>? DragCompleted;
    internal event Action<NoteControl>? ResizeStarted;
    internal event Action<NoteControl>? ResizeCompleted;
    internal NoteAppearance Appearance => _appearance;
    internal bool AlwaysOnTop => _alwaysOnTop;
    internal NoteState? State { get; private set; }
    internal bool HasKeyboardFocusWithin => IsKeyboardFocusWithin;

    internal void AttachState(NoteState state)
    {
        if (State is not null)
        {
            State.PropertyChanged -= OnStatePropertyChanged;
        }

        State = state;
        State.PropertyChanged += OnStatePropertyChanged;
        ApplyState();
    }

    internal void DetachState()
    {
        if (State is not null)
        {
            State.PropertyChanged -= OnStatePropertyChanged;
            State = null;
        }
    }

    internal void SetInteractive(bool isInteractive)
    {
        if (_isInteractive == isInteractive)
        {
            return;
        }

        if (!isInteractive)
        {
            AppearancePopup.IsOpen = false;
        }

        _isInteractive = isInteractive;
        IsHitTestVisible = isInteractive;
        Focusable = isInteractive;
        Editor.IsTabStop = isInteractive;
        NoteTitle.IsTabStop = isInteractive;
    }

    internal (int CaretIndex, int SelectionStart, int SelectionLength) CaptureEditorSelection() =>
        (Editor.CaretIndex, Editor.SelectionStart, Editor.SelectionLength);

    internal void RestoreEditorSelection((int CaretIndex, int SelectionStart, int SelectionLength) selection)
    {
        Editor.SelectionStart = Math.Clamp(selection.SelectionStart, 0, Editor.Text.Length);
        Editor.SelectionLength = Math.Clamp(selection.SelectionLength, 0, Editor.Text.Length - Editor.SelectionStart);
        Editor.CaretIndex = Math.Clamp(selection.CaretIndex, 0, Editor.Text.Length);
    }

    internal void SetDesktopDpiScale(double scale)
    {
        RenderTransform = Math.Abs(scale - 1) < 0.001
            ? Transform.Identity
            : new ScaleTransform(scale, scale);
    }

    public string NoteText
    {
        get => Editor.Text;
        set => Editor.Text = value;
    }

    public string Title
    {
        get => NoteTitle.Text;
        set => NoteTitle.Text = value;
    }

    public void FocusEditor(bool moveCaretToEnd = true)
    {
        Editor.Focus();
        if (moveCaretToEnd)
        {
            Editor.CaretIndex = Editor.Text.Length;
        }
    }

    internal void FocusTitleAt(int caretIndex)
    {
        NoteTitle.Focus();
        NoteTitle.SelectionStart = Math.Clamp(caretIndex, 0, NoteTitle.Text.Length);
        NoteTitle.SelectionLength = 0;
        NoteTitle.CaretIndex = NoteTitle.SelectionStart;
    }

    internal void SetAlwaysOnTop(bool alwaysOnTop, bool notify = true)
    {
        if (notify && _isInteractive && State is not null)
        {
            State.AlwaysOnTop = alwaysOnTop;
            return;
        }

        if (_alwaysOnTop == alwaysOnTop)
        {
            return;
        }

        _alwaysOnTop = alwaysOnTop;
        AlwaysOnTopCheckBox.IsChecked = alwaysOnTop;
    }

    internal void SetAppearance(NoteAppearance appearance, bool notify = true)
    {
        appearance = appearance.Normalize();
        if (notify && _isInteractive && State is not null)
        {
            State.Appearance = appearance;
            return;
        }

        _appearance = appearance;
        var style = NoteStyleCatalog.Get(appearance.StyleId);
        var fontFamily = style.CreateFontFamily();
        var backgroundColor = ParseColor(style.GetBackground(appearance.Color));
        var foregroundColor = ParseColor(style.GetForeground(appearance.Color));
        var borderColor = style.BorderColor is not null
            ? ParseColor(style.BorderColor)
            : style.ColorTarget == NoteStyleColorTarget.Foreground
                ? foregroundColor
                : System.Windows.Media.Color.FromArgb(
                backgroundColor.A,
                (byte)(backgroundColor.R * 0.68),
                (byte)(backgroundColor.G * 0.68),
                (byte)(backgroundColor.B * 0.68));

        NoteBorder.Background = new SolidColorBrush(backgroundColor);
        NoteBorder.BorderBrush = new SolidColorBrush(borderColor);
        NoteBorder.CornerRadius = new CornerRadius(style.BorderRadius);
        NoteBorder.Opacity = appearance.Opacity;
        AppearanceSwatch.Background = new SolidColorBrush(
            style.ColorTarget == NoteStyleColorTarget.Foreground ? foregroundColor : backgroundColor);
        NoteTitle.Foreground = new SolidColorBrush(foregroundColor);
        NoteTitle.FontFamily = fontFamily;
        NoteTitle.FontSize = style.FontSize;
        CloseButton.Foreground = new SolidColorBrush(foregroundColor);
        Editor.Foreground = new SolidColorBrush(foregroundColor);
        Editor.FontFamily = fontFamily;
        Editor.FontSize = style.FontSize;

        _updatingAppearance = true;
        StyleSelector.SelectedValue = style.Id;
        NoteStyleCatalog.PopulateSwatches(ColorSwatchGrid, style, appearance.Color, color =>
            SetAppearance(_appearance with { Color = color }));
        OpacitySlider.Value = appearance.Opacity;
        OpacityValueText.Text = $"{Math.Round(appearance.Opacity * 100)}%";
        _updatingAppearance = false;

    }

    private void ApplyState(string? propertyName = null)
    {
        if (State is null)
        {
            return;
        }

        _applyingState = true;
        if (propertyName is null or nameof(NoteState.Width))
        {
            Width = State.Width;
        }

        if (propertyName is null or nameof(NoteState.Height))
        {
            Height = State.Height;
        }

        if (propertyName is null or nameof(NoteState.Title) && NoteTitle.Text != State.Title)
        {
            NoteTitle.Text = State.Title;
        }

        if (propertyName is null or nameof(NoteState.Text) && Editor.Text != State.Text)
        {
            Editor.Text = State.Text;
        }

        if (propertyName is null or nameof(NoteState.Appearance))
        {
            SetAppearance(State.Appearance, notify: false);
        }

        if (propertyName is null or nameof(NoteState.AlwaysOnTop))
        {
            SetAlwaysOnTop(State.AlwaysOnTop, notify: false);
        }

        _applyingState = false;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoteState.Width) or nameof(NoteState.Height) or
            nameof(NoteState.Title) or nameof(NoteState.Text) or nameof(NoteState.Appearance) or
            nameof(NoteState.AlwaysOnTop))
        {
            ApplyState(e.PropertyName);
        }
    }

    private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (!_isInteractive)
        {
            return;
        }

        e.Handled = true;
        var dragStartPointer = DragSurface.PointToScreen(e.GetPosition(DragSurface));
        var dragWindow = DragRequested?.Invoke(this);

        var window = dragWindow ?? Window.GetWindow(this);
        if (window is null ||
            !NativeMethods.GetWindowRect(new WindowInteropHelper(window).Handle, out var windowBounds))
        {
            return;
        }

        _dragWindow = window;
        _dragWindowHandle = new WindowInteropHelper(window).Handle;
        _dragStartPointer = dragStartPointer;
        _dragStartWindow = new WpfPoint(windowBounds.Left, windowBounds.Top);
        _isDragging = true;
        _dragTimer.Start();
    }

    private void DragTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging || _dragWindow is null)
        {
            _dragTimer.Stop();
            return;
        }

        if (Keyboard.IsKeyDown(Key.Escape) || !NativeMethods.IsLeftMouseButtonDown())
        {
            FinishDrag();
            return;
        }

        if (!NativeMethods.GetCursorPos(out var cursorPosition))
        {
            return;
        }

        var pointer = new WpfPoint(cursorPosition.X, cursorPosition.Y);
        var left = _dragStartWindow.X + pointer.X - _dragStartPointer.X;
        var top = _dragStartWindow.Y + pointer.Y - _dragStartPointer.Y;
        NativeMethods.SetWindowPos(
            _dragWindowHandle,
            IntPtr.Zero,
            (int)Math.Round(left),
            (int)Math.Round(top),
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder);
    }

    private void DragSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && e.ChangedButton == MouseButton.Left)
        {
            FinishDrag();
            e.Handled = true;
        }
    }

    private void FinishDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragTimer.Stop();
        _dragWindow = null;
        _dragWindowHandle = IntPtr.Zero;
        DragCompleted?.Invoke(this);
    }

    internal void CancelDrag()
    {
        _isDragging = false;
        _dragTimer.Stop();
        _dragWindow = null;
        _dragWindowHandle = IntPtr.Zero;
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isInteractive)
        {
            return;
        }

        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
        if (State is not null)
        {
            State.Width = Width;
            State.Height = Height;
        }

    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (_isInteractive)
        {
            ResizeStarted?.Invoke(this);
        }
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isInteractive)
        {
            ResizeCompleted?.Invoke(this);
        }
    }

    private void NoteControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isInteractive &&
            !ResizeThumb.IsMouseOver &&
            !CloseButton.IsMouseOver &&
            !AppearanceButton.IsMouseOver &&
            !AppearancePopup.IsOpen)
        {
            int? titleCaretIndex = NoteTitle.IsMouseOver
                ? NoteTitle.GetCharacterIndexFromPoint(e.GetPosition(NoteTitle), snapToText: true)
                : null;
            ForegroundRequested?.Invoke(this, titleCaretIndex);
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applyingState || !_isInteractive)
        {
            return;
        }

        if (State is not null)
        {
            State.Text = Editor.Text;
        }

    }

    private void Title_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applyingState || !_isInteractive)
        {
            return;
        }

        if (State is not null)
        {
            State.Title = NoteTitle.Text;
        }

    }

    private void AppearanceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInteractive)
        {
            AppearancePopup.IsOpen = !AppearancePopup.IsOpen;
        }
    }

    private void StyleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized && _isInteractive && !_updatingAppearance && StyleSelector.SelectedValue is string styleId)
        {
            var style = NoteStyleCatalog.Get(styleId);
            SetAppearance(_appearance with
            {
                StyleId = style.Id,
                Color = style.DefaultColor
            });
        }
    }

    private static System.Windows.Media.Color ParseColor(string color) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInteractive || _updatingAppearance || OpacityValueText is null)
        {
            return;
        }

        OpacityValueText.Text = $"{Math.Round(e.NewValue * 100)}%";
        SetAppearance(_appearance with { Opacity = e.NewValue });
    }

    private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInteractive)
        {
            SetAlwaysOnTop(AlwaysOnTopCheckBox.IsChecked == true);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInteractive)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}