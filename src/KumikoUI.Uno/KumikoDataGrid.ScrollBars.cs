using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// The grid's scroll bars (#316): when each one is drawn. They are style settings
/// (<see cref="KumikoStyle.VerticalScrollBarVisibility"/>, <see cref="KumikoStyle.HorizontalScrollBarVisibility"/>);
/// a value set here is written into the current style and re-applied after <see cref="GridStyle"/> or
/// <see cref="Theme"/> is replaced, so it survives a theme change. Their size and colors are on <see cref="KumikoStyle"/>.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="VerticalScrollBarVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility), typeof(KumikoScrollBarVisibility), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoScrollBarVisibility.Auto, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.VerticalScrollBarVisibility = (KumikoScrollBarVisibility)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>
    /// When the vertical scroll bar is drawn: <see cref="KumikoScrollBarVisibility.Auto"/> (the default) shows it while
    /// the grid scrolls and while a mouse rests over the right edge of the rows, then fades it;
    /// <see cref="KumikoScrollBarVisibility.Always"/> draws it whenever the rows overflow;
    /// <see cref="KumikoScrollBarVisibility.Never"/> draws none. With a mouse or a pen the thumb drags and the track
    /// pages; a finger pans through it. Until set, the style's value applies; once set, it wins over
    /// <see cref="GridStyle"/> and <see cref="Theme"/>.
    /// </summary>
    public KumikoScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (KumikoScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Identifies the <see cref="HorizontalScrollBarVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility), typeof(KumikoScrollBarVisibility), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoScrollBarVisibility.Auto, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._styleOptions.HorizontalScrollBarVisibility = (KumikoScrollBarVisibility)e.NewValue;
                view.ApplyStyleOptions();
                view.Invalidate();
            }));

    /// <summary>
    /// When the horizontal scroll bar is drawn, at the bottom of the rows across the scrolling columns: the same
    /// choices as <see cref="VerticalScrollBarVisibility"/>. Until set, the style's value applies; once set, it wins
    /// over <see cref="GridStyle"/> and <see cref="Theme"/>.
    /// </summary>
    public KumikoScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (KumikoScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }
}
