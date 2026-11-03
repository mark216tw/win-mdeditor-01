using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace VibeCode.MdEditor.Controls;

public sealed partial class SplitterHandle : UserControl
{
    private static readonly InputCursor ResizeCursor =
        InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

    private bool _isDragging;
    private double _lastX;

    public SplitterHandle()
    {
        InitializeComponent();
        ProtectedCursor = ResizeCursor;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    public event EventHandler<double>? DragDelta;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _lastX = GetHorizontalPosition(e);
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = GetHorizontalPosition(e);
        var horizontalChange = position - _lastX;
        _lastX = position;

        if (Math.Abs(horizontalChange) > 0.1)
        {
            DragDelta?.Invoke(this, horizontalChange);
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    private double GetHorizontalPosition(PointerRoutedEventArgs e)
    {
        var relativeTo = XamlRoot?.Content as UIElement;
        return e.GetCurrentPoint(relativeTo).Position.X;
    }
}

