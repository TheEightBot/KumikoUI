using System.Collections.Specialized;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// Swipe row actions (#329): a finger swipes a data row sideways to reveal drawn items, taps one to run it, or swipes
/// the row most of the way across to run the outermost one. Core's <see cref="KumikoInputController"/> recognizes and
/// draws it (see <see cref="KumikoInputController.AllowSwiping"/>); this file maps the bindable surface onto it and hands
/// the grid's <c>DataContext</c> to the <see cref="UnoActionButtonDefinition"/> items.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Wires the swipe items and closes an open row when the rows change; the controller and source are the grid's own.</summary>
    private void InitializeSwipe()
    {
        ((INotifyCollectionChanged)_inputController.LeadingSwipeItems).CollectionChanged += OnSwipeItemsChanged;
        ((INotifyCollectionChanged)_inputController.TrailingSwipeItems).CollectionChanged += OnSwipeItemsChanged;
        DataContextChanged += OnSwipeDataContextChanged;
        _dataSource.ViewChanged += OnSwipeViewChanged;
    }

    // ── AllowSwiping ──

    /// <summary>Identifies the <see cref="AllowSwiping"/> dependency property.</summary>
    public static readonly DependencyProperty AllowSwipingProperty =
        DependencyProperty.Register(
            nameof(AllowSwiping), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d)._inputController.AllowSwiping = (bool)e.NewValue));

    /// <summary>
    /// Whether a finger can swipe a data row sideways to reveal <see cref="LeadingSwipeItems"/> (swiping toward the end
    /// edge) or <see cref="TrailingSwipeItems"/> (toward the start edge). A horizontal-dominant touch drag on a data cell
    /// then swipes instead of panning, so a grid wider than its viewport scrolls sideways from the header row, by a
    /// vertical-first or diagonal pan, by the wheel or trackpad and by the scroll bars. A mouse or pen never swipes.
    /// Swipes are a touch shortcut: offer every swipe action another way too (an action-button column, a context menu, a
    /// toolbar). Default: false.
    /// </summary>
    public bool AllowSwiping
    {
        get => (bool)GetValue(AllowSwipingProperty);
        set => SetValue(AllowSwipingProperty, value);
    }

    // ── SwipeItemWidth ──

    /// <summary>Identifies the <see cref="SwipeItemWidth"/> dependency property.</summary>
    public static readonly DependencyProperty SwipeItemWidthProperty =
        DependencyProperty.Register(
            nameof(SwipeItemWidth), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(72d, (d, e) => ((KumikoDataGrid)d)._inputController.SwipeItemWidth = (float)(double)e.NewValue));

    /// <summary>
    /// The width of each swipe item, in device-independent units: an open row moves aside by the number of its side's
    /// items times this (at most the row's width). A value that is not positive counts as 72. Default: 72.
    /// </summary>
    public double SwipeItemWidth
    {
        get => (double)GetValue(SwipeItemWidthProperty);
        set => SetValue(SwipeItemWidthProperty, value);
    }

    // ── FullSwipeMode ──

    /// <summary>Identifies the <see cref="FullSwipeMode"/> dependency property.</summary>
    public static readonly DependencyProperty FullSwipeModeProperty =
        DependencyProperty.Register(
            nameof(FullSwipeMode), typeof(KumikoFullSwipeMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoFullSwipeMode.None, (d, e) => ((KumikoDataGrid)d)._inputController.FullSwipeMode = (KumikoFullSwipeMode)e.NewValue));

    /// <summary>
    /// Which sides run their outermost item on a full swipe: dragging the row past 60% of its width and lifting the
    /// finger (with <see cref="EnableHaptics"/>, a short vibration marks the point). Default:
    /// <see cref="KumikoFullSwipeMode.None"/>.
    /// </summary>
    public KumikoFullSwipeMode FullSwipeMode
    {
        get => (KumikoFullSwipeMode)GetValue(FullSwipeModeProperty);
        set => SetValue(FullSwipeModeProperty, value);
    }

    // ── Items ──

    /// <summary>
    /// The items a swipe toward the end edge reveals at the start edge of a row (at the left in a left-to-right layout),
    /// outermost first: item 0 sits against the edge, and a full swipe runs it. Declare them in XAML as
    /// <see cref="UnoActionButtonDefinition"/>s: the grid hands them its <c>DataContext</c>, so
    /// <c>Command="{Binding FlagCommand}"</c> binds to the page's view model, and the command receives the row's item
    /// unless <see cref="UnoActionButtonDefinition.CommandParameter"/> is set. Any
    /// <see cref="IActionButtonDefinition"/> works from code.
    /// </summary>
    public IList<IActionButtonDefinition> LeadingSwipeItems => _inputController.LeadingSwipeItems;

    /// <summary>
    /// The items a swipe toward the start edge reveals at the end edge of a row (at the right in a left-to-right
    /// layout), outermost first. See <see cref="LeadingSwipeItems"/>.
    /// </summary>
    public IList<IActionButtonDefinition> TrailingSwipeItems => _inputController.TrailingSwipeItems;

    // ── Events and methods ──

    /// <summary>
    /// Raised when a finger starts to swipe a row: set <see cref="KumikoSwipeStartingEventArgs.Cancel"/> to keep that row
    /// from swiping. It re-surfaces <see cref="KumikoInputController.SwipeStarting"/>.
    /// </summary>
    public event EventHandler<KumikoSwipeStartingEventArgs>? SwipeStarting
    {
        add => _inputController.SwipeStarting += value;
        remove => _inputController.SwipeStarting -= value;
    }

    /// <summary>
    /// Raised once when a swiped row closes again, after one of its items ran
    /// (<see cref="KumikoSwipeEndedEventArgs.InvokedItem"/>) or without running one. It re-surfaces
    /// <see cref="KumikoInputController.SwipeEnded"/>.
    /// </summary>
    public event EventHandler<KumikoSwipeEndedEventArgs>? SwipeEnded
    {
        add => _inputController.SwipeEnded += value;
        remove => _inputController.SwipeEnded -= value;
    }

    /// <summary>Closes the swiped row without running anything. Does nothing when no row is swiped.</summary>
    public void CloseSwipe()
    {
        _inputController.CloseSwipe();
        Invalidate();
    }

    // ── Wiring ──

    /// <summary>
    /// Gives every <see cref="UnoActionButtonDefinition"/> swipe item this grid's <c>DataContext</c> (definitions are
    /// not in the visual tree), and redraws the grid when one changes.
    /// </summary>
    private void SyncSwipeItemDefinitions()
    {
        var context = DataContext;
        SyncSwipeItemDefinitions(_inputController.LeadingSwipeItems, context);
        SyncSwipeItemDefinitions(_inputController.TrailingSwipeItems, context);
    }

    private void SyncSwipeItemDefinitions(IList<IActionButtonDefinition> items, object? context)
    {
        foreach (var definition in items.OfType<UnoActionButtonDefinition>())
        {
            if (!ReferenceEquals(definition.DataContext, context))
                definition.DataContext = context;

            // Subtract first so a repeated sync never double-subscribes
            definition.Changed -= OnActionButtonDefinitionChanged;
            definition.Changed += OnActionButtonDefinitionChanged;
        }
    }

    private void OnSwipeItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var definition in e.OldItems.OfType<UnoActionButtonDefinition>())
                definition.Changed -= OnActionButtonDefinitionChanged;
        }
        SyncSwipeItemDefinitions();
        Invalidate();
    }

    private void OnSwipeDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        SyncSwipeItemDefinitions();

    // A sort, a filter, a group toggled or rows added or removed close the swiped row (it is tracked by item)
    private void OnSwipeViewChanged(object? sender, KumikoUI.Core.Models.KumikoViewChangedEventArgs e)
    {
        if (e.IsStructural) _inputController.CloseSwipe();
    }
}
