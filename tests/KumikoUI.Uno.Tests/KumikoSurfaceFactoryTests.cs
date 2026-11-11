using KumikoUI.Uno.Rendering;

namespace KumikoUI.Uno.Tests;

/// <summary>Unit tests for <see cref="KumikoSurfaceFactory.Resolve"/>: which surface a <see cref="KumikoRenderMode"/> gets (#65).</summary>
public class KumikoSurfaceFactoryTests
{
    [Theory]
    [InlineData(KumikoRenderMode.Auto, true, KumikoRenderMode.Hardware)]
    [InlineData(KumikoRenderMode.Auto, false, KumikoRenderMode.Software)]
    [InlineData(KumikoRenderMode.Hardware, true, KumikoRenderMode.Hardware)]
    [InlineData(KumikoRenderMode.Hardware, false, KumikoRenderMode.Software)] // unavailable: falls back
    [InlineData(KumikoRenderMode.Software, true, KumikoRenderMode.Software)]
    [InlineData(KumikoRenderMode.Software, false, KumikoRenderMode.Software)]
    public void Resolve_PicksTheSurface(KumikoRenderMode requested, bool hardwareSupported, KumikoRenderMode expected)
    {
        Assert.Equal(expected, KumikoSurfaceFactory.Resolve(requested, hardwareSupported));
    }
}
