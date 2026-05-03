using System.Windows;

namespace Demo.Views;

public partial class RangePaddingDemoView : ChartDemoViewBase
{
    public RangePaddingDemoView()
    {
        InitializeComponent();
    }

    private void ZoomExtents_Click(object sender, RoutedEventArgs e)
    {
        DemoChart.ZoomExtents();
    }
}
