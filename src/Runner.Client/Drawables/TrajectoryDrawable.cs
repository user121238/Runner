using Runner.Core.Activities;

namespace Runner.Client.Drawables;

/// <summary>
/// 轨迹草图：等距投影（经度按中心纬度余弦缩放）后按比例铺满画布，起点白、终点红。
/// 不接地图 SDK，离线零依赖。
/// </summary>
public sealed class TrajectoryDrawable : IDrawable
{
    private static readonly Color Background = Color.FromArgb("#1E1E1E");
    private static readonly Color Path = Color.FromArgb("#4CAF50");
    private const float Padding = 16;

    private readonly IReadOnlyList<TrackPoint> _points;

    public TrajectoryDrawable(IReadOnlyList<TrackPoint> points)
    {
        _points = points;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Background;
        canvas.FillRoundedRectangle(dirtyRect, 12);

        if (_points.Count < 2)
        {
            canvas.FontColor = Colors.Gray;
            canvas.FontSize = 14;
            canvas.DrawString("无轨迹", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        double centerLatitude = _points.Average(p => p.Latitude) * Math.PI / 180;
        double longitudeScale = Math.Cos(centerLatitude);
        var xs = _points.Select(p => p.Longitude * longitudeScale).ToArray();
        var ys = _points.Select(p => p.Latitude).ToArray();
        double minX = xs.Min(), maxX = xs.Max(), minY = ys.Min(), maxY = ys.Max();
        double spanX = Math.Max(maxX - minX, 1e-9), spanY = Math.Max(maxY - minY, 1e-9);

        float width = dirtyRect.Width - 2 * Padding;
        float height = dirtyRect.Height - 2 * Padding;
        double scale = Math.Min(width / spanX, height / spanY);
        float originX = dirtyRect.X + Padding + (float)((width - spanX * scale) / 2);
        float originY = dirtyRect.Y + Padding + (float)((height - spanY * scale) / 2);

        var path = new PathF();
        for (int i = 0; i < _points.Count; i++)
        {
            float x = originX + (float)((xs[i] - minX) * scale);
            float y = originY + (float)((maxY - ys[i]) * scale);
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        canvas.StrokeColor = Path;
        canvas.StrokeSize = 4;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);

        DrawMarker(canvas, path, 0, Colors.White);
        DrawMarker(canvas, path, path.Count - 1, Color.FromArgb("#FF5252"));
    }

    private static void DrawMarker(ICanvas canvas, PathF path, int index, Color color)
    {
        var point = path[index];
        canvas.FillColor = color;
        canvas.FillCircle(point.X, point.Y, 6);
    }
}
