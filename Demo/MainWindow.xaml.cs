using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Demo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (DataContext is MainWindowViewModel vm)
                    vm.NavigateToHome();
            };
        }

        private void HomeNavItem_Click(object sender, MouseButtonEventArgs e)
        {
            ClearAllSelections();
            HighlightHomeNav();
            (DataContext as MainWindowViewModel)?.NavigateCommand?.Execute("HomeView");
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ListBoxItem item && item.Tag is string viewName)
            {
                ClearOtherSelections(listBox);
                ResetHomeHighlight();
                (DataContext as MainWindowViewModel)?.NavigateCommand?.Execute(viewName);
            }
        }

        private void ClearAllSelections()
        {
            BasicChartsList.SelectedIndex = -1;
            AdvancedChartsList.SelectedIndex = -1;
            AppearanceList.SelectedIndex = -1;
            RendererList.SelectedIndex = -1;
        }

        private void ClearOtherSelections(ListBox except)
        {
            if (except != BasicChartsList) BasicChartsList.SelectedIndex = -1;
            if (except != AdvancedChartsList) AdvancedChartsList.SelectedIndex = -1;
            if (except != AppearanceList) AppearanceList.SelectedIndex = -1;
            if (except != RendererList) RendererList.SelectedIndex = -1;
        }

        private void HighlightHomeNav()
        {
            HomeNavItem.Background = (Brush)FindResource("SidebarActiveBg");
            HomeNavItem.BorderBrush = (Brush)FindResource("SidebarActiveBorder");
        }

        private void ResetHomeHighlight()
        {
            HomeNavItem.Background = Brushes.Transparent;
            HomeNavItem.BorderBrush = Brushes.Transparent;
        }
    }
}
