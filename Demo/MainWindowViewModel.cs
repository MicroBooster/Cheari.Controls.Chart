using System.Windows;

namespace Demo
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;

        public DelegateCommand<string> NavigateCommand { get; }

        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
            NavigateCommand = new DelegateCommand<string>(OnNavigate);
        }

        public void NavigateToHome()
        {
            _regionManager.RequestNavigate("ContentRegion", "HomeView");
        }

        private void OnNavigate(string viewName)
        {
            _regionManager.RequestNavigate("ContentRegion", viewName);
        }
    }
}
