using System.Windows;

namespace Demo.Views;

public partial class LogRangePaddingDemoView : ChartDemoViewBase
{
    public LogRangePaddingDemoView()
    {
        InitializeComponent();
    }

    private void ZoomExtents_Click(object sender, RoutedEventArgs e)
    {
        DemoChart.ZoomExtents();
    }
}
