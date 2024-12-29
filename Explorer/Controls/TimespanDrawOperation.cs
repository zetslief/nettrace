using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Explorer.Controls;

public record Range(double From, double To);

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

    private static Rect Measure(IEnumerable<Renderable> items)
    {
        var result = new Rect(0, 0, 0, 0);
        foreach (var item in items)
        {
            switch (item)
            {
                case LabeledRange range:
                    result = result.WithX(Math.Min((float)range.Range.From, result.Left));
                    result = result.WithWidth(Math.Max((float)range.Range.To, result.Width));
                    break;
                default:
                    throw new NotImplementedException($"Measuring for {item} is not implemented yet.");
            }
        }
        return result;
    }

    private static void RenderLabeledRectangle(SKCanvas canvas, Rect dataBounds, Rect bounds, LabeledRange item)
    {
        var paint = new SKPaint
        {
            Color = SKColors.DarkGoldenrod,
            Style = SKPaintStyle.Stroke,
        };

        // 0 is top, Height is bottom + 1
        canvas.DrawRect(
            new(
                (float)(item.Range.From / dataBounds.Width * bounds.Width),
                -1,
                (float)(item.Range.To / dataBounds.Width * bounds.Width),
                (float)bounds.Height
            ),
            paint);
    }
}