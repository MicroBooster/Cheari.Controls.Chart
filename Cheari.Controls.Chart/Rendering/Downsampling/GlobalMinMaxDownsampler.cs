using System.Runtime.CompilerServices;
using Cheari.Controls.Core;

namespace Cheari.Controls.Rendering.Downsampling;

public static class GlobalMinMaxDownsampler
{
    public const int DefaultOversampleFactor = 4;

    public static DownsampledFrame? Downsample(
        ReadOnlySpan<float> xValues,
        ReadOnlySpan<float> yValues,
        int dataCount,
        int viewportWidth,
        int oversampleFactor = DefaultOversampleFactor)
    {
        int totalBuckets = viewportWidth * oversampleFactor;
        if (totalBuckets <= 0 || dataCount <= totalBuckets * 2)
            return null;

        double bucketSize = (double)dataCount / totalBuckets;
        int resultCount = totalBuckets * DownsampledFrame.PointsPerBucket;
        var result = new DownsampledFrame(resultCount)
        {
            PointsPerSourceBucket = (int)Math.Ceiling(bucketSize)
        };
        var resultX = result.XValues;
        var resultY = result.YValues;
        int outIdx = 0;

        double currentStart = 0.0;

        for (int bucket = 0; bucket < totalBuckets; bucket++)
        {
            int start = (int)currentStart;
            int end = bucket == totalBuckets - 1
                ? dataCount
                : (int)((bucket + 1) * bucketSize);

            if (end <= start)
                end = Math.Min(dataCount, start + 1);

            int firstIndex = start;
            int lastIndex = end - 1;
            int minIndex = firstIndex;
            int maxIndex = firstIndex;
            float minY = yValues[firstIndex];
            float maxY = yValues[firstIndex];

            for (int i = start + 1; i < end; i++)
            {
                float y = yValues[i];
                if (y < minY) { minY = y; minIndex = i; }
                if (y > maxY) { maxY = y; maxIndex = i; }
            }

            resultX[outIdx] = xValues[firstIndex];
            resultY[outIdx] = yValues[firstIndex];
            outIdx++;

            if (minIndex <= maxIndex)
            {
                resultX[outIdx] = xValues[minIndex];
                resultY[outIdx] = yValues[minIndex];
                outIdx++;
                resultX[outIdx] = xValues[maxIndex];
                resultY[outIdx] = yValues[maxIndex];
                outIdx++;
            }
            else
            {
                resultX[outIdx] = xValues[maxIndex];
                resultY[outIdx] = yValues[maxIndex];
                outIdx++;
                resultX[outIdx] = xValues[minIndex];
                resultY[outIdx] = yValues[minIndex];
                outIdx++;
            }

            resultX[outIdx] = xValues[lastIndex];
            resultY[outIdx] = yValues[lastIndex];
            outIdx++;

            currentStart = (bucket + 1) * bucketSize;
        }

        return result;
    }

    public static DownsampledFrame? DownsampleRange(
        ReadOnlySpan<float> xValues,
        ReadOnlySpan<float> yValues,
        int sourceStartIdx,
        int sourceEndIdx,
        double pointsPerSourceBucket)
    {
        int rangeCount = sourceEndIdx - sourceStartIdx;
        if (rangeCount <= 0 || pointsPerSourceBucket <= 0)
            return null;

        int bucketCount = Math.Max(1, (int)Math.Ceiling(rangeCount / pointsPerSourceBucket));
        var result = new DownsampledFrame(bucketCount * DownsampledFrame.PointsPerBucket)
        {
            PointsPerSourceBucket = (int)Math.Ceiling(pointsPerSourceBucket)
        };
        var resultX = result.XValues;
        var resultY = result.YValues;
        int outIdx = 0;

        double currentStart = (double)sourceStartIdx;

        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int start = (int)currentStart;
            int end = bucket == bucketCount - 1
                ? sourceEndIdx
                : (int)(sourceStartIdx + (bucket + 1) * pointsPerSourceBucket);

            if (end <= start)
                end = Math.Min(sourceEndIdx, start + 1);

            int firstIndex = start;
            int lastIndex = end - 1;
            int minIndex = firstIndex;
            int maxIndex = firstIndex;
            float minY = yValues[firstIndex];
            float maxY = yValues[firstIndex];

            for (int i = start + 1; i < end; i++)
            {
                float y = yValues[i];
                if (y < minY) { minY = y; minIndex = i; }
                if (y > maxY) { maxY = y; maxIndex = i; }
            }

            resultX[outIdx] = xValues[firstIndex];
            resultY[outIdx] = yValues[firstIndex];
            outIdx++;

            if (minIndex <= maxIndex)
            {
                resultX[outIdx] = xValues[minIndex];
                resultY[outIdx] = yValues[minIndex];
                outIdx++;
                resultX[outIdx] = xValues[maxIndex];
                resultY[outIdx] = yValues[maxIndex];
                outIdx++;
            }
            else
            {
                resultX[outIdx] = xValues[maxIndex];
                resultY[outIdx] = yValues[maxIndex];
                outIdx++;
                resultX[outIdx] = xValues[minIndex];
                resultY[outIdx] = yValues[minIndex];
                outIdx++;
            }

            resultX[outIdx] = xValues[lastIndex];
            resultY[outIdx] = yValues[lastIndex];
            outIdx++;

            currentStart = sourceStartIdx + (bucket + 1) * pointsPerSourceBucket;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FindVisibleStart(DownsampledFrame downsampledFrame, double visibleXMin)
    {
        var xValues = downsampledFrame.XValues;
        int count = downsampledFrame.Count;
        int lo = 0;
        int hi = count - 1;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (xValues[mid] < (float)visibleXMin)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FindVisibleEnd(DownsampledFrame downsampledFrame, double visibleXMax)
    {
        var xValues = downsampledFrame.XValues;
        int count = downsampledFrame.Count;
        int lo = 0;
        int hi = count - 1;

        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (xValues[mid] > (float)visibleXMax)
                hi = mid - 1;
            else
                lo = mid;
        }

        return lo + 1;
    }
}
