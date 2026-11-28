using KumikoUI.Core.Data;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Incremental "load more" loading (#318): orders come from a simulated server in batches. Switch between Automatic
/// (load as you near the end) and Manual (a "Load more" row), and make the next request fail to see the retry row.
/// Pull the rows down from the top to reload them (#330); "Start over" shows the same refresh without the gesture.
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
        kumiko.RefreshCommand = new Command(async () => await ReloadAsync());
    }

    /// <summary>
    /// Reloads the orders from the first batch, as a pull does: the indicator spins while <c>IsRefreshing</c> is true.
    /// </summary>
    private async Task ReloadAsync()
    {
        statusLabel.Text = "Reloading...";
        await Task.Delay(1500); // the simulated server answers slowly
        await _orders.RefreshAsync();
        kumiko.ResetLoadMore(); // a reload starts loading on demand afresh
        kumiko.IsRefreshing = false;
        statusLabel.Text = "Reloaded.";
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (modePicker.SelectedItem is KumikoLoadMoreMode mode)
            kumiko.LoadMoreMode = mode;
    }

    private void OnFailChanged(object? sender, CheckedChangedEventArgs e) => _server.FailNextRequest = e.Value;

    // The same refresh without a pull: setting IsRefreshing shows the indicator (and executes no command)
    private async void OnStartOverClicked(object? sender, EventArgs e)
    {
        if (kumiko.IsRefreshing) return;
        kumiko.IsRefreshing = true;
        await ReloadAsync();
    }

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
