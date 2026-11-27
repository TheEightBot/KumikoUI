using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>
/// The empty view (#326): the message drawn in the data area while the view has no rows, "No items to show" when the
/// collection is empty and "No rows match the current filters" when the filters or the search hide every row. The texts
/// are <see cref="KumikoStrings.EmptyViewNoItems"/> and <see cref="KumikoStrings.EmptyViewNoMatches"/>; the style carries
/// the colors and the font (<see cref="KumikoStyle.EmptyViewTextColor"/>, <see cref="KumikoStyle.EmptyViewFont"/>).
/// </summary>
public partial class KumikoDataGrid
{
    private readonly EmptyViewAnnouncer _emptyViewAnnouncer = new();

    /// <summary>Identifies the <see cref="ShowEmptyView"/> bindable property.</summary>
    public static readonly BindableProperty ShowEmptyViewProperty = BindableProperty.Create(
        nameof(ShowEmptyView), typeof(bool?), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._styleOptions.ShowEmptyView = (bool?)n;
            view.ApplyStyleOptions();
            view.UpdateAccessibilityHint();
            view.InvalidateSurface();
        });

    /// <summary>
    /// Whether the grid draws the empty view while the view has no rows. Null (the default) keeps the style's value
    /// (<see cref="KumikoStyle.ShowEmptyView"/>, on); a value wins over <see cref="GridStyle"/> and <see cref="Theme"/>,
    /// also after either is replaced.
    /// </summary>
    public bool? ShowEmptyView
    {
        get => (bool?)GetValue(ShowEmptyViewProperty);
        set => SetValue(ShowEmptyViewProperty, value);
    }

    /// <summary>Identifies the <see cref="EmptyViewText"/> bindable property.</summary>
    public static readonly BindableProperty EmptyViewTextProperty = BindableProperty.Create(
        nameof(EmptyViewText), typeof(string), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._renderer.EmptyViewText = (string?)n;
            view.UpdateAccessibilityHint();
            view.InvalidateSurface();
        });

    /// <summary>
    /// The empty view's text, replacing both <see cref="KumikoStrings.EmptyViewNoItems"/> and
    /// <see cref="KumikoStrings.EmptyViewNoMatches"/>. Null (the default) uses the strings, which follow the localization.
    /// </summary>
    public string? EmptyViewText
    {
        get => (string?)GetValue(EmptyViewTextProperty);
        set => SetValue(EmptyViewTextProperty, value);
    }

    /// <summary>Identifies the <see cref="EmptyViewRenderer"/> bindable property.</summary>
    public static readonly BindableProperty EmptyViewRendererProperty = BindableProperty.Create(
        nameof(EmptyViewRenderer), typeof(IKumikoEmptyViewRenderer), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._renderer.EmptyViewRenderer = (IKumikoEmptyViewRenderer?)n;
            view.InvalidateSurface();
        });

    /// <summary>
    /// Draws the empty view instead of the built-in centered text. It gets the data area, the reason there are no rows
    /// and the text the built-in view would draw, and must not allocate per frame. Null (the default) draws the text.
    /// </summary>
    public IKumikoEmptyViewRenderer? EmptyViewRenderer
    {
        get => (IKumikoEmptyViewRenderer?)GetValue(EmptyViewRendererProperty);
        set => SetValue(EmptyViewRendererProperty, value);
    }

    /// <summary>The empty text a screen reader hears in place of the size of the grid while the view has no rows, else null.</summary>
    private string? GetEmptyViewHint() => EmptyViewState.CurrentText(_dataSource, _style, EmptyViewText, _renderer.IsBusy);

    /// <summary>After a structural view change: announces the empty text once when a filter or search just emptied the view.</summary>
    private void AnnounceEmptyView()
    {
        if (_emptyViewAnnouncer.Observe(_dataSource, _style, EmptyViewText, _renderer.IsBusy) is { } text)
            Announce(text);
    }
}
