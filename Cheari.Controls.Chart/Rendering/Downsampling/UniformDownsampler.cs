namespace Cheari.Controls.Rendering.Downsampling;

/// <summary>
/// 均匀降采样工具类。
/// 用于将大量数据点降采样以适应屏幕分辨率。
/// </summary>
public static class UniformDownsampler
{
    /// <summary>
    /// 降采样数据。
    /// </summary>
    /// <typeparam name="TY">Y 值类型</typeparam>
    /// <param name="data">原始数据</param>
    /// <param name="targetCount">目标点数</param>
    /// <returns>降采样后的数据</returns>
    public static TY[] Downsample<TY>(TY[] data, int targetCount)
    {
        if (data.Length <= targetCount || targetCount <= 0)
            return data;

        TY[] result = new TY[targetCount];
        double step = (double)(data.Length - 1) / (targetCount - 1);

        for (int i = 0; i < targetCount; i++)
        {
            int index = (int)(i * step);
            result[i] = data[index];
        }

        return result;
    }

    /// <summary>
    /// 降采样数据，同时计算 X 值。
    /// </summary>
    /// <typeparam name="TY">Y 值类型</typeparam>
    /// <param name="xStart">X 轴起始值</param>
    /// <param name="xStep">X 轴步长</param>
    /// <param name="yValues">Y 值数组</param>
    /// <param name="targetCount">目标点数</param>
    /// <returns>降采样后的 X 和 Y 值元组</returns>
    public static (double[] X, TY[] Y) DownsampleWithX<TY>(double xStart, double xStep, TY[] yValues, int targetCount)
    {
        if (yValues.Length <= targetCount || targetCount <= 0)
        {
            double[] xValues = new double[yValues.Length];
            for (int i = 0; i < xValues.Length; i++)
                xValues[i] = xStart + xStep * i;
            return (xValues, yValues);
        }

        TY[] yResult = new TY[targetCount];
        double[] xResult = new double[targetCount];
        double step = (double)(yValues.Length - 1) / (targetCount - 1);

        for (int i = 0; i < targetCount; i++)
        {
            int index = (int)(i * step);
            xResult[i] = xStart + xStep * index;
            yResult[i] = yValues[index];
        }

        return (xResult, yResult);
    }
}
