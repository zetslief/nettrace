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
    private readonly GlyphRun _noSkia;
    private IReadOnlyCollection<Node>? items;
    private Rect? viewport = default;
    private Position? pressed = null;
    private Position? current = null;

    public static readonly DirectProperty<TimespanControl, IReadOnlyCollection<Node>?> ItemsProperty = AvaloniaProperty.RegisterDirect<TimespanControl, IReadOnlyCollection<Node>?>(
        nameof(Items),
        owner => owner.items,
        (owner, value) => owner.items = value,
        defaultBindingMode: BindingMode.TwoWay);
    
    public TimespanControl()
    {
        ClipToBounds = true;
        var text = "Current rendering API is not Skia";
        var glyphs = text.Select(ch => Typeface.Default.GlyphTypeface.GetGlyph(ch)).ToArray();
        _noSkia = new GlyphRun(Typeface.Default.GlyphTypeface, 12, text.AsMemory(), glyphs);
    }

    public IReadOnlyCollection<Node>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Node[] uiNodes = (pressed, current) switch 
        {
            (not null, not null) => [
                new Rectangle(new(pressed.Value.X, pressed.Value.Y, current.Value.X, current.Value.Y), Color.FromArgb(90, 50, 100, 50)),
                new Point(pressed.Value, Colors.Yellow),
                new Point(current.Value, Colors.Orange),
            ],
            (not null, null) => [new Point(pressed.Value, Colors.Red)],
            (null, not null) => [],
            (null, null) => []
        };
        context.Custom(new TimespanDrawOperation(Bounds, _noSkia, items ?? [], uiNodes, viewport));
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
}
