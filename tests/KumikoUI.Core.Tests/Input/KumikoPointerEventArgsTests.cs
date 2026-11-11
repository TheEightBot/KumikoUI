using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests.Input;

/// <summary>Converting a wheel delta to pixels (#60): notches scale, pixels pass through.</summary>
public class KumikoPointerEventArgsTests
{
    [Fact]
    public void ScrollUnit_DefaultsToNotches()
    {
        Assert.Equal(KumikoScrollUnit.Notches, new KumikoPointerEventArgs().ScrollUnit);
    }

    [Theory]
    [InlineData(1f, 108f)]
    [InlineData(-0.25f, -27f)]
    [InlineData(0f, 0f)]
    public void GetScrollPixels_Notches_MultipliesByThePixelsPerNotch(float delta, float expected)
    {
        var e = new KumikoPointerEventArgs { ScrollDeltaX = delta, ScrollDeltaY = delta };

        Assert.Equal(expected, e.GetScrollPixelsX(108));
        Assert.Equal(expected, e.GetScrollPixelsY(108));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-13.5f)]
    public void GetScrollPixels_Pixels_AreReturnedAsIs(float delta)
    {
        var e = new KumikoPointerEventArgs { ScrollDeltaX = delta, ScrollDeltaY = -delta, ScrollUnit = KumikoScrollUnit.Pixels };

        Assert.Equal(delta, e.GetScrollPixelsX(108));
        Assert.Equal(-delta, e.GetScrollPixelsY(108));
    }
}
