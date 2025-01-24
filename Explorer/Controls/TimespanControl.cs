using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
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
    private Rect? viewport = null;
    private Position? pressed = null;
    private Position? current = null;

    public static readonly DirectProperty<TimespanControl, Node?> DataNodeProperty = AvaloniaProperty.RegisterDirect<TimespanControl, Node?>(
        nameof(DataNode),
        owner => owner.dataNode,
        (owner, value) => owner.dataNode = value,
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
            (not null, not null) => [
                new Rectangle(new(pressed.Value.X, pressed.Value.Y, current.Value.X, current.Value.Y), Color.FromArgb(90, 50, 100, 50)),
                new Point(pressed.Value, Colors.Yellow),
                new Point(current.Value, Colors.Orange),
            ],
            (not null, null) => [new Point(pressed.Value, Colors.Red)],
            (null, not null) => [],
            (null, null) => []
        });
        
        var uiCamera = new Camera2D(Position.Zero, Bounds.Into(), Bounds.Into());
        
        Stack<(Camera2D, Node)> renderItems = [];
        renderItems.Push((uiCamera, uiNode));
        
        if (dataNode is not null)
        {
            var dataBounds = Measure(null, dataNode) ?? new(0, 0, 1000, 100);
            var dataCamera = new Camera2D(Position.Zero, dataBounds, Bounds.Into());
            renderItems.Push((dataCamera, dataNode));
        }
        
        context.Custom(new TimespanDrawOperation(Bounds, renderItems)); 
    }
    
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            pressed = current = null;
            viewport = null;
            return;
        }
        Debug.Assert(pressed is not null);
        current = e.GetPosition(this).Into();
        viewport = (viewport ?? new Rect()) with { Left = pressed.Value.X, Right = current.Value.X };
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
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Input);
        base.OnPointerMoved(e);
    }
    
    private static Rect? Measure(Rect? current, Node item)
    {
        static Rect OuterRectangle(Rect a, Rect b) => new(
            a.Left < b.Left ? a.Left : b.Left,
            a.Top < b.Top ? a.Top : b.Top,
            a.Right > b.Right ? a.Right : b.Right,
            a.Bottom > b.Bottom ? a.Bottom : b.Bottom
        );
        
        static Rect OuterPosition(Rect a, Position p) => new(
            a.Left < p.X ? a.Left : p.X,
            a.Top < p.Y ? a.Top : p.Y,
            a.Right > p.X ? a.Right : p.X,
            a.Bottom > p.Y ? a.Bottom : p.Y
        );
        
        static Rect RectangleFromPosition(Position p) => new(
            p.X - 10, p.Y - 10, p.X + 10, p.Y + 10
        );

        return item switch
        {
            Rectangle rectangle => current is null ? rectangle.Rect : OuterRectangle(current.Value, rectangle.Rect),
            Point point => current is null ? RectangleFromPosition(point.Position) : OuterPosition(current.Value, point.Position),
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
