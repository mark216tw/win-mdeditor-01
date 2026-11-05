using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace VibeCode.MdEditor.Controls;

public sealed partial class SplitterHandle : UserControl
{
    private const ulong DoubleClickInterval = 500_000;
    private const double DoubleClickTolerance = 6;

    private static readonly InputCursor ResizeCursor =
        InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

    private bool _isDragging;
    private double _lastX;
    private ulong _lastPrimaryPressTimestamp;
    private Point _lastPrimaryPressPosition;

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

    public event EventHandler? ResetRequested;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var isDoubleClick =
            _lastPrimaryPressTimestamp != 0 &&
            point.Timestamp >= _lastPrimaryPressTimestamp &&
            point.Timestamp - _lastPrimaryPressTimestamp <= DoubleClickInterval &&
            Math.Abs(point.Position.X - _lastPrimaryPressPosition.X) <= DoubleClickTolerance &&
            Math.Abs(point.Position.Y - _lastPrimaryPressPosition.Y) <= DoubleClickTolerance;

        _lastPrimaryPressTimestamp = point.Timestamp;
        _lastPrimaryPressPosition = point.Position;

        if (isDoubleClick)
        {
            _lastPrimaryPressTimestamp = 0;
            _isDragging = false;
            ResetRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
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

