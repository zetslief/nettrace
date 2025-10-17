using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Explorer.Controls;

public sealed class TimespanControl : Control
{
    private Node? dataNode = null;
    private Rect dataBounds = new(0, 0, 1000, 100);
    private Rect? viewport = null;
    private Position? pressed = null;
    private Position? current = null;

    public static readonly DirectProperty<TimespanControl, Node?> DataNodeProperty =
        AvaloniaProperty.RegisterDirect<TimespanControl, Node?>(
            nameof(DataNode),
            owner => owner.dataNode,
            SetDataNodeProperty,
            defaultBindingMode: BindingMode.TwoWay);

    public TimespanControl()
    {
        ClipToBounds = true;
    }

    public Node? DataNode
    {
        get => GetValue(DataNodeProperty);
        set => SetValue(DataNodeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var uiNode = new TreeNode((pressed, current) switch
        {
            (not null, not null) =>
            [
                new Rectangle(new(pressed.Value.X, pressed.Value.Y, current.Value.X, current.Value.Y),
                    Color.FromArgb(90, 50, 100, 50)),
                new Point(pressed.Value, Colors.Yellow),
                new Point(current.Value, Colors.Orange),
            ],
            (not null, null) => [new Point(pressed.Value, Colors.Red)],
            (null, not null) => [],
            (null, null) => []
        });

        var uiCamera = new Camera2D(Vector3.Zero, Vector3.One, new((float)Bounds.Width, (float)Bounds.Height));

        Stack<(Camera2D, Node)> renderItems = [];
        renderItems.Push((uiCamera, uiNode));

        if (dataNode is not null)
        {
            var dataCamera = new Camera2D(
                new(-dataBounds.Left, dataBounds.Top, 0),
                new((float)Bounds.Width / dataBounds.Width, (float)Bounds.Height / dataBounds.Height, 1),
                new((float)Bounds.Width, (float)Bounds.Height));
            renderItems.Push((dataCamera, dataNode));
        }

        context.Custom(new TimespanDrawOperation(Bounds, renderItems));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            pressed = current = null;
            viewport = dataBounds;
            Console.WriteLine($"Viewport is reset to data bounds: {dataBounds}");
            return;
        }

        Debug.Assert(pressed is not null);
        current = e.GetPosition(this).Into();
        // var currentCamera = new Camera2D(Position.Zero, viewport ?? dataBounds, Bounds.Into());
        // var pressedView = currentCamera.FromViewPosition(pressed.Value);
        // var currentView = currentCamera.FromViewPosition(current.Value);
        // viewport = (viewport ?? new Rect()) with { Left = pressedView.X, Right = currentView.X };
        Console.WriteLine($"Viewport is updated: {viewport}");
        pressed = current = null;
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        base.OnPointerEntered(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        pressed = e.GetPosition(this).Into();
        Console.WriteLine($"Mouse pressed: {pressed}");
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        base.OnPointerExited(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        current = e.GetPosition(this).Into();
        base.OnPointerMoved(e);
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Input);
    }

    private static void SetDataNodeProperty(TimespanControl owner, Node? value)
    {
        owner.dataNode = value;
        Console.WriteLine($"Data Node is updated: {value}");

        if (value is not null)
        {
            owner.dataBounds = Measure(null, value) ?? new(0, 0, 1000, 100);
            owner.viewport = owner.dataBounds;
            Console.WriteLine($"Viewport is measured: {owner.viewport}");
        }

        Dispatcher.UIThread.InvokeAsync(owner.InvalidateVisual, DispatcherPriority.Input);
    }

    private static Rect? Measure(Rect? current, Node item)
    {
        static Rect OuterRectangle(Rect a, Rect b) => new(
            Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom)
        );

        static Rect OuterPosition(Rect a, Position p) => new(
            Math.Min(a.Left, p.X), Math.Min(a.Top, p.Y),
            Math.Max(a.Right, p.X), Math.Max(a.Bottom, p.Y)
        );

        static Rect RectangleFromPosition(Position p) => new(p.X, p.Y, p.X, p.Y);

        return item switch
        {
            Rectangle rectangle => current is null ? rectangle.Rect : OuterRectangle(current.Value, rectangle.Rect),
            Point point => current is null
                ? RectangleFromPosition(point.Position)
                : OuterPosition(current.Value, point.Position),
            TreeNode stacked => stacked.Children
                .Select(s => Measure(current, s))
                .Aggregate(current, (l, r) => (l, r) switch
                {
                    (null, null) => null,
                    (var left, null) => left,
                    (null, var right) => right,
                    var (left, right) => OuterRectangle(left.Value, right.Value),
                }),
            _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
        };
    }
}
