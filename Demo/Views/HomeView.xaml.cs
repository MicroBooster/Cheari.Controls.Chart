using System.Windows.Controls;
using System.Windows.Input;
using Demo.ViewModels;

namespace Demo.Views;

public partial class HomeView : System.Windows.Controls.UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void CardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string viewName)
        {
            if (DataContext is HomeViewModel vm)
                vm.NavigateCommand.Execute(viewName);
        }
    }
}
