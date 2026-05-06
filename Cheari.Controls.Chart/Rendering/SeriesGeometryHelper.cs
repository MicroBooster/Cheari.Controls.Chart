using Cheari.Controls.Axes.CoordinateMappers;
using Cheari.Controls.Core;
using Cheari.Controls.Series.Types;

namespace Cheari.Controls.Rendering;

/// <summary>
/// 提供 layout auto-fit 与 renderer 共享的几何计算规则。
/// </summary>
internal static class SeriesGeometryHelper
{
    internal const double LineAntialiasingFeatherPixels = 1.0;

    internal static double ResolveLineAntialiasingFeather(bool enableAntialiasing)
        => enableAntialiasing ? LineAntialiasingFeatherPixels : 0.0;

    internal static double ResolveRenderedStrokeThickness(double strokeThickness)
        => Math.Max(1.0, strokeThickness);

    internal static double ResolveRenderedMarkerSize(double markerSize)
        => Math.Max(1.0, markerSize);

    internal static double CalculateLineEndpointPixelPadding(double strokeThickness, bool enableAntialiasing)
    {
        double feather = ResolveLineAntialiasingFeather(enableAntialiasing);
        double halfThickness = ResolveRenderedStrokeThickness(strokeThickness) * 0.5;
        double expandedHalfThickness = halfThickness + feather;
        return Math.Sqrt(feather * feather + expandedHalfThickness * expandedHalfThickness);
    }

    internal static double CalculateLineCrossAxisPixelPadding(double strokeThickness, bool enableAntialiasing)
    {
        double feather = ResolveLineAntialiasingFeather(enableAntialiasing);
        double halfThickness = ResolveRenderedStrokeThickness(strokeThickness) * 0.5;
        return halfThickness + feather;
    }

    internal static double CalculateLineAlongAxisPixelPadding(bool enableAntialiasing)
        => ResolveLineAntialiasingFeather(enableAntialiasing);

    internal static double CalculateMarkerPixelPadding(double markerSize)
        => ResolveRenderedMarkerSize(markerSize) * 0.5;

    internal static double ResolveBarWidthData(
        BarRenderableSeries series,
        double axisSpan,
        int pointCount,
        double fallbackRangeLength)
    {
        ArgumentNullException.ThrowIfNull(series);
        return ResolveDataWidth(
            explicitWidth: series.BarWidth,
            axisSpan: axisSpan,
            pointCount: pointCount,
            fallbackRangeLength: fallbackRangeLength,
            autoWidthFactor: 1.0 - series.BarSpacing,
            fallbackWidthFactor: 0.8);
    }

    internal static double ResolveOhlcBodyWidthData(
        OhlcRenderableSeries series,
        double axisSpan,
        int pointCount,
        double fallbackRangeLength)
    {
        ArgumentNullException.ThrowIfNull(series);
        return ResolveDataWidth(
            explicitWidth: series.BarWidth,
            axisSpan: axisSpan,
            pointCount: pointCount,
            fallbackRangeLength: fallbackRangeLength,
            autoWidthFactor: 0.6,
            fallbackWidthFactor: 0.6);
    }

    internal static bool TryTransformAxisValue(double value, ICoordinateMapper mapper, out double transformedValue)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (mapper is LogCoordinateMapper logMapper)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                transformedValue = 0;
                return false;
            }

            transformedValue = Math.Log(value) * logMapper.InvLogBase;
            return !double.IsNaN(transformedValue) && !double.IsInfinity(transformedValue);
        }

        transformedValue = value;
        return !double.IsNaN(transformedValue) && !double.IsInfinity(transformedValue);
    }

    internal static double InverseTransformAxisValue(double transformedValue, ICoordinateMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (mapper is LogCoordinateMapper logMapper)
            return Math.Pow(logMapper.LogBase, transformedValue);

        return transformedValue;
    }

    internal static DataRange TransformRange(DataRange range, ICoordinateMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (mapper is not LogCoordinateMapper logMapper
            || range.Min <= 0
            || range.Max <= 0)
        {
            return range;
        }

        return new DataRange(
            Math.Log(range.Min) * logMapper.InvLogBase,
            Math.Log(range.Max) * logMapper.InvLogBase);
    }

    private static double ResolveDataWidth(
        double explicitWidth,
        double axisSpan,
        int pointCount,
        double fallbackRangeLength,
        double autoWidthFactor,
        double fallbackWidthFactor)
    {
        double normalizedSpan = Math.Abs(axisSpan);
        double normalizedFallbackRangeLength = Math.Abs(fallbackRangeLength);
        double dataSpacing = pointCount > 1
            ? normalizedSpan / (pointCount - 1)
            : (normalizedFallbackRangeLength > double.Epsilon ? normalizedFallbackRangeLength : 1.0) / 10.0;

        double width = explicitWidth > 0
            ? explicitWidth
            : dataSpacing * autoWidthFactor;

        if (width <= 0)
            width = dataSpacing * fallbackWidthFactor;

        return width;
    }
}
