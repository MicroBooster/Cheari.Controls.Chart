using Cheari.Controls.Legend;
using System.Windows;
using System.Windows.Input;
using Demo.ViewModels;

namespace Demo.Views;

public partial class LegendDemoView : ChartDemoViewBase
{
    public LegendDemoView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LegendDemoViewModel vm)
        {
            LegendControl.Legend = vm.LegendInstance;
        }
    }

    private void LegendChip_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.DataContext is ILegendItem item)
        {
            item.IsVisible = !item.IsVisible;
        }
    }
}
