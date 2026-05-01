using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Rendering.Commands;
using Cheari.Controls.Series.Renderers;
using Cheari.Controls.Series.Types;
using Vortice.Wpf;

namespace Cheari.Controls.Rendering;

/// <summary>
/// GDI 软件渲染器，使用 CPU 进行软件渲染。
/// 作为 D3D11 渲染器的降级方案，适用于不支持硬件加速的环境。
/// </summary>
internal sealed class GdiRenderer : IRenderer
{
    /// <summary>
    /// 获取是否需要 D3D11 支持。
    /// </summary>
    public bool RequiresD3D11 => false;

    public ChartRendererBackend Backend => ChartRendererBackend.Gdi;

    public ImageSource? CurrentImageSource => _bitmap;

    public string? LastError => null;

    private WriteableBitmap? _bitmap;
    private int _width;
    private int _height;
    private bool _isDisposed;

    /// <summary>
    /// 初始化渲染器。
    /// </summary>
    /// <param name="args">绘图表面事件参数</param>
    public void Initialize(DrawingSurfaceEventArgs args)
    {
    }

    /// <summary>
    /// 反初始化渲染器，释放所有资源。
    /// </summary>
    public void Uninitialize()
    {
        _bitmap = null;
    }

    /// <summary>
    /// 使用单个渲染组渲染图表。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">渲染宽度</param>
    /// <param name="height">渲染高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="xRange">X轴数据范围</param>
    /// <param name="yRange">Y轴数据范围</param>
    /// <param name="commands">渲染命令列表</param>
    /// <returns>渲染是否成功</returns>
    public bool Render(
        DrawEventArgs args,
        int width,
        int height,
        Color backgroundColor,
        bool enableAntialiasing,
        DataRange xRange,
        DataRange yRange,
        IReadOnlyList<IRenderCommand>? commands = null)
    {
        var group = new AxisRenderGroup
        {
            XRange = xRange,
            YRange = yRange,
            XMapper = LinearCoordinateMapper.Instance,
            YMapper = LinearCoordinateMapper.Instance,
            Commands = commands ?? Array.Empty<IRenderCommand>()
        };
        return Render(args, width, height, backgroundColor, enableAntialiasing, new AxisRenderGroup[] { group });
    }

    /// <summary>
    /// 使用多个渲染组渲染图表。
    /// </summary>
    /// <param name="args">绘制事件参数</param>
    /// <param name="width">渲染宽度</param>
    /// <param name="height">渲染高度</param>
    /// <param name="backgroundColor">背景颜色</param>
    /// <param name="enableAntialiasing">是否启用抗锯齿</param>
    /// <param name="renderGroups">渲染组列表</param>
    /// <returns>渲染是否成功</returns>
    public bool Render(
        DrawEventArgs args,
        int width,
        int height,
        Color backgroundColor,
        bool enableAntialiasing,
        IReadOnlyList<AxisRenderGroup> renderGroups)
    {
        if (_isDisposed)
            return false;

        if (width <= 0 || height <= 0)
            return false;

        EnsureBitmap(width, height);

        if (_bitmap == null)
            return false;

        _bitmap.Lock();

        try
        {
            var rect = new Int32Rect(0, 0, width, height);
            var pixels = new byte[width * height * 4];
            FillBackground(pixels, width, height, backgroundColor);

            for (int g = 0; g < renderGroups.Count; g++)
            {
                var group = renderGroups[g];
                var xRange = group.XRange;
                var yRange = group.YRange;
                var commands = group.Commands;

                for (int i = 0; i < commands.Count; i++)
                {
                    var cmd = commands[i];
                    if (cmd is AreaRenderOperation area)
                        DrawAreaOperation(pixels, width, height, area, xRange, yRange);
                    else if (cmd is BarRenderOperation bar)
                        DrawBarOperation(pixels, width, height, bar, xRange, yRange);
                    else if (cmd is ScatterRenderOperation scatter)
                        DrawScatterOperation(pixels, width, height, scatter, xRange, yRange);
                    else if (cmd is GridLineRenderOperation grid)
                        DrawGridLineOperation(pixels, width, height, grid, xRange, yRange);
                    else if (cmd is LineRenderOperation line)
                        DrawLineOperation(pixels, width, height, line, xRange, yRange);
                }
            }

            _bitmap.WritePixels(rect, pixels, width * 4, 0);
            _bitmap.AddDirtyRect(rect);
        }
        finally
        {
            _bitmap.Unlock();
        }

        return true;
    }

    /// <summary>
    /// 确保位图存在且尺寸正确。
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap == null || _width != width || _height != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            _width = width;
            _height = height;
        }
    }

    /// <summary>
    /// 填充背景。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="color">背景颜色</param>
    private static void FillBackground(byte[] pixels, int width, int height, Color color)
    {
        byte b = color.B;
        byte g = color.G;
        byte r = color.R;
        byte a = color.A;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }
    }

    /// <summary>
    /// 规范化数据范围。
    /// </summary>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    /// <returns>规范化后的范围长度</returns>
    private static (double xLength, double yLength) NormalizeRanges(DataRange xRange, DataRange yRange)
    {
        double xLength = Math.Abs(xRange.Length) > double.Epsilon ? xRange.Length : 1.0;
        double yLength = Math.Abs(yRange.Length) > double.Epsilon ? yRange.Length : 1.0;
        return (xLength, yLength);
    }

    /// <summary>
    /// 将数据坐标转换为屏幕像素坐标。
    /// </summary>
    /// <param name="dx">数据X坐标</param>
    /// <param name="dy">数据Y坐标</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    /// <param name="xLength">X轴长度</param>
    /// <param name="yLength">Y轴长度</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>屏幕像素坐标</returns>
    private static (int sx, int sy) DataToPixel(double dx, double dy, DataRange xRange, DataRange yRange, double xLength, double yLength, int width, int height)
    {
        int sx = (int)((dx - xRange.Min) / xLength * width);
        int sy = (int)(height - (dy - yRange.Min) / yLength * height);
        return (sx, sy);
    }

    #region Line Drawing

    /// <summary>
    /// 绘制线图渲染操作。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="line">线渲染操作</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private static void DrawLineOperation(byte[] pixels, int width, int height, LineRenderOperation line, DataRange xRange, DataRange yRange)
    {
        var (xLength, yLength) = NormalizeRanges(xRange, yRange);

        byte r = line.StrokeColor.R;
        byte g = line.StrokeColor.G;
        byte b = line.StrokeColor.B;
        byte a = line.StrokeColor.A;

        foreach (var inst in line.LineInstances)
        {
            var (x1, y1) = DataToPixel(inst.X1, inst.Y1, xRange, yRange, xLength, yLength, width, height);
            var (x2, y2) = DataToPixel(inst.X2, inst.Y2, xRange, yRange, xLength, yLength, width, height);
            DrawBresenhamLine(pixels, width, height, x1, y1, x2, y2, r, g, b, a);
        }
    }

    /// <summary>
    /// 绘制网格线渲染操作。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="grid">网格线渲染操作</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private static void DrawGridLineOperation(byte[] pixels, int width, int height, GridLineRenderOperation grid, DataRange xRange, DataRange yRange)
    {
        var (xLength, yLength) = NormalizeRanges(xRange, yRange);

        foreach (var inst in grid.Lines)
        {
            byte r = (byte)(inst.R * 255);
            byte g = (byte)(inst.G * 255);
            byte b = (byte)(inst.B * 255);
            byte a = (byte)(inst.A * 255);

            var (x1, y1) = DataToPixel(inst.X1, inst.Y1, xRange, yRange, xLength, yLength, width, height);
            var (x2, y2) = DataToPixel(inst.X2, inst.Y2, xRange, yRange, xLength, yLength, width, height);
            DrawBresenhamLine(pixels, width, height, x1, y1, x2, y2, r, g, b, a);
        }
    }

    /// <summary>
    /// 使用 Bresenham 算法绘制直线。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="x0">起点X坐标</param>
    /// <param name="y0">起点Y坐标</param>
    /// <param name="x1">终点X坐标</param>
    /// <param name="y1">终点Y坐标</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="a">alpha分量</param>
    private static void DrawBresenhamLine(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte a)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixel(pixels, width, height, x0, y0, r, g, b, a);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    #endregion

    #region Scatter Drawing

    /// <summary>
    /// 绘制散点图渲染操作。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="scatter">散点图渲染操作</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private static void DrawScatterOperation(byte[] pixels, int width, int height, ScatterRenderOperation scatter, DataRange xRange, DataRange yRange)
    {
        var (xLength, yLength) = NormalizeRanges(xRange, yRange);

        foreach (var inst in scatter.Instances)
        {
            var (cx, cy) = DataToPixel(inst.X, inst.Y, xRange, yRange, xLength, yLength, width, height);
            int size = Math.Max(1, (int)inst.Size);
            byte r = (byte)(inst.R * 255);
            byte g = (byte)(inst.G * 255);
            byte b = (byte)(inst.B * 255);
            byte a = (byte)(inst.A * 255);

            DrawMarker(pixels, width, height, cx, cy, size, (MarkerType)(int)inst.MarkerType, r, g, b, a);
        }
    }

    /// <summary>
    /// 绘制标记点。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="cx">中心点X坐标</param>
    /// <param name="cy">中心点Y坐标</param>
    /// <param name="size">大小</param>
    /// <param name="markerType">标记类型</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="a">alpha分量</param>
    private static void DrawMarker(byte[] pixels, int width, int height, int cx, int cy, int size, MarkerType markerType, byte r, byte g, byte b, byte a)
    {
        int halfSize = size / 2;

        switch (markerType)
        {
            case MarkerType.Circle:
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    for (int dx = -halfSize; dx <= halfSize; dx++)
                    {
                        if (dx * dx + dy * dy <= halfSize * halfSize)
                            SetPixel(pixels, width, height, cx + dx, cy + dy, r, g, b, a);
                    }
                }
                break;

            case MarkerType.Square:
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    for (int dx = -halfSize; dx <= halfSize; dx++)
                        SetPixel(pixels, width, height, cx + dx, cy + dy, r, g, b, a);
                }
                break;

            case MarkerType.Diamond:
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    for (int dx = -halfSize; dx <= halfSize; dx++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) <= halfSize)
                            SetPixel(pixels, width, height, cx + dx, cy + dy, r, g, b, a);
                    }
                }
                break;

            case MarkerType.TriangleUp:
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    int rowWidth = halfSize + dy + 1;
                    for (int dx = -rowWidth; dx <= rowWidth; dx++)
                        SetPixel(pixels, width, height, cx + dx, cy + dy, r, g, b, a);
                }
                break;

            case MarkerType.TriangleDown:
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    int rowWidth = halfSize - dy + 1;
                    for (int dx = -rowWidth; dx <= rowWidth; dx++)
                        SetPixel(pixels, width, height, cx + dx, cy + dy, r, g, b, a);
                }
                break;
        }
    }

    #endregion

    #region Bar Drawing

    /// <summary>
    /// 绘制柱状图渲染操作。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="bar">柱状图渲染操作</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private static void DrawBarOperation(byte[] pixels, int width, int height, BarRenderOperation bar, DataRange xRange, DataRange yRange)
    {
        var (xLength, yLength) = NormalizeRanges(xRange, yRange);

        foreach (var inst in bar.Instances)
        {
            var (x1, y1) = DataToPixel(inst.X1, inst.Y1, xRange, yRange, xLength, yLength, width, height);
            var (x2, y2) = DataToPixel(inst.X2, inst.Y2, xRange, yRange, xLength, yLength, width, height);

            byte r = (byte)(inst.R * 255);
            byte g = (byte)(inst.G * 255);
            byte b = (byte)(inst.B * 255);
            byte a = (byte)(inst.A * 255);

            int left = Math.Min(x1, x2);
            int right = Math.Max(x1, x2);
            int top = Math.Min(y1, y2);
            int bottom = Math.Max(y1, y2);

            for (int py = top; py <= bottom; py++)
            {
                for (int px = left; px <= right; px++)
                    SetPixel(pixels, width, height, px, py, r, g, b, a);
            }
        }
    }

    #endregion

    #region Area Drawing

    /// <summary>
    /// 绘制面积图渲染操作。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="area">面积图渲染操作</param>
    /// <param name="xRange">X轴范围</param>
    /// <param name="yRange">Y轴范围</param>
    private static void DrawAreaOperation(byte[] pixels, int width, int height, AreaRenderOperation area, DataRange xRange, DataRange yRange)
    {
        var (xLength, yLength) = NormalizeRanges(xRange, yRange);

        if (area.FillVertices.Count >= 3)
        {
            var points = new List<(int x, int y, byte r, byte g, byte b, byte a)>();
            for (int i = 0; i < area.FillVertices.Count; i += 3)
            {
                var v0 = area.FillVertices[i];
                var v1 = area.FillVertices[i + 1];
                var v2 = area.FillVertices[i + 2];

                var (x0, y0) = DataToPixel(v0.X, v0.Y, xRange, yRange, xLength, yLength, width, height);
                var (x1p, y1p) = DataToPixel(v1.X, v1.Y, xRange, yRange, xLength, yLength, width, height);
                var (x2p, y2p) = DataToPixel(v2.X, v2.Y, xRange, yRange, xLength, yLength, width, height);

                byte r = (byte)(v0.R * 255);
                byte g = (byte)(v0.G * 255);
                byte b = (byte)(v0.B * 255);
                byte a = (byte)(v0.A * 255);

                FillTriangle(pixels, width, height, x0, y0, x1p, y1p, x2p, y2p, r, g, b, a);
            }
        }

        if (area.BorderLine != null)
            DrawLineOperation(pixels, width, height, area.BorderLine, xRange, yRange);
    }

    /// <summary>
    /// 填充三角形。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="x0">顶点0 X坐标</param>
    /// <param name="y0">顶点0 Y坐标</param>
    /// <param name="x1">顶点1 X坐标</param>
    /// <param name="y1">顶点1 Y坐标</param>
    /// <param name="x2">顶点2 X坐标</param>
    /// <param name="y2">顶点2 Y坐标</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="a">alpha分量</param>
    private static void FillTriangle(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a)
    {
        int minX = Math.Max(0, Math.Min(Math.Min(x0, x1), x2));
        int maxX = Math.Min(width - 1, Math.Max(Math.Max(x0, x1), x2));
        int minY = Math.Max(0, Math.Min(Math.Min(y0, y1), y2));
        int maxY = Math.Min(height - 1, Math.Max(Math.Max(y0, y1), y2));

        int denom = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
        if (denom == 0) return;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                int w0 = (y1 - y2) * (px - x2) + (x2 - x1) * (py - y2);
                int w1 = (y2 - y0) * (px - x2) + (x0 - x2) * (py - y2);
                int w2 = denom - w0 - w1;

                if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                    SetPixel(pixels, width, height, px, py, r, g, b, a);
            }
        }
    }

    #endregion

    /// <summary>
    /// 设置单个像素的颜色。
    /// </summary>
    /// <param name="pixels">像素数组</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="a">alpha分量</param>
    private static void SetPixel(byte[] pixels, int width, int height, int x, int y, byte r, byte g, byte b, byte a)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int offset = (y * width + x) * 4;
        pixels[offset] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = a;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _bitmap = null;
        _isDisposed = true;
    }
}
