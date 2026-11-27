using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// The busy indicator (#327): the grid draws a themed bar or a scrim with a spinner over the data area while
/// <see cref="IsBusy"/> is true, and by default while a background rebuild of a large source (a sort, filter or
/// grouping) or the first load of an empty grid outlasts <see cref="BusyIndicatorDelay"/>. The effective state is
/// Core's <c>KumikoBusyState</c>; this file maps the dependency properties onto it, repaints while the indicator
/// animates and announces the busy state once. Same names and defaults as the MAUI grid.
/// </summary>
public partial class KumikoDataGrid
{
    private KumikoBusyState _busy = null!; // created by InitializeBusy from the constructor
    private DispatcherTimer? _busyIndicatorTimer;

    private void InitializeBusy()
    {
        _busy = new KumikoBusyState(StartBusyDelayTimer);
        _busy.Changed += OnBusyChanged;
        _busy.Attach(_dataSource);
        _dataSource.IncrementalLoading.StateChanged += OnBusyLoaderStateChanged;
    }

    // ── Properties ──

    /// <summary>Identifies the <see cref="IsBusy"/> dependency property.</summary>
    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d)._busy.Manual = (bool)e.NewValue));

    /// <summary>
    /// Whether the grid shows its busy indicator (<see cref="KumikoStyle.BusyIndicatorKind"/>): a thin bar under the
    /// header, or a scrim with a spinner over the rows. The indicator does not block input. Set it while the app loads
    /// data. The grid also shows it by itself while a background rebuild runs (see <see cref="ShowBusyDuringViewUpdate"/>).
    /// Default false.
    /// </summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowBusyDuringViewUpdate"/> dependency property.</summary>
    public static readonly DependencyProperty ShowBusyDuringViewUpdateProperty =
        DependencyProperty.Register(
            nameof(ShowBusyDuringViewUpdate), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, (d, e) => ((KumikoDataGrid)d)._busy.ShowDuringViewUpdate = (bool)e.NewValue));

    /// <summary>
    /// Whether the busy indicator also shows by itself while a background rebuild
    /// (<see cref="KumikoSource.IsViewUpdatePending"/>) or the first load of an empty grid lasts longer than
    /// <see cref="BusyIndicatorDelay"/>, without <see cref="IsBusy"/> being set. A rebuild that finishes sooner never
    /// shows it. Default true.
    /// </summary>
    public bool ShowBusyDuringViewUpdate
    {
        get => (bool)GetValue(ShowBusyDuringViewUpdateProperty);
        set => SetValue(ShowBusyDuringViewUpdateProperty, value);
    }

    /// <summary>Identifies the <see cref="BusyIndicatorDelay"/> dependency property.</summary>
    public static readonly DependencyProperty BusyIndicatorDelayProperty =
        DependencyProperty.Register(
            nameof(BusyIndicatorDelay), typeof(TimeSpan), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoBusyState.DefaultDelay, (d, e) => ((KumikoDataGrid)d)._busy.Delay = (TimeSpan)e.NewValue));

    /// <summary>
    /// How long a background rebuild or an initial load must last before the grid shows the busy indicator by itself
    /// (<see cref="ShowBusyDuringViewUpdate"/>), so a quick one never flashes it. <see cref="IsBusy"/> shows it at once.
    /// Default 300 ms; zero or less shows it at once.
    /// </summary>
    public TimeSpan BusyIndicatorDelay
    {
        get => (TimeSpan)GetValue(BusyIndicatorDelayProperty);
        set => SetValue(BusyIndicatorDelayProperty, value);
    }

    // ── State ──

    private Action StartBusyDelayTimer(TimeSpan delay, Action elapsed)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            elapsed();
        };
        timer.Start();
        return timer.Stop;
    }

    private void OnBusyChanged(object? sender, EventArgs e)
    {
        _renderer.IsBusy = _busy.IsBusy;
        if (_busy.IsBusy) Announce(_dataSource.Strings.AnnounceBusy, important: false);
        RequestRedraw();
    }

    // The first load of an empty grid counts as a pending update; a later batch has its own load-more row
    private void OnBusyLoaderStateChanged(object? sender, EventArgs e) =>
        _busy.InitialLoad = _dataSource.IncrementalLoading.State == KumikoLoadMoreState.Loading && _dataSource.SourceItemCount == 0;

    // ── Indicator frames ──

    /// <summary>After each frame: repaint at the indicator's pace while it animates, and stop when it does not.</summary>
    private void UpdateBusyIndicator()
    {
        if (!_renderer.IsBusyIndicatorAnimating)
        {
            _busyIndicatorTimer?.Stop();
            return;
        }
        if (_busyIndicatorTimer is null)
        {
            _busyIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KumikoRenderer.BusyFrameIntervalMs) };
            _busyIndicatorTimer.Tick += OnBusyIndicatorTick;
        }
        if (!_busyIndicatorTimer.IsEnabled) _busyIndicatorTimer.Start();
    }

    // One tick, one frame: the frame it requests decides whether to go on
    private void OnBusyIndicatorTick(object? sender, object e)
    {
        _busyIndicatorTimer?.Stop();
        RequestRedraw();
    }

    // ── Lifecycle ──

    /// <summary>Unloaded: stop the timers (they hold the grid) and stop following the source.</summary>
    private void DisconnectBusy()
    {
        _busyIndicatorTimer?.Stop();
        _busy.Dispose();
    }

    /// <summary>Loaded again: follow the source again.</summary>
    private void ReconnectBusy() => _busy.Attach(_dataSource);
}
