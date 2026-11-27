using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Export to CSV (#323): the grid's rows as the user sees them, written through <c>ExportToCsvAsync</c> to a file in
/// the cache directory and handed to the platform's share sheet, which offers saving it (Files on iOS, the
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

    private async void OnExportClicked(object? sender, EventArgs e)
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
            string path = Path.Combine(FileSystem.CacheDirectory, "employees.csv");
            var progress = new Progress<KumikoExportProgress>(p => statusLabel.Text = $"{p.RowsWritten} of {p.TotalRows} rows");
            await using (var file = File.Create(path))
            {
                // On the UI thread: the rows are copied here, then written on a pool thread.
                await kumiko.DataSource.ExportToCsvAsync(file, options, progress: progress);
            }

            statusLabel.Text = "Exported.";
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export CSV",
                File = new ShareFile(path, "text/csv"),
            });
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Export failed: " + ex.Message;
        }
    }
}
