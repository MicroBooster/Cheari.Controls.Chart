namespace Cheari.Controls.Rendering.Downsampling;

public sealed class DownsampledFrame
{
    public float[] XValues { get; private set; }
    public float[] YValues { get; private set; }
    public int Count { get; private set; }
    public int PointsPerSourceBucket { get; init; }

    public const int PointsPerBucket = 4;

    public DownsampledFrame(int count)
    {
        XValues = new float[count];
        YValues = new float[count];
        Count = count;
    }

    public DownsampledFrame(float[] xValues, float[] yValues, int count)
    {
        XValues = xValues;
        YValues = yValues;
        Count = count;
    }

    public void AppendFrom(float[] newXValues, float[] newYValues, int newCount)
    {
        if (newCount <= 0)
            return;

        int requiredCapacity = Count + newCount;
        if (requiredCapacity > XValues.Length)
        {
            XValues = GrowArray(XValues, requiredCapacity);
            YValues = GrowArray(YValues, requiredCapacity);
        }

        Array.Copy(newXValues, 0, XValues, Count, newCount);
        Array.Copy(newYValues, 0, YValues, Count, newCount);
        Count += newCount;
    }

    private static float[] GrowArray(float[] array, int minCapacity)
    {
        int newCapacity = array.Length == 0 ? 256 : array.Length * 2;
        while (newCapacity < minCapacity)
            newCapacity *= 2;

        var newArray = new float[newCapacity];
        Array.Copy(array, 0, newArray, 0, array.Length);
        return newArray;
    }
}
