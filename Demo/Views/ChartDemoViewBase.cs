using Demo.ViewModels;
using Prism.Navigation.Regions;

namespace Demo.Views;

public class ChartDemoViewBase : System.Windows.Controls.UserControl, INavigationAware, IRegionMemberLifetime
{
    private bool _cleanupRequested = true;

    protected ChartDemoViewBase()
    {
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Demo 导航页不保留在 region 缓存中，离开后会销毁并在下次导航时重新创建。
    /// </summary>
    public bool KeepAlive => false;

    /// <summary>
    /// 导航进入后初始化当前页面的数据与定时资源。
    /// </summary>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (DataContext is ChartDemoViewModelBase vm)
        {
            _cleanupRequested = false;
            vm.Initialize();
        }
    }

    /// <summary>
    /// 导航离开时立即释放当前页面持有的实时资源。
    /// </summary>
    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        CleanupViewModel();
    }

    /// <summary>
    /// 当前页实例不复用，但显式返回 true 以允许 Prism 正常完成导航匹配流程。
    /// </summary>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        CleanupViewModel();
    }

    private void CleanupViewModel()
    {
        if (_cleanupRequested)
            return;

        _cleanupRequested = true;

        if (DataContext is ChartDemoViewModelBase vm)
            vm.Cleanup();
    }
}
