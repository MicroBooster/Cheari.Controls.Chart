using Cheari.Controls.Data;
using Cheari.Controls.Core;

namespace Cheari.Controls.Rendering.Downsampling;

/// <summary>
/// MinMax 降采样策略，保留每个桶内的最小值和最大值点，
/// 确保降采样后的线条保留原始数据的视觉极值特征。
/// </summary>
public class MinMaxDownsamplingStrategy : IDownsamplingStrategy
{
    /// <inheritdoc/>
    public void Downsample(IDataSeries dataSeries, int targetCount, DataRange visibleXRange, List<double> sampledX, List<double> sampledY)
    {
        int count = dataSeries.Count;
        if (count == 0)
        {
            sampledX.Clear();
            sampledY.Clear();
            return;
        }

        Span<double> xBuffer = count <= 1024 ? stackalloc double[1024] : new double[count];
        Span<double> yBuffer = count <= 1024 ? stackalloc double[1024] : new double[count];

        int copied = dataSeries.CopyXValues(xBuffer);
        xBuffer = xBuffer.Slice(0, copied);
        dataSeries.CopyYValues(yBuffer);
        yBuffer = yBuffer.Slice(0, copied);

        Downsample(xBuffer, yBuffer, targetCount, visibleXRange, sampledX, sampledY);
    }

    /// <inheritdoc/>
    public void Downsample(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, int targetCount, DataRange visibleXRange, List<double> sampledX, List<double> sampledY)
    {
        sampledX.Clear();
        sampledY.Clear();

        int count = xValues.Length;
        if (count == 0)
            return;

        int startIndex = 0;
        int endIndex = count;

        if (visibleXRange.Min != double.MinValue || visibleXRange.Max != double.MaxValue)
        {
            startIndex = BinarySearchFirstGreaterOrEqual(xValues, visibleXRange.Min);
            endIndex = BinarySearchLastLessOrEqual(xValues, visibleXRange.Max) + 1;

            if (startIndex > 0 && xValues[startIndex] - visibleXRange.Min > 0
                && Math.Abs(xValues[startIndex - 1] - visibleXRange.Min) <= 1e-10)
            {
                startIndex--;
            }

            if (endIndex < count && visibleXRange.Max - xValues[endIndex - 1] > 0
                && Math.Abs(xValues[endIndex] - visibleXRange.Max) <= 1e-10)
            {
                endIndex++;
            }

            if (endIndex <= startIndex)
            {
                return;
            }
        }

        int visibleCount = endIndex - startIndex;
        if (targetCount <= 0 || visibleCount <= targetCount * 2)
        {
            AppendRange(xValues, yValues, startIndex, endIndex, sampledX, sampledY);
            return;
        }

        double bucketSize = (double)visibleCount / targetCount;
        int lastAddedIndex = -1;
        for (int bucket = 0; bucket < targetCount; bucket++)
        {
            int start = startIndex + (int)Math.Floor(bucket * bucketSize);
            int end = bucket == targetCount - 1
                ? endIndex
                : startIndex + (int)Math.Floor((bucket + 1) * bucketSize);

            if (end <= start)
                end = Math.Min(endIndex, start + 1);

            SampleBucket(xValues, yValues, start, end, ref lastAddedIndex, sampledX, sampledY);
        }

        AppendIndexIfNeeded(xValues, yValues, endIndex - 1, ref lastAddedIndex, sampledX, sampledY);
    }

    private static void SampleBucket(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, int startInclusive, int endExclusive, ref int lastAddedIndex, List<double> sampledX, List<double> sampledY)
    {
        if (startInclusive >= endExclusive)
            return;

        int firstIndex = startInclusive;
        int lastIndex = endExclusive - 1;
        int minIndex = -1;
        int maxIndex = -1;
        double minValue = double.MaxValue;
        double maxValue = double.MinValue;

        for (int i = startInclusive; i < endExclusive; i++)
        {
            double y = yValues[i];
            if (double.IsNaN(y) || double.IsInfinity(y))
                continue;

            if (minIndex == -1 || y < minValue)
            {
                minValue = y;
                minIndex = i;
            }

            if (maxIndex == -1 || y > maxValue)
            {
                maxValue = y;
                maxIndex = i;
            }
        }

        AppendIndexIfNeeded(xValues, yValues, firstIndex, ref lastAddedIndex, sampledX, sampledY);

        if (minIndex >= 0 && maxIndex >= 0)
        {
            if (minIndex <= maxIndex)
            {
                AppendIndexIfNeeded(xValues, yValues, minIndex, ref lastAddedIndex, sampledX, sampledY);
                AppendIndexIfNeeded(xValues, yValues, maxIndex, ref lastAddedIndex, sampledX, sampledY);
            }
            else
            {
                AppendIndexIfNeeded(xValues, yValues, maxIndex, ref lastAddedIndex, sampledX, sampledY);
                AppendIndexIfNeeded(xValues, yValues, minIndex, ref lastAddedIndex, sampledX, sampledY);
            }
        }

        AppendIndexIfNeeded(xValues, yValues, lastIndex, ref lastAddedIndex, sampledX, sampledY);
    }

    private static void AppendRange(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, int startInclusive, int endExclusive, List<double> sampledX, List<double> sampledY)
    {
        for (int i = startInclusive; i < endExclusive; i++)
        {
            sampledX.Add(xValues[i]);
            sampledY.Add(yValues[i]);
        }
    }

    private static void AppendIndexIfNeeded(ReadOnlySpan<double> xValues, ReadOnlySpan<double> yValues, int index, ref int lastAddedIndex, List<double> sampledX, List<double> sampledY)
    {
        if (index < 0 || index == lastAddedIndex)
            return;

        sampledX.Add(xValues[index]);
        sampledY.Add(yValues[index]);
        lastAddedIndex = index;
    }

    private static int BinarySearchFirstGreaterOrEqual(ReadOnlySpan<double> values, double target)
    {
        int lo = 0;
        int hi = values.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (values[mid] < target)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static int BinarySearchLastLessOrEqual(ReadOnlySpan<double> values, double target)
    {
        int lo = 0;
        int hi = values.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (values[mid] > target)
                hi = mid - 1;
            else
                lo = mid;
        }
        return lo;
    }
}
