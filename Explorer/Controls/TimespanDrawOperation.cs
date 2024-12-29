using System;
using System.Collections.Generic;
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
            switch (item)
            {
                case LabeledRange lr:
                    RenderLabeledRectangle(canvas, dataBounds, Bounds, lr);
                    break;
                default:
                    throw new NotImplementedException($"Rendering for {item} is not implemented yet.");
            }
        }

        canvas.Restore();
    }

    private static Range Measure(IEnumerable<Renderable> items)
    {
        var result = new Range(DateTime.MaxValue, DateTime.MinValue);
        foreach (var item in items)
        {
            switch (item)
            {
                case LabeledRange range:
                    result = result with {
                        From = range.Range.From < result.From ? range.Range.From : result.From,
                        To = range.Range.To > result.To ? range.Range.To : result.To 
                    };
                    break;
                default:
                    throw new NotImplementedException($"Measuring for {item} is not implemented yet.");
            }
        }
        return result;
    }

    private static void RenderLabeledRectangle(SKCanvas canvas, Range dataBounds, Rect bounds, LabeledRange item)
    {
        var paint = new SKPaint
        {
            Color = SKColors.DarkGoldenrod,
            Style = SKPaintStyle.Stroke,
        };

        var width = dataBounds.To - dataBounds.From;
        var fromX = (item.Range.From - dataBounds.From) / width;
        var toX = (item.Range.To - dataBounds.From) / width;

        // 0 is top, Height is bottom + 1
        canvas.DrawRect(
            new(
                (float)(fromX * bounds.Width),
                -1,
                (float)(toX * bounds.Width),
                (float)bounds.Height
            ),
            paint);
    }
}