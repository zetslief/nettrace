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

public abstract record Node();
public record LabeledRange(string Label, Range Range) : Node();
public record StackedNode(Stack<IReadOnlyCollection<Node>> Items) : Node();


internal sealed class TimespanDrawOperation(Avalonia.Rect bounds, GlyphRun noSkia, IReadOnlyCollection<Node>? data) : ICustomDrawOperation
{
    private readonly Camera2D camera = new(Position.Zero, (float)bounds.Width, (float)bounds.Height, bounds.FromAvalonia());  
    
    private readonly IImmutableGlyphRunReference _noSkia = noSkia.TryCreateImmutableGlyphRunReference()
            ?? throw new InvalidOperationException("Failed to create no skia.");
    private readonly IReadOnlyCollection<Node>? data = data;

    public void Dispose()
    {
    }

    public Avalonia.Rect Bounds { get; } = bounds;

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
            Render(camera, canvas, dataBounds, item, 0);
        }

        canvas.Restore();
    }

    private static void Render(Camera2D camera, SKCanvas canvas, Range dataBounds, Node item, int offset)
    {
        switch (item)
        {
            case LabeledRange lr:
                RenderLabeledRectangle(camera, canvas, dataBounds, offset, lr);
                break;
            case StackedNode stacked:
                foreach (var collection in stacked.Items)
                {
                    var stack = offset++;
                    foreach (var single in collection)
                        Render(camera, canvas, dataBounds, single, stack);
                }
                break;
            default:
                throw new NotImplementedException($"Rendering for {item} is not implemented yet.");
        }
    }

    private static Range Measure(IEnumerable<Node> items)
    {
        static Range Outer(Range a, Range b) => new(a.From < b.From ? a.From : b.From, a.To > b.To ? a.To : b.To);

        var result = new Range(DateTime.MaxValue, DateTime.MinValue);
        foreach (var item in items)
        {
            result = item switch
            {
                LabeledRange range => Outer(result, range.Range),
                StackedNode stacked => stacked.Items.Select(Measure).Aggregate(result, Outer),
                _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
            };
        }
        return result;
    }

    private static void RenderLabeledRectangle(Camera2D camera, SKCanvas canvas, Range dataBounds, int offset, LabeledRange item)
    {
        var circlePaint = new SKPaint
        {
            Color = offset switch
            {
                0 => SKColors.Green,
                1 => SKColors.Blue,
                _ => SKColors.Red
            },
            Style = SKPaintStyle.Fill,
        };
        var strokePaint = new SKPaint
        {
            Color = offset switch
            {
                0 => SKColors.Green,
                1 => SKColors.Blue,
                _ => SKColors.Red
            },
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };
        var fillPaint = new SKPaint
        {
            Color = offset switch
            {
                0 => SKColors.DarkGreen,
                1 => SKColors.DarkBlue,
                _ => SKColors.DarkRed
            },
            Style = SKPaintStyle.Fill,
        };

        var width = dataBounds.To - dataBounds.From;
        var fromX = (item.Range.From - dataBounds.From) / width;
        var toX = (item.Range.To - dataBounds.From) / width;
        var fromY = 0.2 * camera.Height * offset;
        var toY = 0.2 * camera.Height * offset + 0.2 * camera.Height;

        SKRect rect = new(
            (float)(fromX * camera.Width),
            (float)fromY,
            (float)(toX * camera.Width),
            (float)toY
        );
        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, strokePaint);
        canvas.DrawCircle((float)(fromX * camera.Width), (float)fromY, 2, circlePaint);
    }
}

public sealed class Camera2D(Position position, float width, float height, Rect view)
{
    private readonly Rect view = view;
    
    public Position Position { get; } = position;
    public float Width { get; } = width;
    public float Height { get; } = height;
    
    public Position ToViewPosition(Position position)
        => new(view.Width / Width * position.X, view.Height / Height * position.Y);
}

public readonly record struct Position(float X, float Y)
{
    public static Position Zero { get; } = new(0, 0);
}

public readonly record struct Rect(Position TopLeft, Position BottomRight)
{
    public float Width { get; } = BottomRight.X - TopLeft.X;
    public float Height { get; } = BottomRight.Y - TopLeft.Y;
}

public static class Converters
{
    public static Rect FromAvalonia(this Avalonia.Rect rect)
        => new(new((float)rect.Left, (float)rect.Top), new((float)rect.Right, (float)rect.Bottom)); 
    
    public static Position FromSkia(this SKPoint point)
        => new(point.X, point.Y);
}