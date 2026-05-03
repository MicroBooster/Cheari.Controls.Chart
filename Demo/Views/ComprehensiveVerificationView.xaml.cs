using Demo.ViewModels;
using Prism.Navigation.Regions;

namespace Demo.Views;

public partial class ComprehensiveVerificationView : INavigationAware, IRegionMemberLifetime
{
    private bool _cleanupRequested = true;

    public ComprehensiveVerificationView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public bool KeepAlive => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _cleanupRequested = false;
        if (DataContext is ComprehensiveVerificationViewModel vm)
            vm.Initialize();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        CleanupViewModel();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        CleanupViewModel();
    }

    private void CleanupViewModel()
    {
        if (_cleanupRequested)
            return;

        _cleanupRequested = true;

        if (DataContext is ComprehensiveVerificationViewModel vm)
            vm.Cleanup();
    }
}
