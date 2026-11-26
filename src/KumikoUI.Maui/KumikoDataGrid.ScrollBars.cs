using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// The grid's scroll bars (#316): when each one is drawn. They are style settings
/// (<see cref="KumikoStyle.VerticalScrollBarVisibility"/>, <see cref="KumikoStyle.HorizontalScrollBarVisibility"/>);
/// a value set here is written into the current style and re-applied after <see cref="GridStyle"/> or
/// <see cref="Theme"/> is replaced, so it survives a theme change. Their size and colors are on <see cref="KumikoStyle"/>.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="VerticalScrollBarVisibility"/> bindable property.</summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(
        nameof(VerticalScrollBarVisibility), typeof(KumikoScrollBarVisibility), typeof(KumikoDataGrid),
        KumikoScrollBarVisibility.Auto,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.VerticalScrollBarVisibility = (KumikoScrollBarVisibility)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

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

    /// <summary>Identifies the <see cref="HorizontalScrollBarVisibility"/> bindable property.</summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create(
        nameof(HorizontalScrollBarVisibility), typeof(KumikoScrollBarVisibility), typeof(KumikoDataGrid),
        KumikoScrollBarVisibility.Auto,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.HorizontalScrollBarVisibility = (KumikoScrollBarVisibility)n;
            view.ApplyStyleOptions();
            view.InvalidateSurface();
        });

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
