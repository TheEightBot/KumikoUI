using KumikoUI.Uno.Rendering;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Unit tests for <see cref="KumikoSurfaceMetrics"/>: mapping the software surface's device-pixel bitmap to logical
/// units, and detecting real rasterization-scale changes (#65, UNO-18).
/// </summary>
public class KumikoSurfaceMetricsTests
{
    [Theory]
    [InlineData(800, 600, 800.0, 600.0, 1f)]
    [InlineData(1600, 1200, 800.0, 600.0, 2f)]
    [InlineData(1200, 900, 800.0, 600.0, 1.5f)]
    public void FromPixels_WholeScale_ScalesBothAxesAndKeepsTheLogicalSize(
        int pixelWidth, int pixelHeight, double logicalWidth, double logicalHeight, float expectedScale)
    {
        var metrics = KumikoSurfaceMetrics.FromPixels(pixelWidth, pixelHeight, logicalWidth, logicalHeight, 1.0);

        Assert.Equal(expectedScale, metrics.ScaleX, precision: 4);
        Assert.Equal(expectedScale, metrics.ScaleY, precision: 4);
        Assert.Equal((float)logicalWidth, metrics.Width);
        Assert.Equal((float)logicalHeight, metrics.Height);
        Assert.False(metrics.IsEmpty);
    }

    [Fact]
    public void FromPixels_TruncatedBitmap_UsesAPerAxisScaleSoTheDrawingFillsTheBitmap()
    {
        // 1.25 × 333.3 = 416.6 → the bitmap is 416 pixels wide; 1.25 × 201 = 251.25 → 251 high.
        var metrics = KumikoSurfaceMetrics.FromPixels(416, 251, 333.3, 201.0, 1.25);

        Assert.Equal(416f, metrics.ScaleX * metrics.Width, precision: 2);
        Assert.Equal(251f, metrics.ScaleY * metrics.Height, precision: 2);
        Assert.NotEqual(metrics.ScaleX, metrics.ScaleY);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-10.0)]
    public void FromPixels_UnusableLogicalSize_FallsBackToTheRasterizationScale(double logical)
    {
        var metrics = KumikoSurfaceMetrics.FromPixels(400, 300, logical, logical, 2.0);

        Assert.Equal(2f, metrics.ScaleX);
        Assert.Equal(2f, metrics.ScaleY);
        Assert.Equal(200f, metrics.Width);
        Assert.Equal(150f, metrics.Height);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    public void FromPixels_UnusableFallbackScale_UsesOne(double fallback)
    {
        var metrics = KumikoSurfaceMetrics.FromPixels(400, 300, double.NaN, double.NaN, fallback);

        Assert.Equal(1f, metrics.ScaleX);
        Assert.Equal(400f, metrics.Width);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(400, 0)]
    [InlineData(0, 0)]
    public void FromPixels_NoPixels_IsEmpty(int pixelWidth, int pixelHeight)
    {
        var metrics = KumikoSurfaceMetrics.FromPixels(pixelWidth, pixelHeight, 200.0, 150.0, 2.0);

        Assert.True(metrics.IsEmpty);
    }

    [Theory]
    [InlineData(1.0, 2.0, true)]     // moved to a 200 % display
    [InlineData(2.0, 1.5, true)]
    [InlineData(1.0, 1.0, false)]
    [InlineData(1.5, 1.5004, false)] // rounding noise
    [InlineData(0.0, 1.0, true)]     // first real value
    [InlineData(double.NaN, 1.25, true)]
    [InlineData(1.0, 0.0, false)]    // an unusable new value is ignored
    [InlineData(1.0, double.NaN, false)]
    public void ScaleChanged_DetectsRealChangesOnly(double previous, double current, bool expected)
    {
        Assert.Equal(expected, KumikoSurfaceMetrics.ScaleChanged(previous, current));
    }
}
