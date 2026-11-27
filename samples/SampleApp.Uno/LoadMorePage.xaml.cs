using KumikoUI.Core.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;

namespace SampleApp.Uno;

/// <summary>
/// Incremental "load more" loading (#318, ← SampleApp.Maui LoadMorePage): orders come from a simulated server in
/// batches. Switch between Automatic (load as you near the end) and Manual (a "Load more" row), and make the next
/// request fail to see the retry row.
/// </summary>
public sealed partial class LoadMorePage : Page
{
    private readonly RemoteOrderService _server = new();
    private readonly KumikoIncrementalCollection<RemoteOrder> _orders;

    public LoadMorePage()
    {
        this.InitializeComponent();
        modeBox.ItemsSource = new[] { KumikoLoadMoreMode.Automatic, KumikoLoadMoreMode.Manual };
        modeBox.SelectedIndex = 0;
        _orders = _server.CreateCollection();
        kumiko.ItemsSource = _orders;
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (modeBox.SelectedItem is KumikoLoadMoreMode mode)
            kumiko.LoadMoreMode = mode;
    }

    private void OnFailChanged(object sender, RoutedEventArgs e) => _server.FailNextRequest = failCheck.IsChecked == true;

    private async void OnStartOverClicked(object sender, RoutedEventArgs e) => await _orders.RefreshAsync();

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
