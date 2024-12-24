using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;

namespace Explorer.Controls;

public sealed class TimespanControl : Control
{
    private readonly GlyphRun _noSkia;
    private DateTime[]? items;

    public static readonly DirectProperty<TimespanControl, DateTime[]?> ItemsProperty = AvaloniaProperty.RegisterDirect<TimespanControl, DateTime[]?>(
        "Items",
        owner => owner.items,
        (owner, value) => owner.items = value,
        defaultBindingMode: BindingMode.TwoWay);

    public DateTime[]? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public TimespanControl()
    {
        ClipToBounds = true;
        var text = "Current rendering API is not Skia";
        var glyphs = text.Select(ch => Typeface.Default.GlyphTypeface.GetGlyph(ch)).ToArray();
        _noSkia = new GlyphRun(Typeface.Default.GlyphTypeface, 12, text.AsMemory(), glyphs);
    }

    public override void Render(DrawingContext context)
    {
        context.Custom(new TimespanDrawOperation(new Rect(0, 0, Bounds.Width, Bounds.Height), _noSkia, items));
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }
}
