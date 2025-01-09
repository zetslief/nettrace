using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Explorer.Controls;

public abstract record Node();
public record Point(Position Position, Color Color) : Node();
public record Rectangle(Rect Rect, Color Color) : Node();
public record TreeNode(IReadOnlyCollection<Node> Children) : Node();

internal sealed class TimespanDrawOperation(Avalonia.Rect bounds, GlyphRun noSkia, IReadOnlyCollection<Node> data, IReadOnlyCollection<Node> uiData) : ICustomDrawOperation
{
    private readonly IImmutableGlyphRunReference _noSkia = noSkia.TryCreateImmutableGlyphRunReference()
            ?? throw new InvalidOperationException("Failed to create no skia.");
    private readonly IReadOnlyCollection<Node> data = data;
    private readonly IReadOnlyCollection<Node> uiData = uiData;

    public Avalonia.Rect Bounds { get; } = bounds;

    public bool HitTest(Avalonia.Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null)
        {
            context.DrawGlyphRun(Brushes.Black, _noSkia);
            return;
        }

        if (data.Count + uiData.Count == 0)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Clear(SKColors.Black);

        var dataBounds = Measure(new Rect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue), data);
        if (dataBounds.Left == float.MaxValue) dataBounds = new(0, 0, 1, 1);
        
        Camera2D camera = new(Position.Zero, dataBounds, Bounds.Into());
        
        var ui = TranslateUiNode(camera, uiData);

        foreach (var item in data.Concat(ui))
        {
            Render(camera, canvas, item);
        }

        canvas.Restore();
    }

    public void Dispose()
    {
    }

    private static void Render(Camera2D camera, SKCanvas canvas, Node item)
    {
        switch (item)
        {
            case Rectangle rectangle:
                RenderRectangle(camera, canvas, rectangle);
                break;
            case Point point:
                RenderPoint(camera, canvas, point);
                break;
            case TreeNode stacked:
                foreach (var collection in stacked.Children)
                {
                    Render(camera, canvas, collection);
                }
                break;
            default:
                throw new NotImplementedException($"Rendering for {item} is not implemented yet.");
        }
    }

    private static Rect Measure(Rect current, IEnumerable<Node> items)
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

        return items.Aggregate(
            current,
            (result, item) => item switch
            {
                Rectangle rectangle => OuterRectangle(result, rectangle.Rect),
                Point point => OuterPosition(current, point.Position),
                TreeNode stacked => Measure(result, stacked.Children),
                _ => throw new NotImplementedException($"Measuring for {item} is not implemented yet."),
            });
    }
    
    private static void RenderPoint(Camera2D camera, SKCanvas canvas, Point point)
    {
        var position = camera.ToViewPosition(point.Position);
        
        canvas.DrawLine(
            position.X, position.Y - 15, position.X, position.Y,
            new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = point.Color.Dimmer() });
    }

    private static void RenderRectangle(Camera2D camera, SKCanvas canvas, Rectangle item)
    {
        var (fromX, fromY) = camera.ToViewPosition(new(item.Rect.Left, item.Rect.Top));
        var (toX, toY) = camera.ToViewPosition(new(item.Rect.Right, item.Rect.Bottom));

        SKRect rect = new(fromX, fromY, toX, toY);
        
        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() });
        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = item.Color.Dimmer()});
        
        canvas.DrawCircle(
            fromX, fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() } );
        canvas.DrawCircle(
            toX, fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() } );
    }
    
    private static IEnumerable<Node> TranslateUiNode(Camera2D camera, IEnumerable<Node> nodes)
        => nodes.Select(node => TranslateUiNode(camera, node));
    
    private static Node TranslateUiNode(Camera2D camera, Node node) => node switch
    {
        Point point => new Point(camera.FromViewPosition(point.Position), point.Color),
        Rectangle rect => new Rectangle(Rect.FromPositions(
            camera.FromViewPosition(new(rect.Rect.Left, rect.Rect.Top)),
            camera.FromViewPosition(new(rect.Rect.Right, rect.Rect.Bottom))),
            rect.Color),
        _ => node,
    };
}

public sealed class Camera2D(Position position, Rect data, Rect view)
{
    private readonly Rect view = view;
    
    public Position Position { get; } = position;
    public float Width { get; } = data.Width;
    public float Height { get; } = data.Height;
    
    public Position ToViewPosition(Position position)
        => new((position.X - data.Left) / Width * view.Width, (position.Y - data.Top) / Height * view.Height);
    
    public Position FromViewPosition(Position position)
        => new(position.X / view.Width * data.Width + data.Left, position.Y / view.Height * data.Height + data.Top);
    
    public float ToViewY(float y)
        => (y - data.Top) / Height * view.Height;
}

public readonly record struct Position(float X, float Y)
{
    public static Position Zero { get; } = new(0, 0);
}

public readonly record struct Rect(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    
    public static Rect FromPositions(Position leftTop, Position rightBottom)
        => new(leftTop.X, leftTop.Y, rightBottom.X, rightBottom.Y);
    
    public override string ToString()
        => $"Rect({Left}, {Top}, {Right}, {Bottom}) | Width {Width} | Height {Height}";
}

public static class Converters
{
    public static Rect Into(this Avalonia.Rect rect)
        => new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
    
    public static Position FromSkia(this SKPoint point)
        => new(point.X, point.Y);
    
    public static Position Into(this Avalonia.Point point)
        => new((float)point.X, (float)point.Y);
    
    public static SKColor Into(this Avalonia.Media.Color color)
        => new(color.R, color.G, color.B, color.A);
    
    public static SKColor Dimmer(this Avalonia.Media.Color color)
        => new(
            (byte)Math.Max(color.R - 50, 0),
            (byte)Math.Max(color.G - 50, 0),
            (byte)Math.Max(color.B - 50, 0),
            color.A);
}