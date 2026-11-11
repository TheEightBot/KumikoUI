using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>Precedence rules behind the hosts' Theme, GridStyle and FollowSystemTheme (#57).</summary>
public class KumikoAppearanceTests
{
    private const KumikoThemeMode Dark = KumikoThemeMode.Dark;
    private const KumikoThemeMode Light = KumikoThemeMode.Light;

    [Fact]
    public void Default_FollowsTheSystem_AndAppliesIt()
    {
        var a = new KumikoAppearance();
        Assert.True(a.ShouldFollowSystem);
        Assert.Equal(Dark, a.SystemThemeChanged(Dark));
        Assert.Null(a.SystemThemeChanged(Dark));
    }

    [Fact]
    public void AssigningTheme_StopsFollowing_EvenWhenEqualToTheDefault()
    {
        var a = new KumikoAppearance();
        a.SystemThemeChanged(Dark);

        Assert.Equal(Light, a.ThemeAssigned(Light));   // default value, system is dark: still applied
        Assert.False(a.ShouldFollowSystem);
        Assert.Null(a.SystemThemeChanged(Dark));
    }

    [Fact]
    public void AssigningTheSameThemeAfterACustomStyle_ReplacesTheStyle()
    {
        var a = new KumikoAppearance();
        a.ThemeAssigned(Dark);
        Assert.Null(a.ThemeAssigned(Dark));            // untouched preset: nothing to do

        a.StyleAssigned();                             // custom GridStyle replaces the preset
        Assert.Null(a.AppliedTheme);
        Assert.Equal(Dark, a.ThemeAssigned(Dark));     // Theme = Dark again must apply the preset
        Assert.Equal(Dark, a.AppliedTheme);
    }

    [Fact]
    public void AssigningStyle_StopsFollowing()
    {
        var a = new KumikoAppearance();
        a.StyleAssigned();
        Assert.False(a.ShouldFollowSystem);
        Assert.Null(a.SystemThemeChanged(Dark));
    }

    [Fact]
    public void ExplicitFollow_WinsInEitherOrder()
    {
        var before = new KumikoAppearance();
        before.FollowAssigned(true, Light, Dark);
        before.ThemeAssigned(Light);
        Assert.True(before.ShouldFollowSystem);

        var after = new KumikoAppearance();
        after.ThemeAssigned(Light);
        Assert.Equal(Dark, after.FollowAssigned(true, Light, Dark));
        Assert.True(after.ShouldFollowSystem);
    }

    [Fact]
    public void TurningFollowOff_ShowsTheTheme_ButKeepsACustomStyle()
    {
        var a = new KumikoAppearance();
        a.SystemThemeChanged(Dark);
        Assert.Equal(Light, a.FollowAssigned(false, Light, Dark));

        var custom = new KumikoAppearance();
        custom.StyleAssigned();
        Assert.Null(custom.FollowAssigned(false, Light, Dark));
    }

    [Fact]
    public void NullStyle_ResolvesToSystemWhileFollowing_ElseTheme()
    {
        var a = new KumikoAppearance();
        Assert.Equal(Dark, a.Resolve(Light, Dark));

        a.ThemeAssigned(Light);
        Assert.Equal(Light, a.Resolve(Light, Dark));
    }
}
