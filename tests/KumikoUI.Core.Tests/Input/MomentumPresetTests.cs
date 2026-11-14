using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Per-platform momentum presets (#101): iOS coasts furthest, Android settles sooner, desktop needs a deliberate
/// flick and coasts least; each preset is a fresh instance with the other settings at their defaults.
/// </summary>
public class MomentumPresetTests
{
    /// <summary>How far a fling released at <paramref name="speed"/> px/s travels, stepped at 60 Hz.</summary>
    private static float Coast(KumikoMomentumPreset preset, float speed)
    {
        var scroller = new InertialScroller { Settings = ScrollSettings.FromPreset(preset) };
        // Moves 16 ms apart at the given speed
        for (int i = 0; i <= 5; i++)
            scroller.TrackVelocity(0, speed * 0.016f, 1000 + i * 16);
        scroller.StartFling(1000 + 5 * 16);

        float travelled = 0;
        for (int frame = 0; frame < 2000 && scroller.IsActive; frame++)
            travelled += scroller.Update(16f).dy;
        Assert.False(scroller.IsActive);
        return travelled;
    }

    [Fact]
    public void IosCoastsFurthest_ThenAndroid_ThenDesktop()
    {
        float ios = Coast(KumikoMomentumPreset.IOS, 3000);
        float android = Coast(KumikoMomentumPreset.Android, 3000);
        float desktop = Coast(KumikoMomentumPreset.Desktop, 3000);

        Assert.True(ios > android, $"iOS {ios} vs Android {android}");
        Assert.True(android > desktop, $"Android {android} vs desktop {desktop}");
    }

    [Fact]
    public void Desktop_IgnoresASlowFlick_ThatAPhoneCoastsOn()
    {
        Assert.Equal(0, Coast(KumikoMomentumPreset.Desktop, 80));
        Assert.True(Coast(KumikoMomentumPreset.IOS, 80) > 0);
        Assert.True(Coast(KumikoMomentumPreset.Android, 80) > 0);
    }

    [Theory]
    [InlineData(KumikoMomentumPreset.IOS)]
    [InlineData(KumikoMomentumPreset.Android)]
    [InlineData(KumikoMomentumPreset.Desktop)]
    public void APreset_TunesOnlyTheMomentum(KumikoMomentumPreset preset)
    {
        var defaults = new ScrollSettings();
        var settings = ScrollSettings.FromPreset(preset);

        Assert.NotSame(settings, ScrollSettings.FromPreset(preset));
        Assert.True(settings.EnableInertia);
        Assert.Equal(defaults.FlingHoldCutoffMs, settings.FlingHoldCutoffMs);
        Assert.Equal(defaults.AutoScrollEdgeSize, settings.AutoScrollEdgeSize);
        Assert.Equal(defaults.AutoScrollMaxSpeed, settings.AutoScrollMaxSpeed);
        Assert.Equal(defaults.VelocitySmoothing, settings.VelocitySmoothing);
    }

    [Fact]
    public void Default_IsTheScrollSettingsDefaults()
    {
        var settings = ScrollSettings.FromPreset(KumikoMomentumPreset.Default);

        Assert.Equal(new ScrollSettings().Friction, settings.Friction);
        Assert.Equal(new ScrollSettings().MinVelocity, settings.MinVelocity);
    }
}
