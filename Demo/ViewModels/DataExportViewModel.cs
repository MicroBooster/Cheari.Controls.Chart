using System.Text;
using System.Windows;
using System.Windows.Input;
using Cheari.Controls.Data;
using Cheari.Controls.Series.Types;
using Cheari.Controls.Core;
using Prism.Commands;
using MediaColor = System.Windows.Media.Color;

namespace Demo.ViewModels;

public class DataExportViewModel : ChartDemoViewModelBase
{
    private UniformDataSeries<double, double>? _dataSeries;

    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand CopyDataCommand { get; }

    public DataExportViewModel()
    {
        ExportCsvCommand = new DelegateCommand(ExportCsv);
        ExportJsonCommand = new DelegateCommand(ExportJson);
        CopyDataCommand = new DelegateCommand(CopyData);
    }

    protected override void InitializeChart()
    {
        ResetDefaultAxes(new DataRange(-0.5, 50.5), new DataRange(-3, 3));

        _dataSeries = new UniformDataSeries<double, double>(index => index, _ => 0);
        var rng = new Random(42);

        for (int i = 0; i < 50; i++)
            _dataSeries.Append(Math.Sin(i * 0.3) * 1.5 + Math.Cos(i * 0.15) * 0.7);

        Series.Add(new LineRenderableSeries
        {
            Title = "Wave",
            Stroke = MediaColor.FromRgb(64, 224, 208),
            StrokeThickness = 2,
            DataSeries = _dataSeries
        });

        Series.Add(new ScatterRenderableSeries
        {
            Title = "Points",
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerColor = MediaColor.FromRgb(255, 215, 0),
            DataSeries = _dataSeries
        });

        Description = "数据导出演示 | CSV / JSON 导出 | 剪贴板复制 | 右键菜单";
    }

    private void ExportCsv()
    {
        if (_dataSeries == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = "chart_data.csv"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("X,Y");
        int count = _dataSeries.Count;
        for (int i = 0; i < count; i++)
        {
            sb.AppendLine($"{_dataSeries.GetX(i):F6},{_dataSeries.GetY(i):F6}");
        }

        System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
        System.Windows.MessageBox.Show($"已导出 {count} 条数据到:\n{dlg.FileName}", "导出成功");
    }

    private void ExportJson()
    {
        if (_dataSeries == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "chart_data.json"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("[");
        int count = _dataSeries.Count;
        for (int i = 0; i < count; i++)
        {
            sb.Append($"  {{\"x\":{_dataSeries.GetX(i):F6},\"y\":{_dataSeries.GetY(i):F6}}}");
            sb.AppendLine(i < count - 1 ? "," : "");
        }
        sb.AppendLine("]");

        System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
        System.Windows.MessageBox.Show($"已导出 {count} 条数据到:\n{dlg.FileName}", "导出成功");
    }

    private void CopyData()
    {
        if (_dataSeries == null) return;

        var sb = new StringBuilder();
        int count = _dataSeries.Count;
        for (int i = 0; i < count; i++)
        {
            sb.AppendLine($"{_dataSeries.GetX(i):F6}\t{_dataSeries.GetY(i):F6}");
        }

        System.Windows.Clipboard.SetText(sb.ToString());
        System.Windows.MessageBox.Show($"已复制 {count} 条数据到剪贴板", "复制成功");
    }
}
