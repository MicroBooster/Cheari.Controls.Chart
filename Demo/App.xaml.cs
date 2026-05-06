using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Demo.ViewModels;
using Demo.Views;

namespace Demo
{
    public partial class App : PrismApplication
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int pid);

        public App()
        {
            if (!AttachConsole(-1))
            {
                AllocConsole();
            }

            Console.WriteLine("=== Demo Started ===");
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<LineChartView, LineChartViewModel>();
            containerRegistry.RegisterForNavigation<ScatterChartView, ScatterChartViewModel>();
            containerRegistry.RegisterForNavigation<BarChartView, BarChartViewModel>();
            containerRegistry.RegisterForNavigation<AreaChartView, AreaChartViewModel>();
            containerRegistry.RegisterForNavigation<OhlcChartView, OhlcChartViewModel>();
            containerRegistry.RegisterForNavigation<AxisShowcaseView, AxisShowcaseViewModel>();
            containerRegistry.RegisterForNavigation<MultiAxisView, MultiAxisViewModel>();
            containerRegistry.RegisterForNavigation<LogAxisView, LogAxisViewModel>();
            containerRegistry.RegisterForNavigation<FifoChartView, FifoChartViewModel>();
            containerRegistry.RegisterForNavigation<LegendDemoView, LegendDemoViewModel>();
            containerRegistry.RegisterForNavigation<InteractionDemoView, InteractionDemoViewModel>();
            containerRegistry.RegisterForNavigation<DataReuseDemoView, DataReuseDemoViewModel>();
            containerRegistry.RegisterForNavigation<FinancialChartView, FinancialChartViewModel>();
            containerRegistry.RegisterForNavigation<ThemeCustomizationView, ThemeCustomizationViewModel>();
            containerRegistry.RegisterForNavigation<DataExportView, DataExportViewModel>();
            containerRegistry.RegisterForNavigation<SoftwareBackendView, SoftwareBackendViewModel>();
            containerRegistry.RegisterForNavigation<HardwareOnlyDiagnosticsView, HardwareOnlyDiagnosticsViewModel>();
            containerRegistry.RegisterForNavigation<StressTestView, StressTestViewModel>();
            containerRegistry.RegisterForNavigation<ComprehensiveVerificationView, ComprehensiveVerificationViewModel>();
            containerRegistry.RegisterForNavigation<RangePaddingDemoView, RangePaddingDemoViewModel>();
            containerRegistry.RegisterForNavigation<LogRangePaddingDemoView, LogRangePaddingDemoViewModel>();
            containerRegistry.RegisterForNavigation<HomeView, HomeViewModel>();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }
    }
}
