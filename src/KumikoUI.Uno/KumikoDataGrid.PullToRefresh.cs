using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// Pull-to-refresh (#330): with <see cref="IsPullToRefreshEnabled"/>, a finger or pen pulled down from the top of the
/// rows draws an indicator under the header and, released past the threshold, refreshes. The gesture, the indicator
/// and its animation are Core's (<see cref="KumikoInputController.IsPullToRefreshEnabled"/>); this file maps the
/// dependency properties onto it, raises <see cref="Refreshing"/>, executes <see cref="RefreshCommand"/> and announces the
/// state. Same names and defaults as the MAUI grid. The indicator's frames run on the grid's frame loop only while it
/// moves.
/// </summary>
public partial class KumikoDataGrid
{
    private bool _pushingRefreshState;

    private void InitializePullToRefresh()
    {
        _inputController.RefreshRequested += OnPullRefreshRequested;
        _inputController.IsRefreshingChanged += OnControllerIsRefreshingChanged;
    }

    /// <summary>Identifies the <see cref="IsPullToRefreshEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsPullToRefreshEnabledProperty =
        DependencyProperty.Register(
            nameof(IsPullToRefreshEnabled), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d)._inputController.IsPullToRefreshEnabled = (bool)e.NewValue));

    /// <summary>
    /// Whether a finger or pen can pull the rows down from the top to refresh. Default false. A pull that starts with
    /// the grid scrolled to the top draws an indicator under the header (the rows do not move); released past the
    /// threshold (<see cref="KumikoInputController.PullToRefreshThreshold"/>, 64 by default) it sets
    /// <see cref="IsRefreshing"/>, raises <see cref="Refreshing"/> and executes <see cref="RefreshCommand"/>. A mouse,
    /// the wheel and a trackpad never pull, and screen reader users cannot: offer the refresh elsewhere too (a button
    /// or a menu item that sets <see cref="IsRefreshing"/>). Do not also wrap the grid in a <c>RefreshContainer</c>.
    /// </summary>
    public bool IsPullToRefreshEnabled
    {
        get => (bool)GetValue(IsPullToRefreshEnabledProperty);
        set => SetValue(IsPullToRefreshEnabledProperty, value);
    }

    /// <summary>Identifies the <see cref="IsRefreshing"/> dependency property.</summary>
    public static readonly DependencyProperty IsRefreshingProperty =
        DependencyProperty.Register(
            nameof(IsRefreshing), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d).OnIsRefreshingPropertyChanged((bool)e.NewValue)));

    /// <summary>
    /// Whether the data is refreshing. Bind it with <c>Mode=TwoWay</c>: a pull sets it to true, and the app sets it back to false when the
    /// reload finished (on the UI thread). While it is true the indicator holds under the header with a spinner and
    /// another pull is ignored. Setting it to true in code shows the indicator without executing
    /// <see cref="RefreshCommand"/>, so a refresh started from a button looks the same. Default false.
    /// </summary>
    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <summary>Identifies the <see cref="RefreshCommand"/> dependency property.</summary>
    public static readonly DependencyProperty RefreshCommandProperty =
        DependencyProperty.Register(nameof(RefreshCommand), typeof(ICommand), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>
    /// The command a pull released past the threshold executes (with <see cref="RefreshCommandParameter"/>, when its
    /// <see cref="ICommand.CanExecute"/> allows), after <see cref="Refreshing"/>. It reloads the data and then sets
    /// <see cref="IsRefreshing"/> back to false. Call <see cref="ResetLoadMore"/> after reloading incrementally loaded
    /// items. Default null.
    /// </summary>
    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="RefreshCommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty RefreshCommandParameterProperty =
        DependencyProperty.Register(nameof(RefreshCommandParameter), typeof(object), typeof(KumikoDataGrid), new PropertyMetadata(null));

    /// <summary>The parameter passed to <see cref="RefreshCommand"/>'s <c>CanExecute</c> and <c>Execute</c>. Default null.</summary>
    public object? RefreshCommandParameter
    {
        get => GetValue(RefreshCommandParameterProperty);
        set => SetValue(RefreshCommandParameterProperty, value);
    }

    /// <summary>
    /// Raised on the UI thread when a pull is released past the threshold, after <see cref="IsRefreshing"/> turned
    /// true and before <see cref="RefreshCommand"/> executes; the sender is the grid. Reload the data, then set
    /// <see cref="IsRefreshing"/> to false. Not raised when <see cref="IsRefreshing"/> is set in code.
    /// </summary>
    public event EventHandler? Refreshing;

    private void OnIsRefreshingPropertyChanged(bool value)
    {
        if (_pushingRefreshState) return;
        _inputController.IsRefreshing = value; // raises IsRefreshingChanged, which announces
    }

    private void OnControllerIsRefreshingChanged(object? sender, EventArgs e)
    {
        bool value = _inputController.IsRefreshing;
        if (IsRefreshing != value)
        {
            _pushingRefreshState = true;
            try { SetValue(IsRefreshingProperty, value); }
            finally { _pushingRefreshState = false; }
        }
        Announce(KumikoAnnouncements.RefreshStateChanged(_dataSource.Strings, value), important: true);
    }

    private void OnPullRefreshRequested(object? sender, EventArgs e)
    {
        Refreshing?.Invoke(this, EventArgs.Empty);
        if (RefreshCommand is { } command && command.CanExecute(RefreshCommandParameter))
            command.Execute(RefreshCommandParameter);
    }
}
