using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Commands;

namespace Demo.ViewModels;

public class HomeViewModel : BindableBase
{
    public ObservableCollection<ChartCardItem> ChartTypes { get; } = new()
    {
        new ChartCardItem("📈", "折线图", "高性能折线图，支持大数据量和降采样", "LineChartView", "#00BCD4"),
        new ChartCardItem("🔵", "散点图", "可定制标记类型和大小的点位图", "ScatterChartView", "#9C27B0"),
        new ChartCardItem("📊", "柱状图", "支持正负值的动态柱状图", "BarChartView", "#FF9800"),
        new ChartCardItem("🏔️", "面积图", "填充渐变的面积可视化", "AreaChartView", "#4CAF50"),
        new ChartCardItem("🕯️", "蜡烛图", "K线OHLC金融数据图表", "OhlcChartView", "#F44336"),
        new ChartCardItem("🔗", "数据复用", "同一数据多系列渲染", "DataReuseDemoView", "#2196F3"),
        new ChartCardItem("💰", "金融图表", "K线 + 成交量联动", "FinancialChartView", "#795548"),
        new ChartCardItem("🖱️", "交互修饰器", "Tooltip / 十字光标 / 框选", "InteractionDemoView", "#00BCD4"),
        new ChartCardItem("🔀", "多轴系统", "左右上下的多轴演示", "MultiAxisView", "#607D8B"),
        new ChartCardItem("🌐", "对数轴", "对数刻度坐标轴演示", "LogAxisView", "#009688"),
        new ChartCardItem("⏱️", "FIFO实时", "环形缓冲区流式数据", "FifoChartView", "#3F51B5"),
        new ChartCardItem("🏷️", "图例演示", "系列图例与可见性切换", "LegendDemoView", "#E91E63"),
        new ChartCardItem("🎨", "主题定制", "自定义图表外观与样式", "ThemeCustomizationView", "#673AB7"),
        new ChartCardItem("💾", "数据导出", "CSV / JSON / PNG 导出", "DataExportView", "#CDDC39"),
    };

    public ICommand NavigateCommand { get; }

    public HomeViewModel(MainWindowViewModel mainVm)
    {
        NavigateCommand = new DelegateCommand<string?>(viewName =>
        {
            if (!string.IsNullOrEmpty(viewName))
                mainVm.NavigateCommand.Execute(viewName);
        });
    }
}

public class ChartCardItem
{
    public string Icon { get; }
    public string Title { get; }
    public string Description { get; }
    public string ViewName { get; }
    public string AccentColor { get; }

    public ChartCardItem(string icon, string title, string description, string viewName, string accentColor)
    {
        Icon = icon;
        Title = title;
        Description = description;
        ViewName = viewName;
        AccentColor = accentColor;
    }
}
