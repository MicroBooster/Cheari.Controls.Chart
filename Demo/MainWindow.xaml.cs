using DevExpress.Xpf.Accordion;
using System.Windows;

namespace Demo
{
    public partial class MainWindow : DevExpress.Xpf.Core.ThemedWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void NavigationAccordion_SelectedItemChanged(object sender, AccordionSelectedItemChangedEventArgs e)
        {
            if (e.NewItem is AccordionItem item && item.Tag is string viewName && !string.IsNullOrEmpty(viewName))
            {
                (DataContext as MainWindowViewModel)?.NavigateCommand?.Execute(viewName);
            }
        }
    }
}
