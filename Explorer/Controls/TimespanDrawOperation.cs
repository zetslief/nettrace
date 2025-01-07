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

public abstract record Node();
public record Rectangle(Rect Rect, Color Color) : Node();
public record TreeNode(IReadOnlyCollection<Node> Children) : Node();

internal sealed class TimespanDrawOperation(Avalonia.Rect bounds, GlyphRun noSkia, IReadOnlyCollection<Node>? data) : ICustomDrawOperation
{
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

        var dataBounds = Measure(new Rect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue), data);
        dataBounds = dataBounds with { Right = dataBounds.Left + dataBounds.Width * 0.3f };
        
        Camera2D camera = new(Position.Zero, (float)Bounds.Width, dataBounds.Height, Bounds.Into());

        foreach (var item in data)
        {
            Render(camera, canvas, dataBounds, item);
        }

        canvas.Restore();
    }

    private static void Render(Camera2D camera, SKCanvas canvas, Rect dataBounds, Node item)
    {
        switch (item)
        {
            case Rectangle rectangle:
                RenderRectangle(camera, canvas, dataBounds, rectangle);
                break;
            case TreeNode stacked:
                foreach (var collection in stacked.Children)
                {
                    Render(camera, canvas, dataBounds, collection);
                }
                break;
            default:
                throw new NotImplementedException($"Rendering for {item} is not implemented yet.");
        }
    }

    private static Rect Measure(Rect current, IEnumerable<Node> items)
    {
        static Rect Outer(Rect a, Rect b) => new(
            a.Left < b.Left ? a.Left : b.Left,
            a.Top < b.Top ? a.Top : b.Top,
            a.Right > b.Right ? a.Right : b.Right,
            a.Bottom > b.Bottom ? a.Bottom : b.Bottom
        );

        return items.Aggregate(
            current,
            (result, item) => item switch
            {
                Rectangle range => Outer(result, range.Rect),
                TreeNode stacked => Measure(result, stacked.Children),
                _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
            });
    }

    private static void RenderRectangle(Camera2D camera, SKCanvas canvas, Rect dataBounds, Rectangle item)
    {
        var fromX = (item.Rect.Left - dataBounds.Left) / dataBounds.Width;
        var toX = (item.Rect.Right - dataBounds.Left) / dataBounds.Width;
        var fromY = camera.ToViewY(item.Rect.Top);
        var toY = camera.ToViewY(item.Rect.Bottom);

        SKRect rect = new(
            (float)(fromX * camera.Width),
            fromY,
            (float)(toX * camera.Width),
            toY
        );
        
        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() });
        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = item.Color.Dimmer()});
        canvas.DrawCircle(
            (float)(fromX * camera.Width), (float)fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() } );
        canvas.DrawCircle(
            (float)(toX * camera.Width), (float)fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() } );
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
    
    public float ToViewY(float y)
        => view.Height / Height * y;
}

public readonly record struct Position(float X, float Y)
{
    public static Position Zero { get; } = new(0, 0);
}

public readonly record struct Rect(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    
    public override string ToString()
        => $"Rect({Left}, {Top}, {Right}, {Bottom}) | Width {Width} | Height {Height}";
}

public static class Converters
{
    public static Rect Into(this Avalonia.Rect rect)
        => new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    
    public static Position FromSkia(this SKPoint point)
        => new(point.X, point.Y);
    
    public static SKColor Into(this Avalonia.Media.Color color)
        => new(color.R, color.G, color.B, color.A);
    
    public static SKColor Dimmer(this Avalonia.Media.Color color)
        => new(
            (byte)Math.Max(color.R - 50, 0),
            (byte)Math.Max(color.G - 50, 0),
            (byte)Math.Max(color.B - 50, 0),
            color.A);
}