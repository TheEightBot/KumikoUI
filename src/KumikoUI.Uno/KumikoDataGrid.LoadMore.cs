using System.Windows.Input;
using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace KumikoUI.Uno;

/// <summary>
/// Incremental "load more" loading (#318): the grid asks for the next batch as the user nears the end of the loaded
/// rows, draws a loading row while it waits and an error row with a retry when a batch fails. The state machine is
/// Core's <see cref="KumikoIncrementalLoader"/> (<see cref="KumikoSource.IncrementalLoading"/>); this file maps the
/// dependency properties onto it, adapts <see cref="LoadMoreCommand"/> and WinUI's
/// <see cref="ISupportIncrementalLoading"/>, animates the busy indicator and announces progress. Same names and
/// defaults as the MAUI grid.
/// </summary>
public partial class KumikoDataGrid
{
    private KumikoCommandIncrementalSource? _loadMoreCommandSource;
    private SupportIncrementalLoadingAdapter? _supportIncrementalLoadingSource;
    private bool _pushingLoadMoreState;
    private DispatcherTimer? _loadMoreIndicatorTimer;
    private DispatcherTimer? _loadMoreAnnounceTimer;

    private void InitializeLoadMore()
    {
        var loader = _dataSource.IncrementalLoading;
        loader.StateChanged += OnLoaderStateChanged;
        loader.LoadMoreRequested += OnLoaderLoadMoreRequested;
        loader.LoadMoreCompleted += OnLoaderLoadMoreCompleted;
        RegisterPropertyChangedCallback(ItemsSourceProperty, static (d, _) => ((KumikoDataGrid)d).UpdateLoadMoreHostSource());
    }

    // ── Properties ──

    /// <summary>Identifies the <see cref="IncrementalSource"/> dependency property.</summary>
    public static readonly DependencyProperty IncrementalSourceProperty =
        DependencyProperty.Register(
            nameof(IncrementalSource), typeof(IKumikoIncrementalSource), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) => ((KumikoDataGrid)d)._dataSource.IncrementalSource = (IKumikoIncrementalSource?)e.NewValue));

    /// <summary>
    /// The object that loads more items on request (see <see cref="IKumikoIncrementalSource"/>). It takes precedence
    /// over an <see cref="ItemsSource"/> that implements the interface itself (a <see cref="KumikoIncrementalCollection{T}"/>),
    /// then over one that implements <see cref="ISupportIncrementalLoading"/>, then over <see cref="LoadMoreCommand"/>.
    /// Default null.
    /// </summary>
    public IKumikoIncrementalSource? IncrementalSource
    {
        get => (IKumikoIncrementalSource?)GetValue(IncrementalSourceProperty);
        set => SetValue(IncrementalSourceProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreCommand"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreCommandProperty =
        DependencyProperty.Register(
            nameof(LoadMoreCommand), typeof(ICommand), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, _) => ((KumikoDataGrid)d).UpdateLoadMoreHostSource()));

    /// <summary>
    /// The view model command that loads the next batch, used when neither <see cref="IncrementalSource"/> nor the
    /// items are incremental. Its <see cref="ICommand.CanExecute"/> (with <see cref="LoadMoreCommandParameter"/>) says
    /// whether more items exist. The grid sets <see cref="IsLoadingMore"/> to true and executes it; the view model appends
    /// the items to the bound collection on the UI thread, then sets <see cref="IsLoadingMore"/> back to false, or sets
    /// <see cref="LoadMoreErrorText"/> when the batch failed. Bind both with <c>Mode=TwoWay</c>. Default null.
    /// </summary>
    public ICommand? LoadMoreCommand
    {
        get => (ICommand?)GetValue(LoadMoreCommandProperty);
        set => SetValue(LoadMoreCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreCommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreCommandParameterProperty =
        DependencyProperty.Register(nameof(LoadMoreCommandParameter), typeof(object), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>The parameter passed to <see cref="LoadMoreCommand"/>'s <c>CanExecute</c> and <c>Execute</c>. Default null.</summary>
    public object? LoadMoreCommandParameter
    {
        get => GetValue(LoadMoreCommandParameterProperty);
        set => SetValue(LoadMoreCommandParameterProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreMode"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreModeProperty =
        DependencyProperty.Register(
            nameof(LoadMoreMode), typeof(KumikoLoadMoreMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoLoadMoreMode.Automatic, (d, e) => ((KumikoDataGrid)d)._dataSource.IncrementalLoading.Mode = (KumikoLoadMoreMode)e.NewValue));

    /// <summary>
    /// When the next batch loads: <see cref="KumikoLoadMoreMode.Automatic"/> (the default) as the user nears the end,
    /// <see cref="KumikoLoadMoreMode.Manual"/> when the "Load more" row after the last row is tapped (or Down is pressed
    /// on the last row), <see cref="KumikoLoadMoreMode.None"/> only through <see cref="LoadMoreAsync"/>.
    /// </summary>
    public KumikoLoadMoreMode LoadMoreMode
    {
        get => (KumikoLoadMoreMode)GetValue(LoadMoreModeProperty);
        set => SetValue(LoadMoreModeProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreThreshold"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreThresholdProperty =
        DependencyProperty.Register(
            nameof(LoadMoreThreshold), typeof(int), typeof(KumikoDataGrid),
            new PropertyMetadata(-1, (d, e) => ((KumikoDataGrid)d)._dataSource.IncrementalLoading.Threshold = Math.Max(-1, (int)e.NewValue)));

    /// <summary>
    /// How close to the end the next batch is requested in Automatic mode, in rows remaining after the last row on
    /// screen: 0 when the last row comes into view, -1 (the default) one viewport of rows ahead; a value below -1
    /// counts as -1. WinUI's
    /// <c>IncrementalLoadingThreshold</c> counts viewports instead: n viewports is about n times the rows on screen.
    /// </summary>
    public int LoadMoreThreshold
    {
        get => (int)GetValue(LoadMoreThresholdProperty);
        set => SetValue(LoadMoreThresholdProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreBatchSize"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreBatchSizeProperty =
        DependencyProperty.Register(
            nameof(LoadMoreBatchSize), typeof(int), typeof(KumikoDataGrid),
            new PropertyMetadata(0, (d, e) => ((KumikoDataGrid)d)._dataSource.IncrementalLoading.BatchSize = Math.Max(0, (int)e.NewValue)));

    /// <summary>
    /// How many items each batch asks for (passed as the count to <see cref="IKumikoIncrementalSource.LoadMoreItemsAsync"/>,
    /// and as the page size to an <see cref="ISupportIncrementalLoading"/> items source, like WinUI's
    /// <c>DataFetchSize</c>). 0 (the default), or a negative value, asks for twice the rows that fit the viewport, at
    /// least 20.
    /// </summary>
    public int LoadMoreBatchSize
    {
        get => (int)GetValue(LoadMoreBatchSizeProperty);
        set => SetValue(LoadMoreBatchSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="IsLoadingMore"/> dependency property.</summary>
    public static readonly DependencyProperty IsLoadingMoreProperty =
        DependencyProperty.Register(
            nameof(IsLoadingMore), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d).OnIsLoadingMoreChanged((bool)e.NewValue)));

    /// <summary>
    /// Whether a batch is loading. Bind it with <c>Mode=TwoWay</c>: it follows the grid, and with
    /// <see cref="LoadMoreCommand"/> the view model ends a batch by setting it back to false.
    /// </summary>
    public bool IsLoadingMore
    {
        get => (bool)GetValue(IsLoadingMoreProperty);
        set => SetValue(IsLoadingMoreProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreErrorText"/> dependency property.</summary>
    public static readonly DependencyProperty LoadMoreErrorTextProperty =
        DependencyProperty.Register(
            nameof(LoadMoreErrorText), typeof(string), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) => ((KumikoDataGrid)d).OnLoadMoreErrorTextChanged((string?)e.NewValue)));

    /// <summary>
    /// The message the error row shows after a batch failed; null shows <see cref="KumikoStrings.LoadMoreFailed"/>.
    /// Bind it with <c>Mode=TwoWay</c>: it follows the grid, and with <see cref="LoadMoreCommand"/> the view model
    /// fails the batch by setting it. Cleared when the next batch starts.
    /// </summary>
    public string? LoadMoreErrorText
    {
        get => (string?)GetValue(LoadMoreErrorTextProperty);
        set => SetValue(LoadMoreErrorTextProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreState"/> dependency property. Read-only: the grid sets it.</summary>
    public static readonly DependencyProperty LoadMoreStateProperty =
        DependencyProperty.Register(
            nameof(LoadMoreState), typeof(KumikoLoadMoreState), typeof(KumikoDataGrid), new PropertyMetadata(KumikoLoadMoreState.Idle));

    /// <summary>The state of incremental loading, for status UI outside the grid. Read-only.</summary>
    public KumikoLoadMoreState LoadMoreState => (KumikoLoadMoreState)GetValue(LoadMoreStateProperty);

    // ── Events and methods ──

    /// <summary>Raised on the UI thread before a batch is requested; the sender is the grid.</summary>
    public event EventHandler<KumikoLoadMoreRequestedEventArgs>? LoadMoreRequested;

    /// <summary>
    /// Raised on the UI thread when a batch finished (once its rows are drawn) or failed; the sender is the grid. A
    /// handler of a failed batch can set <see cref="KumikoLoadMoreCompletedEventArgs.ErrorText"/>.
    /// </summary>
    public event EventHandler<KumikoLoadMoreCompletedEventArgs>? LoadMoreCompleted;

    /// <summary>
    /// Loads the next batch now, whatever <see cref="LoadMoreMode"/>, and retries after a failure; while a batch is
    /// loading it joins it. See <see cref="KumikoIncrementalLoader.LoadMoreAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait, not the batch.</param>
    /// <returns>The number of items the batch appended; 0 when there is nothing to load or it failed.</returns>
    public Task<int> LoadMoreAsync(CancellationToken cancellationToken = default) =>
        _dataSource.IncrementalLoading.LoadMoreAsync(cancellationToken);

    /// <summary>
    /// Cancels a batch in flight, clears a failure or the end of the data, and evaluates afresh. Call it after
    /// changing the query behind the items in place (a server-side sort or filter).
    /// </summary>
    public void ResetLoadMore() => _dataSource.IncrementalLoading.Reset();

    // ── Host sources: ISupportIncrementalLoading items, then the command ──

    private void UpdateLoadMoreHostSource()
    {
        var command = LoadMoreCommand;
        if (!ReferenceEquals(_loadMoreCommandSource?.Command, command))
        {
            _loadMoreCommandSource?.Detach();
            _loadMoreCommandSource = command == null
                ? null
                : new KumikoCommandIncrementalSource(command, () => LoadMoreCommandParameter, () => _dataSource.SourceItemCount,
                    SetIsLoadingMoreFromAdapter, SetLoadMoreErrorTextFromAdapter);
        }

        var supporting = ItemsSource as ISupportIncrementalLoading;
        if (!ReferenceEquals(_supportIncrementalLoadingSource?.Collection, supporting))
        {
            _supportIncrementalLoadingSource?.Detach();
            _supportIncrementalLoadingSource = supporting == null ? null : new SupportIncrementalLoadingAdapter(supporting);
        }

        _dataSource.IncrementalLoading.HostSource =
            (IKumikoIncrementalSource?)_supportIncrementalLoadingSource ?? _loadMoreCommandSource;
    }

    private void SetIsLoadingMoreFromAdapter(bool value) => PushLoadMoreValue(IsLoadingMoreProperty, value);

    private void SetLoadMoreErrorTextFromAdapter(string? value) => PushLoadMoreValue(LoadMoreErrorTextProperty, value);

    private void OnIsLoadingMoreChanged(bool value)
    {
        if (_pushingLoadMoreState || value) return;
        _loadMoreCommandSource?.OnIsLoadingMoreCleared(); // the view model ended the batch
    }

    private void OnLoadMoreErrorTextChanged(string? value)
    {
        if (_pushingLoadMoreState) return;
        if (_loadMoreCommandSource is { IsPending: true } adapter)
            adapter.OnErrorTextSet(value); // the view model failed the batch
        else if (_dataSource.IncrementalLoading.State == KumikoLoadMoreState.Failed)
            _dataSource.IncrementalLoading.ErrorText = value;
    }

    private void PushLoadMoreValue(DependencyProperty property, object? value)
    {
        if (Equals(GetValue(property), value)) return;
        bool pushing = _pushingLoadMoreState;
        _pushingLoadMoreState = true;
        try { SetValue(property, value); }
        finally { _pushingLoadMoreState = pushing; }
    }

    // ── Loader events ──

    private void OnLoaderStateChanged(object? sender, EventArgs e)
    {
        var loader = _dataSource.IncrementalLoading;
        PushLoadMoreValue(IsLoadingMoreProperty, loader.State == KumikoLoadMoreState.Loading);
        PushLoadMoreValue(LoadMoreErrorTextProperty, loader.State == KumikoLoadMoreState.Failed ? loader.ErrorText : null);
        PushLoadMoreValue(LoadMoreStateProperty, loader.State);
        Invalidate();
    }

    private void OnLoaderLoadMoreRequested(object? sender, KumikoLoadMoreRequestedEventArgs e)
    {
        LoadMoreRequested?.Invoke(this, e);

        // The user asked: say so now. An automatic batch is announced only if it is still loading a second later.
        if (e.Trigger != KumikoLoadMoreTrigger.NearEnd)
        {
            Announce(KumikoAnnouncements.LoadMoreStarted(_dataSource.Strings), important: true);
            return;
        }
        if (_loadMoreAnnounceTimer is null)
        {
            _loadMoreAnnounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _loadMoreAnnounceTimer.Tick += OnLoadMoreAnnounceTick;
        }
        _loadMoreAnnounceTimer.Stop();
        _loadMoreAnnounceTimer.Start();
    }

    private void OnLoadMoreAnnounceTick(object? sender, object e)
    {
        _loadMoreAnnounceTimer?.Stop();
        if (_dataSource.IncrementalLoading.State == KumikoLoadMoreState.Loading)
            Announce(KumikoAnnouncements.LoadMoreStarted(_dataSource.Strings), important: false);
    }

    private void OnLoaderLoadMoreCompleted(object? sender, KumikoLoadMoreCompletedEventArgs e)
    {
        _loadMoreAnnounceTimer?.Stop();
        LoadMoreCompleted?.Invoke(this, e);
        if (KumikoAnnouncements.LoadMoreCompleted(_dataSource.Strings, e, _dataSource.Culture) is { } message)
            Announce(message, important: true);
    }

    // ── Busy indicator ──

    /// <summary>After each frame: repaint at the indicator's pace while the loading row animates on screen.</summary>
    private void UpdateLoadMoreIndicator()
    {
        if (!_renderer.NeedsLoadMoreAnimationFrame)
        {
            _loadMoreIndicatorTimer?.Stop();
            return;
        }
        if (_loadMoreIndicatorTimer is null)
        {
            _loadMoreIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KumikoRenderer.LoadMoreIndicatorStepMs) };
            _loadMoreIndicatorTimer.Tick += OnLoadMoreIndicatorTick;
        }
        if (!_loadMoreIndicatorTimer.IsEnabled) _loadMoreIndicatorTimer.Start();
    }

    // One tick, one frame: the frame it requests decides whether to go on
    private void OnLoadMoreIndicatorTick(object? sender, object e)
    {
        _loadMoreIndicatorTimer?.Stop();
        RequestRedraw();
    }

    // ── Lifecycle ──

    /// <summary>Unloaded: stop the timers and stop listening to the command and the items (the source's Detach cancels a batch).</summary>
    private void DisconnectLoadMore()
    {
        _loadMoreIndicatorTimer?.Stop();
        _loadMoreAnnounceTimer?.Stop();
        _loadMoreCommandSource?.Detach();
        _supportIncrementalLoadingSource?.Detach();
    }

    /// <summary>Loaded again: listen to the command and the items again.</summary>
    private void ReconnectLoadMore()
    {
        _loadMoreCommandSource?.Attach();
        _supportIncrementalLoadingSource?.Attach();
    }
}
