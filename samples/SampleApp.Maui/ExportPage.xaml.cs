using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using KumikoUI.Export.Excel;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Export to CSV (#323) and Excel (#324): the grid's rows as the user sees them, written through
/// <c>ExportToCsvAsync</c> or <c>ExportToExcelAsync</c> (the KumikoUI.Export.Excel package) to a file in the cache directory and handed to the platform's share sheet, which offers saving it (Files on iOS, the
/// document picker on Mac Catalyst, "Save as" on Windows). Select rows or group the grid and export again.
/// </summary>
public partial class ExportPage : ContentPage
{
    public ExportPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = ShowcaseData.CreateRows(250, _ => "");
    }

    private void OnGroupChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
            kumiko.GroupDescriptions.Add(new GroupDescription("Department"));
        else
            kumiko.GroupDescriptions.Clear();
    }

    private void OnExportClicked(object? sender, EventArgs e) => _ = ExportAsync(excel: false);

    private void OnExportExcelClicked(object? sender, EventArgs e) => _ = ExportAsync(excel: true);

    private async Task ExportAsync(bool excel)
    {
        var options = new KumikoExportOptions();
        if (selectedCheck.IsChecked)
        {
            options = options with
            {
                Rows = KumikoExportRows.Selected,
                Selection = KumikoExportSelection.FromSelection(kumiko.Selection),
            };
        }

        try
        {
            string path = Path.Join(FileSystem.CacheDirectory, excel ? "employees.xlsx" : "employees.csv");
            var progress = new Progress<KumikoExportProgress>(p => statusLabel.Text = $"{p.RowsWritten} of {p.TotalRows} rows");
            await using (var file = File.Create(path))
            {
                // On the UI thread: the rows are copied here, then written on a pool thread.
                if (excel)
                {
                    // Typed cells, the grid's widths and colors, a frozen header with filters, groups as outlines.
                    var xlsx = new KumikoXlsxOptions { SheetName = "Employees", UseGridColors = true, Style = kumiko.GridStyle };
                    await kumiko.DataSource.ExportToExcelAsync(file, options, xlsx, progress);
                }
                else
                {
                    await kumiko.DataSource.ExportToCsvAsync(file, options, progress: progress);
                }
            }

            statusLabel.Text = "Exported.";
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = excel ? "Export Excel" : "Export CSV",
                File = new ShareFile(path, excel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv"),
            });
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            statusLabel.Text = "Export failed: " + ex.Message;
        }
    }
}
