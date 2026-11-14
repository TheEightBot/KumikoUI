using KumikoUI.Core.Rendering;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Images and styling: an <c>Image</c> column, the icon set (<c>KumikoStyle.IconProvider</c>, built-in or
/// <c>KumikoFontIconProvider</c>) and per-cell and per-row styles (<c>CellStyleResolver</c>,
/// <c>RowStyleResolver</c>, <c>AlternateRowBackground</c>).
/// </summary>
public partial class StylingPage : ContentPage
{
    // Every built-in glyph drawn from the Material Icons font registered in MauiProgram
    private static readonly KumikoFontIconProvider MaterialIcons = KumikoFontIconProvider.CreateMaterialIcons("MaterialIcons");

    private bool _materialIcons;
    private bool _resolvers = true;
    private bool _stripes;

    public StylingPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = ShowcaseData.CreateRows(500, n => $"Avatars/avatar-{n}.png");
        ApplyStyle();
    }

    // A style change assigns a style back to the grid; the resolvers live on the style.
    private void ApplyStyle()
    {
        var style = kumiko.GridStyle;
        style.IconProvider = _materialIcons ? MaterialIcons : KumikoIconProvider.Default;
        if (_resolvers) ShowcaseData.Apply(style); else ShowcaseData.Clear(style);
        kumiko.GridStyle = style;
    }

    private void OnToggleIconsClicked(object? sender, EventArgs e)
    {
        _materialIcons = !_materialIcons;
        iconsToggle.Text = _materialIcons ? "Icons: Material" : "Icons: Built-in";
        ApplyStyle();
    }

    private void OnToggleResolversClicked(object? sender, EventArgs e)
    {
        _resolvers = !_resolvers;
        resolversToggle.Text = _resolvers ? "Cell and row styles: ON" : "Cell and row styles: OFF";
        ApplyStyle();
    }

    private void OnToggleStripesClicked(object? sender, EventArgs e)
    {
        _stripes = !_stripes;
        stripesToggle.Text = _stripes ? "Striped rows: ON" : "Striped rows: OFF";
        kumiko.AlternateRowBackground = _stripes ? Color.FromArgb("#EEF2FF") : null;
    }
}
