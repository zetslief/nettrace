using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Explorer.Controls;

public record Range(DateTime From, DateTime To);

public abstract record Renderable();
public record LabeledRange(string Label, Range Range) : Renderable();
public record StackedRenderable(Stack<IReadOnlyCollection<Renderable>> Items) : Renderable();


internal sealed class TimespanDrawOperation(Rect bounds, GlyphRun noSkia, IReadOnlyCollection<Renderable>? data) : ICustomDrawOperation
{
    private readonly IImmutableGlyphRunReference _noSkia = noSkia.TryCreateImmutableGlyphRunReference()
            ?? throw new InvalidOperationException("Failed to create no skia.");
    private readonly IReadOnlyCollection<Renderable>? data = data;

    public void Dispose()
    {
    }

    public Rect Bounds { get; } = bounds;

    public bool HitTest(Point p) => false;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null)
        {
            context.DrawGlyphRun(Brushes.Black, _noSkia);
            return;
        }

        if (data is null || data.Count == 0)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Clear(SKColors.Black);

        var dataBounds = Measure(data);

        foreach (var item in data)
        {
            Render(canvas, dataBounds, Bounds, item, 0);
        }

        canvas.Restore();
    }

    private static void Render(SKCanvas canvas, Range dataBounds, Rect bounds, Renderable item, int offset)
    {
        switch (item)
        {
            case LabeledRange lr:
                RenderLabeledRectangle(canvas, dataBounds, bounds, offset, lr);
                break;
            case StackedRenderable stacked:
                foreach (var collection in stacked.Items)
                {
                    var stack = offset++;
                    foreach (var single in collection)
                        Render(canvas, dataBounds, bounds, single, stack);
                }
                break;
            default:
                throw new NotImplementedException($"Rendering for {item} is not implemented yet.");
        }
    }

    private static Range Measure(IEnumerable<Renderable> items)
    {
        static Range Outer(Range a, Range b) => new(a.From < b.From ? a.From : b.From, a.To > b.To ? a.To : b.To);

        var result = new Range(DateTime.MaxValue, DateTime.MinValue);
        foreach (var item in items)
        {
            result = item switch
            {
                LabeledRange range => Outer(result, range.Range),
                StackedRenderable stacked => stacked.Items.Select(Measure).Aggregate(result, Outer),
                _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
            };
        }
        return result;
    }

    private static void RenderLabeledRectangle(SKCanvas canvas, Range dataBounds, Rect bounds, int offset, LabeledRange item)
    {
        var paint = new SKPaint
        {
            Color = offset switch
            {
                0 => SKColors.Green,
                1 => SKColors.Blue,
                _ => SKColors.Red
            },
            Style = SKPaintStyle.Stroke,
        };

        var width = dataBounds.To - dataBounds.From;
        var fromX = (item.Range.From - dataBounds.From) / width;
        var toX = (item.Range.To - dataBounds.From) / width;

        // 0 is top, Height is bottom + 1
        canvas.DrawRect(
            new(
                (float)(fromX * bounds.Width),
                (float)(0.1 * bounds.Height * offset),
                (float)(toX * bounds.Width),
                (float)(0.1 * bounds.Height * offset + 0.1 * bounds.Height)
            ),
            paint);
    }
}