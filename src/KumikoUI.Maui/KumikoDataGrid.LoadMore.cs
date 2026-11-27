using System.Windows.Input;
using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// Incremental "load more" loading (#318): the grid asks for the next batch as the user nears the end of the loaded
/// rows, draws a loading row while it waits and an error row with a retry when a batch fails. The state machine is
/// Core's <see cref="KumikoIncrementalLoader"/> (<see cref="KumikoSource.IncrementalLoading"/>); this file maps the
/// bindable surface onto it, adapts <see cref="LoadMoreCommand"/>, animates the busy indicator and announces progress.
/// </summary>
public partial class KumikoDataGrid
{
    private KumikoCommandIncrementalSource? _loadMoreCommandSource;
    private bool _pushingLoadMoreState;
    private IDispatcherTimer? _loadMoreIndicatorTimer;
    private IDispatcherTimer? _loadMoreAnnounceTimer;
    private int _loadMoreAnnounceBatch;

    private void InitializeLoadMore()
    {
        var loader = _dataSource.IncrementalLoading;
        loader.StateChanged += OnLoaderStateChanged;
        loader.LoadMoreRequested += OnLoaderLoadMoreRequested;
        loader.LoadMoreCompleted += OnLoaderLoadMoreCompleted;
    }

    // ── Properties ──

    /// <summary>Identifies the <see cref="IncrementalSource"/> bindable property.</summary>
    public static readonly BindableProperty IncrementalSourceProperty = BindableProperty.Create(
        nameof(IncrementalSource), typeof(IKumikoIncrementalSource), typeof(KumikoDataGrid),
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._dataSource.IncrementalSource = (IKumikoIncrementalSource?)n);

    /// <summary>
    /// The object that loads more items on request (see <see cref="IKumikoIncrementalSource"/>). It takes precedence
    /// over an <see cref="ItemsSource"/> that implements the interface itself (a <see cref="KumikoIncrementalCollection{T}"/>),
    /// which takes precedence over <see cref="LoadMoreCommand"/>. Default null.
    /// </summary>
    public IKumikoIncrementalSource? IncrementalSource
    {
        get => (IKumikoIncrementalSource?)GetValue(IncrementalSourceProperty);
        set => SetValue(IncrementalSourceProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreCommand"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreCommandProperty = BindableProperty.Create(
        nameof(LoadMoreCommand), typeof(ICommand), typeof(KumikoDataGrid),
        propertyChanged: (b, _, _) => ((KumikoDataGrid)b).UpdateLoadMoreCommandSource());

    /// <summary>
    /// The view model command that loads the next batch, used when neither <see cref="IncrementalSource"/> nor the
    /// items are incremental. Its <see cref="ICommand.CanExecute"/> (with <see cref="LoadMoreCommandParameter"/>) says
    /// whether more items exist. The grid sets <see cref="IsLoadingMore"/> to true and executes it; the view model appends
    /// the items to the bound collection on the UI thread, then sets <see cref="IsLoadingMore"/> back to false, or sets
    /// <see cref="LoadMoreErrorText"/> when the batch failed. Bind both two-way. Default null.
    /// </summary>
    public ICommand? LoadMoreCommand
    {
        get => (ICommand?)GetValue(LoadMoreCommandProperty);
        set => SetValue(LoadMoreCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreCommandParameter"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreCommandParameterProperty = BindableProperty.Create(
        nameof(LoadMoreCommandParameter), typeof(object), typeof(KumikoDataGrid));

    /// <summary>The parameter passed to <see cref="LoadMoreCommand"/>'s <c>CanExecute</c> and <c>Execute</c>. Default null.</summary>
    public object? LoadMoreCommandParameter
    {
        get => GetValue(LoadMoreCommandParameterProperty);
        set => SetValue(LoadMoreCommandParameterProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreMode"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreModeProperty = BindableProperty.Create(
        nameof(LoadMoreMode), typeof(KumikoLoadMoreMode), typeof(KumikoDataGrid), KumikoLoadMoreMode.Automatic,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._dataSource.IncrementalLoading.Mode = (KumikoLoadMoreMode)n);

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

    /// <summary>Identifies the <see cref="LoadMoreThreshold"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreThresholdProperty = BindableProperty.Create(
        nameof(LoadMoreThreshold), typeof(int), typeof(KumikoDataGrid), -1,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._dataSource.IncrementalLoading.Threshold = Math.Max(-1, (int)n));

    /// <summary>
    /// How close to the end the next batch is requested in Automatic mode, in rows remaining after the last row on
    /// screen: 0 when the last row comes into view, -1 (the default) one viewport of rows ahead; a value below -1
    /// counts as -1. Unlike <c>CollectionView.RemainingItemsThreshold</c>, -1 does not turn loading off; use
    /// <see cref="LoadMoreMode"/>.
    /// </summary>
    public int LoadMoreThreshold
    {
        get => (int)GetValue(LoadMoreThresholdProperty);
        set => SetValue(LoadMoreThresholdProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreBatchSize"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreBatchSizeProperty = BindableProperty.Create(
        nameof(LoadMoreBatchSize), typeof(int), typeof(KumikoDataGrid), 0,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._dataSource.IncrementalLoading.BatchSize = Math.Max(0, (int)n));

    /// <summary>
    /// How many items each batch asks for (passed as the count to <see cref="IKumikoIncrementalSource.LoadMoreItemsAsync"/>
    /// and on <see cref="LoadMoreRequested"/>). 0 (the default), or a negative value, asks for twice the rows that fit
    /// the viewport, at least 20.
    /// </summary>
    public int LoadMoreBatchSize
    {
        get => (int)GetValue(LoadMoreBatchSizeProperty);
        set => SetValue(LoadMoreBatchSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="IsLoadingMore"/> bindable property.</summary>
    public static readonly BindableProperty IsLoadingMoreProperty = BindableProperty.Create(
        nameof(IsLoadingMore), typeof(bool), typeof(KumikoDataGrid), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnIsLoadingMoreChanged((bool)n));

    /// <summary>
    /// Whether a batch is loading. Two-way: it follows the grid, and with <see cref="LoadMoreCommand"/> the view model
    /// ends a batch by setting it back to false.
    /// </summary>
    public bool IsLoadingMore
    {
        get => (bool)GetValue(IsLoadingMoreProperty);
        set => SetValue(IsLoadingMoreProperty, value);
    }

    /// <summary>Identifies the <see cref="LoadMoreErrorText"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreErrorTextProperty = BindableProperty.Create(
        nameof(LoadMoreErrorText), typeof(string), typeof(KumikoDataGrid), null, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnLoadMoreErrorTextChanged((string?)n));

    /// <summary>
    /// The message the error row shows after a batch failed; null shows <see cref="KumikoStrings.LoadMoreFailed"/>.
    /// Two-way: it follows the grid, and with <see cref="LoadMoreCommand"/> the view model fails the batch by setting
    /// it. Cleared when the next batch starts.
    /// </summary>
    public string? LoadMoreErrorText
    {
        get => (string?)GetValue(LoadMoreErrorTextProperty);
        set => SetValue(LoadMoreErrorTextProperty, value);
    }

    private static readonly BindablePropertyKey LoadMoreStatePropertyKey = BindableProperty.CreateReadOnly(
        nameof(LoadMoreState), typeof(KumikoLoadMoreState), typeof(KumikoDataGrid), KumikoLoadMoreState.Idle);

    /// <summary>Identifies the read-only <see cref="LoadMoreState"/> bindable property.</summary>
    public static readonly BindableProperty LoadMoreStateProperty = LoadMoreStatePropertyKey.BindableProperty;

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

    // ── Command adapter ──

    private void UpdateLoadMoreCommandSource()
    {
        var command = LoadMoreCommand;
        if (ReferenceEquals(_loadMoreCommandSource?.Command, command)) return;
        _loadMoreCommandSource?.Detach();
        _loadMoreCommandSource = command == null
            ? null
            : new KumikoCommandIncrementalSource(command, () => LoadMoreCommandParameter, () => _dataSource.SourceItemCount,
                SetIsLoadingMoreFromAdapter, SetLoadMoreErrorTextFromAdapter);
        _dataSource.IncrementalLoading.HostSource = _loadMoreCommandSource;
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

    private void PushLoadMoreValue(BindableProperty property, object? value)
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
        if (!Equals(GetValue(LoadMoreStateProperty), loader.State)) SetValue(LoadMoreStatePropertyKey, loader.State);
        InvalidateSurface();
    }

    private void OnLoaderLoadMoreRequested(object? sender, KumikoLoadMoreRequestedEventArgs e)
    {
        LoadMoreRequested?.Invoke(this, e);

        // The user asked: say so now. An automatic batch is announced only if it is still loading a second later.
        _loadMoreAnnounceBatch++;
        if (e.Trigger != KumikoLoadMoreTrigger.NearEnd)
        {
            Announce(KumikoAnnouncements.LoadMoreStarted(_dataSource.Strings));
            return;
        }
        if (_loadMoreAnnounceTimer is null)
        {
            _loadMoreAnnounceTimer = Dispatcher.CreateTimer();
            _loadMoreAnnounceTimer.IsRepeating = false;
            _loadMoreAnnounceTimer.Interval = TimeSpan.FromSeconds(1);
            _loadMoreAnnounceTimer.Tick += OnLoadMoreAnnounceTick;
        }
        _loadMoreAnnounceTimer.Stop();
        _loadMoreAnnounceTimer.Start();
    }

    private void OnLoadMoreAnnounceTick(object? sender, EventArgs e)
    {
        if (_dataSource.IncrementalLoading.State == KumikoLoadMoreState.Loading)
            Announce(KumikoAnnouncements.LoadMoreStarted(_dataSource.Strings));
    }

    private void OnLoaderLoadMoreCompleted(object? sender, KumikoLoadMoreCompletedEventArgs e)
    {
        _loadMoreAnnounceTimer?.Stop();
        LoadMoreCompleted?.Invoke(this, e);
        if (KumikoAnnouncements.LoadMoreCompleted(_dataSource.Strings, e, _dataSource.Culture) is { } message)
            Announce(message);
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
            _loadMoreIndicatorTimer = Dispatcher.CreateTimer();
            _loadMoreIndicatorTimer.IsRepeating = false;
            _loadMoreIndicatorTimer.Interval = TimeSpan.FromMilliseconds(KumikoRenderer.LoadMoreIndicatorStepMs);
            _loadMoreIndicatorTimer.Tick += OnLoadMoreIndicatorTick;
        }
        if (!_loadMoreIndicatorTimer.IsRunning) _loadMoreIndicatorTimer.Start();
    }

    // One-shot: the frame it requests decides whether to go on
    private void OnLoadMoreIndicatorTick(object? sender, EventArgs e) => InvalidateSurface();

    // ── Lifecycle ──

    /// <summary>Unloaded: stop the timers and stop listening to the command (the source's Detach cancels a batch).</summary>
    private void DisconnectLoadMore()
    {
        _loadMoreIndicatorTimer?.Stop();
        _loadMoreAnnounceTimer?.Stop();
        _loadMoreCommandSource?.Detach();
    }

    /// <summary>Reconnected: listen to the command again.</summary>
    private void ReconnectLoadMore() => _loadMoreCommandSource?.Attach();
}
