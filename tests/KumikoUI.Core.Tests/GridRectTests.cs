using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class GridRectTests
{
    // ── Construction ─────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsProperties()
    {
        var rect = new GridRect(10, 20, 100, 50);
        Assert.Equal(10f, rect.X);
        Assert.Equal(20f, rect.Y);
        Assert.Equal(100f, rect.Width);
        Assert.Equal(50f, rect.Height);
    }

    [Fact]
    public void Edges_AreCalculatedCorrectly()
    {
        var rect = new GridRect(10, 20, 100, 50);
        Assert.Equal(10f, rect.Left);
        Assert.Equal(20f, rect.Top);
        Assert.Equal(110f, rect.Right);
        Assert.Equal(70f, rect.Bottom);
    }

    [Fact]
    public void Empty_IsAllZeros()
    {
        var rect = GridRect.Empty;
        Assert.Equal(0f, rect.X);
        Assert.Equal(0f, rect.Y);
        Assert.Equal(0f, rect.Width);
        Assert.Equal(0f, rect.Height);
    }

    // ── Contains ─────────────────────────────────────────────────

    [Fact]
    public void Contains_PointInsideRect_ReturnsTrue()
    {
        var rect = new GridRect(0, 0, 100, 100);
        Assert.True(rect.Contains(50, 50));
    }

    [Fact]
    public void Contains_PointOnLeftEdge_ReturnsTrue()
    {
        var rect = new GridRect(10, 10, 80, 80);
        Assert.True(rect.Contains(10, 50));
    }

    [Fact]
    public void Contains_PointOnRightEdge_ReturnsTrue()
    {
        var rect = new GridRect(10, 10, 80, 80);
        Assert.True(rect.Contains(90, 50));
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var rect = new GridRect(0, 0, 100, 100);
        Assert.False(rect.Contains(150, 50));
        Assert.False(rect.Contains(50, 150));
        Assert.False(rect.Contains(-1, 50));
        Assert.False(rect.Contains(50, -1));
    }

    // ── Inflate ───────────────────────────────────────────────────

    [Fact]
    public void Inflate_ExpandsBothAxes()
    {
        var rect = new GridRect(10, 20, 100, 50);
        var inflated = rect.Inflate(5, 10);
        Assert.Equal(5f, inflated.X);
        Assert.Equal(10f, inflated.Y);
        Assert.Equal(110f, inflated.Width);
        Assert.Equal(70f, inflated.Height);
    }

    [Fact]
    public void Inflate_ZeroDeltas_ReturnsSameRect()
    {
        var rect = new GridRect(10, 20, 100, 50);
        var inflated = rect.Inflate(0, 0);
        Assert.Equal(rect.X, inflated.X);
        Assert.Equal(rect.Y, inflated.Y);
        Assert.Equal(rect.Width, inflated.Width);
        Assert.Equal(rect.Height, inflated.Height);
    }

    [Fact]
    public void Inflate_NegativeDeltas_ShrinksRect()
    {
        var rect = new GridRect(0, 0, 100, 100);
        var inflated = rect.Inflate(-10, -10);
        Assert.Equal(10f, inflated.X);
        Assert.Equal(10f, inflated.Y);
        Assert.Equal(80f, inflated.Width);
        Assert.Equal(80f, inflated.Height);
    }

    // ── Offset ────────────────────────────────────────────────────

    [Fact]
    public void Offset_ShiftsPosition_KeepsSize()
    {
        var rect = new GridRect(10, 20, 100, 50);
        var shifted = rect.Offset(5, -10);
        Assert.Equal(15f, shifted.X);
        Assert.Equal(10f, shifted.Y);
        Assert.Equal(100f, shifted.Width);
        Assert.Equal(50f, shifted.Height);
    }

    [Fact]
    public void Offset_ZeroDeltas_ReturnsSameValues()
    {
        var rect = new GridRect(10, 20, 100, 50);
        var shifted = rect.Offset(0, 0);
        Assert.Equal(rect.X, shifted.X);
        Assert.Equal(rect.Y, shifted.Y);
    }
}

