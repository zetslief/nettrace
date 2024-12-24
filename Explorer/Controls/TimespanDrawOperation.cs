using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Explorer.Controls;

internal sealed class TimespanDrawOperation(Rect bounds, GlyphRun noSkia, DateTime[]? data) : ICustomDrawOperation
{
    private readonly IImmutableGlyphRunReference _noSkia = noSkia.TryCreateImmutableGlyphRunReference()
            ?? throw new InvalidOperationException("Failed to create no skia.");
    private readonly DateTime[]? data = data;

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

        if (data is null || data.Length == 0)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Clear(SKColors.Black);

        var min = data.Min();
        var max = data.Max();
        var range = max - min;

        if (range == TimeSpan.Zero)
        {
            return; 
        }

        var paint = new SKPaint
        {
            Color = SKColors.DarkGreen,
            Style = SKPaintStyle.Fill
        };
        

        foreach (var point in data)
        {
            var distance = point - min;
            var position = distance / range;
            var scaledPosition = position * Bounds.Width;
            var rect = new SKRect(0, 0, (float)Bounds.Width / 2, (float)Bounds.Height / 2);
            canvas.DrawLine(
                new SKPoint((float)scaledPosition, 0),
                new SKPoint((float)scaledPosition, (float)Bounds.Height),
                paint
            );
        }

        canvas.Restore();
    }
}