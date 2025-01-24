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
        
        if (uiNode.Children.Count > 0)
        {
            var uiCamera = new Camera2D(Position.Zero, Bounds.Into(), Bounds.Into());
            context.Custom(new TimespanDrawOperation(Bounds, uiCamera, uiNode));
        }
        
        if (dataNode is null) return;
        
        if (!TryMeasure(dataNode, out var dataBounds))
        {
            dataBounds = new(0, 0, 1000, 100);
        }
        
        var dataCamera = new Camera2D(Position.Zero, dataBounds, Bounds.Into());
        
        context.Custom(new TimespanDrawOperation(Bounds, dataCamera, dataNode)); 
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
    
    private static bool TryMeasure(Node node, out Rect measurement)
    {
        measurement = Measure(new(0, 0, 0, 0), node);
        return measurement is { Height: 0, Width: 0 };
    }
    
    private static Rect Measure(Rect current, Node item)
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

        return item switch
        {
            Rectangle rectangle => OuterRectangle(current, rectangle.Rect),
            Point point => OuterPosition(current, point.Position),
            TreeNode stacked => stacked.Children.Select(s => Measure(current, s)).Aggregate(current, OuterRectangle),
            _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
        };
    }
}
