using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using KumikoUI.Export.Excel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;
using Windows.Storage.Pickers;

namespace SampleApp.Uno;

/// <summary>
/// Export to CSV (#323) and Excel (#324, ← SampleApp.Maui ExportPage): the grid's rows as the user sees them, written
/// through <c>ExportToCsvAsync</c> or <c>ExportToExcelAsync</c> (the KumikoUI.Export.Excel package) to the file the user picks in the platform's save dialog. Select rows or group the grid
/// and export again.
/// </summary>
public sealed partial class ExportPage : Page
{
    public ExportPage()
    {
        this.InitializeComponent();
        kumiko.ItemsSource = ShowcaseData.CreateRows(250, _ => "");
    }

    private void OnGroupChanged(object sender, RoutedEventArgs e)
    {
        if (groupCheck.IsChecked == true)
            kumiko.GroupDescriptions.Add(new GroupDescription("Department"));
        else
            kumiko.GroupDescriptions.Clear();
    }

    private void OnExportClicked(object sender, RoutedEventArgs e) => _ = ExportAsync(excel: false);

    private void OnExportExcelClicked(object sender, RoutedEventArgs e) => _ = ExportAsync(excel: true);

    private async Task ExportAsync(bool excel)
    {
        var options = new KumikoExportOptions();
        if (selectedCheck.IsChecked == true)
        {
            options = options with
            {
                Rows = KumikoExportRows.Selected,
                Selection = KumikoExportSelection.FromSelection(kumiko.Selection),
            };
        }

        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "employees" };
            if (excel)
                picker.FileTypeChoices.Add("Excel workbook", new List<string> { ".xlsx" });
            else
                picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
#if WINDOWS
            // WinAppSDK pickers need the window they belong to.
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow));
#endif
            var file = await picker.PickSaveFileAsync();
            if (file is null) return; // canceled

            var progress = new Progress<KumikoExportProgress>(p => statusLabel.Text = $"{p.RowsWritten} of {p.TotalRows} rows");
            await using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0);
            // On the UI thread: the rows are copied here, then written on a pool thread.
            if (excel)
            {
                // Typed cells, the grid's widths and colors, a frozen header with filters, groups as outlines.
                var xlsx = new KumikoXlsxOptions { SheetName = "Employees", UseGridColors = true, Style = kumiko.GridStyle };
                await kumiko.DataSource.ExportToExcelAsync(stream, options, xlsx, progress);
            }
            else
            {
                await kumiko.DataSource.ExportToCsvAsync(stream, options, progress: progress);
            }

            statusLabel.Text = "Exported.";
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            statusLabel.Text = "Export failed: " + ex.Message;
        }
    }
}
