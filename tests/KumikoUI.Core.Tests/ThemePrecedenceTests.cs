using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>Height precedence behind the hosts' bindable Theme/GridStyle (#57): explicit heights beat the style, in either order.</summary>
public class ThemePrecedenceTests
{
    [Fact]
    public void HeightSetBeforeTheTheme_SurvivesTheThemeChange()
    {
        var options = new KumikoStyleOptions { RowHeight = 52f, HeaderHeight = 60f };
        var light = KumikoTheme.Create(KumikoThemeMode.Light);
        options.Apply(light);

        var dark = KumikoTheme.Create(KumikoThemeMode.Dark);
        options.Apply(dark);

        Assert.Equal(52f, dark.RowHeight);
        Assert.Equal(60f, dark.HeaderHeight);
    }

    [Fact]
    public void HeightSetAfterTheTheme_AppliesToTheThemeStyle()
    {
        var options = new KumikoStyleOptions();
        var dark = KumikoTheme.Create(KumikoThemeMode.Dark);
        options.Apply(dark);

        options.RowHeight = 28f;
        options.Apply(dark);

        Assert.Equal(28f, dark.RowHeight);
        Assert.Equal(new KumikoStyle().HeaderHeight, dark.HeaderHeight);   // not set: the style's value stays
    }

    [Fact]
    public void HeightsAreReappliedToAnAssignedStyleInstance()
    {
        var options = new KumikoStyleOptions { RowHeight = 44f };
        var custom = new KumikoStyle { RowHeight = 20f, HeaderHeight = 31f };

        options.Apply(custom);

        Assert.Equal(44f, custom.RowHeight);
        Assert.Equal(31f, custom.HeaderHeight);
    }

    [Fact]
    public void HeightOverrides_CountAsOverrides_AndBumpTheStyleVersion()
    {
        var options = new KumikoStyleOptions();
        Assert.False(options.HasOverrides);

        options.HeaderHeight = 50f;
        Assert.True(options.HasOverrides);

        var style = new KumikoStyle();
        var version = style.Version;
        options.Apply(style);
        Assert.True(style.Version > version);
    }
}
