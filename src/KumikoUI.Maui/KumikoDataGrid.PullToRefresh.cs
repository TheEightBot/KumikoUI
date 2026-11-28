using System.Windows.Input;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// Pull-to-refresh (#330): with <see cref="IsPullToRefreshEnabled"/>, a finger or pen pulled down from the top of the
/// rows draws an indicator under the header and, released past the threshold, refreshes. The gesture, the indicator
/// and its animation are Core's (<see cref="KumikoInputController.IsPullToRefreshEnabled"/>); this file maps the
/// bindable surface onto it, raises <see cref="Refreshing"/>, executes <see cref="RefreshCommand"/> and announces the
/// state. The indicator's frames run on the grid's frame loop only while it moves.
/// </summary>
public partial class KumikoDataGrid
{
    private bool _pushingRefreshState;

    private void InitializePullToRefresh()
    {
        _inputController.RefreshRequested += OnPullRefreshRequested;
        _inputController.IsRefreshingChanged += OnControllerIsRefreshingChanged;
    }

    /// <summary>Identifies the <see cref="IsPullToRefreshEnabled"/> bindable property.</summary>
    public static readonly BindableProperty IsPullToRefreshEnabledProperty = BindableProperty.Create(
        nameof(IsPullToRefreshEnabled), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._inputController.IsPullToRefreshEnabled = (bool)n);

    /// <summary>
    /// Whether a finger or pen can pull the rows down from the top to refresh. Default false. A pull that starts with
    /// the grid scrolled to the top draws an indicator under the header (the rows do not move); released past the
    /// threshold (<see cref="KumikoInputController.PullToRefreshThreshold"/>, 64 by default) it sets
    /// <see cref="IsRefreshing"/>, raises <see cref="Refreshing"/> and executes <see cref="RefreshCommand"/>. A mouse,
    /// the wheel and a trackpad never pull, and screen reader users cannot: offer the refresh elsewhere too (a button
    /// or a menu item that sets <see cref="IsRefreshing"/>). Do not also wrap the grid in a <c>RefreshView</c>.
    /// </summary>
    public bool IsPullToRefreshEnabled
    {
        get => (bool)GetValue(IsPullToRefreshEnabledProperty);
        set => SetValue(IsPullToRefreshEnabledProperty, value);
    }

    /// <summary>Identifies the <see cref="IsRefreshing"/> bindable property.</summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(
        nameof(IsRefreshing), typeof(bool), typeof(KumikoDataGrid), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnIsRefreshingPropertyChanged((bool)n));

    /// <summary>
    /// Whether the data is refreshing. Two-way: a pull sets it to true, and the app sets it back to false when the
    /// reload finished (on the UI thread). While it is true the indicator holds under the header with a spinner and
    /// another pull is ignored. Setting it to true in code shows the indicator without executing
    /// <see cref="RefreshCommand"/>, so a refresh started from a button looks the same. Default false.
    /// </summary>
    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <summary>Identifies the <see cref="RefreshCommand"/> bindable property.</summary>
    public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(
        nameof(RefreshCommand), typeof(ICommand), typeof(KumikoDataGrid));

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

    /// <summary>Identifies the <see cref="RefreshCommandParameter"/> bindable property.</summary>
    public static readonly BindableProperty RefreshCommandParameterProperty = BindableProperty.Create(
        nameof(RefreshCommandParameter), typeof(object), typeof(KumikoDataGrid));

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
        Announce(KumikoAnnouncements.RefreshStateChanged(_dataSource.Strings, value));
    }

    private void OnPullRefreshRequested(object? sender, EventArgs e)
    {
        Refreshing?.Invoke(this, EventArgs.Empty);
        if (RefreshCommand is { } command && command.CanExecute(RefreshCommandParameter))
            command.Execute(RefreshCommandParameter);
    }
}
