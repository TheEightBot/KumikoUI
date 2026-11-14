using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;

namespace SampleApp.Uno;

/// <summary>
/// Images and styling (← SampleApp.Maui StylingPage): an <c>Image</c> column, the icon set
/// (<c>KumikoStyle.IconProvider</c>, built-in or <c>KumikoFontIconProvider</c>) and per-cell and per-row styles
/// (<c>CellStyleResolver</c>, <c>RowStyleResolver</c>, <c>AlternateRowBackground</c>).
/// </summary>
public sealed partial class StylingPage : Page
{
    // Every built-in glyph drawn from the Material Icons font registered in App.OnLaunched (KumikoFonts)
    private static readonly KumikoFontIconProvider MaterialIcons = KumikoFontIconProvider.CreateMaterialIcons("MaterialIcons");

    private bool _materialIcons;
    private bool _resolvers = true;
    private bool _stripes;

    public StylingPage()
    {
        this.InitializeComponent();
        kumiko.ItemsSource = ShowcaseData.CreateRows(500, n => $"Assets/Avatars/avatar-{n}.png");
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

    private void OnToggleIconsClicked(object sender, RoutedEventArgs e)
    {
        _materialIcons = !_materialIcons;
        iconsToggle.Content = _materialIcons ? "Icons: Material" : "Icons: Built-in";
        ApplyStyle();
    }

    private void OnToggleResolversClicked(object sender, RoutedEventArgs e)
    {
        _resolvers = !_resolvers;
        resolversToggle.Content = _resolvers ? "Cell and row styles: ON" : "Cell and row styles: OFF";
        ApplyStyle();
    }

    private void OnToggleStripesClicked(object sender, RoutedEventArgs e)
    {
        _stripes = !_stripes;
        stripesToggle.Content = _stripes ? "Striped rows: ON" : "Striped rows: OFF";
        kumiko.AlternateRowBackground = _stripes ? Windows.UI.Color.FromArgb(255, 0xEE, 0xF2, 0xFF) : (Windows.UI.Color?)null;
    }
}
