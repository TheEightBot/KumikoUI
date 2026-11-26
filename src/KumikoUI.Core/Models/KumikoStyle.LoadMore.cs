using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>The look of the load-more row drawn after the last row while more rows load or can be loaded (#318).</summary>
public sealed partial class KumikoStyle
{
    /// <summary>
    /// Height in pixels of the load-more row, the band after the last row that shows the loading indicator, the
    /// "Load more" button or a failed load's error and retry. <see cref="float.NaN"/> (the default) uses
    /// <see cref="RowHeight"/>.
    /// </summary>
    public float LoadMoreRowHeight { get; set => Set(ref field, value); } = float.NaN;

    /// <summary>Background color of the load-more row. <c>null</c> (the default) uses <see cref="BackgroundColor"/>.</summary>
    public KumikoColor? LoadMoreRowBackgroundColor { get; set => Set(ref field, value); }

    /// <summary>Color of the load-more row's "Loading more..." text. <c>null</c> (the default) uses <see cref="CellTextColor"/>.</summary>
    public KumikoColor? LoadMoreTextColor { get; set => Set(ref field, value); }

    /// <summary>Font of the load-more row's texts. <c>null</c> (the default) uses <see cref="CellFont"/>.</summary>
    public KumikoFont? LoadMoreFont { get; set => Set(ref field, value); }

    /// <summary>
    /// Color of the load-more row's busy indicator, its "Load more" button text and its retry button. <c>null</c> (the
    /// default) uses <see cref="AccentColor"/>.
    /// </summary>
    public KumikoColor? LoadMoreIndicatorColor { get; set => Set(ref field, value); }

    /// <summary>Color of the error text on the load-more row after a load failed. Default: a dark red.</summary>
    public KumikoColor LoadMoreErrorTextColor { get; set => Set(ref field, value); } = new(196, 43, 28);

    /// <summary>The load-more row's height: <see cref="LoadMoreRowHeight"/>, or <see cref="RowHeight"/> when it is not a positive number.</summary>
    internal float EffectiveLoadMoreRowHeight =>
        float.IsNaN(LoadMoreRowHeight) || LoadMoreRowHeight <= 0 ? RowHeight : LoadMoreRowHeight;
}
