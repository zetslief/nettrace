using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Avalonia;
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

internal sealed class TimespanDrawOperation(Avalonia.Rect bounds, IEnumerable<(Camera2D, Node)> items) : ICustomDrawOperation
{
    private readonly IEnumerable<(Camera2D, Node)> items = items;

    public Avalonia.Rect Bounds { get; } = bounds;

    public bool HitTest(Avalonia.Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null)
        {
            return;
        }

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Clear(SKColors.Black);

        var stopwatch = Stopwatch.StartNew();

        foreach (var (camera, node) in items)
            Render(camera, canvas, node);

        // Console.WriteLine($"Render: {stopwatch.Elapsed.TotalMilliseconds} ms");

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

    private static void RenderPoint(Camera2D camera, SKCanvas canvas, Point point)
    {
        var position = camera.LocalToWorld(point.Position);

        canvas.DrawLine(
            position.X, position.Y - 15, position.X, position.Y,
            new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = point.Color.Dimmer() });
    }

    private static void RenderRectangle(Camera2D camera, SKCanvas canvas, Rectangle item)
    {
        var (fromX, fromY) = camera.LocalToWorld(new(item.Rect.Left, item.Rect.Top));
        var (toX, toY) = camera.LocalToWorld(new(item.Rect.Right, item.Rect.Bottom));

        SKRect rect = new(fromX, fromY, toX, toY);

        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() });
        canvas.DrawRect(rect, new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = item.Color.Dimmer() });

        canvas.DrawCircle(
            fromX, fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() });
        canvas.DrawCircle(
            toX, fromY, 2,
            new() { Style = SKPaintStyle.Fill, Color = item.Color.Into() });
    }

    private static IEnumerable<Node> TranslateUiNode(Camera2D camera, IEnumerable<Node> nodes)
        => nodes.Select(node => TranslateUiNode(camera, node));

    private static Node TranslateUiNode(Camera2D camera, Node node) => node switch
    {
        Point point => new Point(camera.LocalToWorld(point.Position), point.Color),
        Rectangle rect => new Rectangle(Rect.FromPositions(
            camera.WorldToLocal(new(rect.Rect.Left, rect.Rect.Top)),
            camera.WorldToLocal(new(rect.Rect.Right, rect.Rect.Bottom))),
            rect.Color),
        _ => node,
    };
}

public sealed class Camera2D
{
    private readonly Matrix4x4 _matrix;
    private readonly Matrix4x4 _inverse;
    private readonly Matrix4x4 _scale;

    public Camera2D(Vector3 position, Vector3 scale, Size size)
    {
        _matrix = Matrix4x4.CreateOrthographic(size.X, size.Y, -1, 1) * Matrix4x4.CreateTranslation(position);
        Matrix4x4.Invert(_matrix, out _inverse);
        _scale = Matrix4x4.CreateScale(scale);
    }

    public Position LocalToWorld(Position position)
        => Position.FromTranslation(_matrix * Matrix4x4.CreateTranslation(position.X, position.Y, 0) * _scale);

    public Position WorldToLocal(Position position)
        => Position.FromTranslation(_inverse * Matrix4x4.CreateTranslation(new(position.X, position.Y, 0)));
}

public readonly record struct Position(float X, float Y)
{
    public static Position FromTranslation(Matrix4x4 m) => new(m.Translation.X, m.Translation.Y);
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

public readonly record struct Size(float X, float Y);

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
