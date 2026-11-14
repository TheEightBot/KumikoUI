using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Tests for <see cref="MauiActionButtonDefinition"/>: its MAUI <c>Color</c> properties reach the renderer as
/// <see cref="KumikoColor"/> values through <see cref="IActionButtonDefinition"/> (#112).
/// </summary>
public class MauiActionButtonDefinitionTests
{
    [Fact]
    public void Defaults_AreTheBlueButtonWithWhiteText()
    {
        IActionButtonDefinition core = new MauiActionButtonDefinition();

        Assert.Equal(new KumikoColor(13, 110, 253), core.BackgroundColor);
        Assert.Equal(KumikoColor.White, core.TextColor);
    }

    [Fact]
    public void ColorChanges_ReachTheCoreColors()
    {
        var definition = new MauiActionButtonDefinition
        {
            BackgroundColor = Color.FromRgba(220, 53, 69, 128),
            TextColor = Colors.Black,
        };
        IActionButtonDefinition core = definition;

        Assert.Equal(new KumikoColor(220, 53, 69, 128), core.BackgroundColor);
        Assert.Equal(new KumikoColor(0, 0, 0), core.TextColor);
    }

    [Fact]
    public void HexText_ConvertsLikeAnyMauiColor()
    {
        var definition = new MauiActionButtonDefinition { BackgroundColor = Color.FromArgb("#0D6EFD") };

        Assert.Equal(new KumikoColor(13, 110, 253), ((IActionButtonDefinition)definition).BackgroundColor);
    }
}
