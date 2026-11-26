using KumikoUI.Core.Data;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Incremental "load more" loading (#318): orders come from a simulated server in batches. Switch between Automatic
/// (load as you near the end) and Manual (a "Load more" row), and make the next request fail to see the retry row.
/// </summary>
public partial class LoadMorePage : ContentPage
{
    private readonly RemoteOrderService _server = new();
    private readonly KumikoIncrementalCollection<RemoteOrder> _orders;

    public LoadMorePage()
    {
        InitializeComponent();
        modePicker.ItemsSource = new[] { KumikoLoadMoreMode.Automatic, KumikoLoadMoreMode.Manual };
        modePicker.SelectedItem = KumikoLoadMoreMode.Automatic;
        _orders = _server.CreateCollection();
        kumiko.ItemsSource = _orders;
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (modePicker.SelectedItem is KumikoLoadMoreMode mode)
            kumiko.LoadMoreMode = mode;
    }

    private void OnFailChanged(object? sender, CheckedChangedEventArgs e) => _server.FailNextRequest = e.Value;

    private async void OnStartOverClicked(object? sender, EventArgs e) => await _orders.RefreshAsync();

    private void OnLoadMoreRequested(object? sender, KumikoLoadMoreRequestedEventArgs e) =>
        statusLabel.Text = $"Loading {e.Count} more ({e.Trigger})...";

    private void OnLoadMoreCompleted(object? sender, KumikoLoadMoreCompletedEventArgs e)
    {
        failCheck.IsChecked = _server.FailNextRequest;
        statusLabel.Text = e.State switch
        {
            KumikoLoadMoreState.Failed => "The request failed.",
            KumikoLoadMoreState.Completed => $"All {_orders.Count} orders loaded.",
            _ => $"{_orders.Count} of {_server.TotalOrders} orders loaded.",
        };
    }
}
