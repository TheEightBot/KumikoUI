using KumikoUI.Core.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;

namespace SampleApp.Uno;

/// <summary>
/// Incremental "load more" loading (#318, ← SampleApp.Maui LoadMorePage): orders come from a simulated server in
/// batches. Switch between Automatic (load as you near the end) and Manual (a "Load more" row), and make the next
/// request fail to see the retry row. Pull the rows down from the top to reload them (#330); "Start over" shows the same
/// refresh without the gesture.
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
        kumiko.RefreshCommand = new ReloadCommand(this);
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

    /// <summary>The pull's command (the Uno analogue of MAUI's <c>Command</c>): reloads the page's orders.</summary>
    private sealed class ReloadCommand(LoadMorePage page) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter) => await page.ReloadAsync();
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (modeBox.SelectedItem is KumikoLoadMoreMode mode)
            kumiko.LoadMoreMode = mode;
    }

    private void OnFailChanged(object sender, RoutedEventArgs e) => _server.FailNextRequest = failCheck.IsChecked == true;

    // The same refresh without a pull: setting IsRefreshing shows the indicator (and executes no command)
    private async void OnStartOverClicked(object sender, RoutedEventArgs e)
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
