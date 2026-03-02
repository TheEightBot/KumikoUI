using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class ScrollStateTests
{
    // ── MaxOffset ─────────────────────────────────────────────────

    [Fact]
    public void MaxOffsetX_ContentLargerThanViewport_IsPositive()
    {
        var scroll = new ScrollState { ContentWidth = 1000, ViewportWidth = 400 };
        Assert.Equal(600f, scroll.MaxOffsetX);
    }

    [Fact]
    public void MaxOffsetX_ContentSmallerThanViewport_IsZero()
    {
        var scroll = new ScrollState { ContentWidth = 200, ViewportWidth = 400 };
        Assert.Equal(0f, scroll.MaxOffsetX);
    }

    [Fact]
    public void MaxOffsetY_ContentLargerThanViewport_IsPositive()
    {
        var scroll = new ScrollState { ContentHeight = 2000, ViewportHeight = 600 };
        Assert.Equal(1400f, scroll.MaxOffsetY);
    }

    [Fact]
    public void MaxOffsetY_ContentSmallerThanViewport_IsZero()
    {
        var scroll = new ScrollState { ContentHeight = 100, ViewportHeight = 600 };
        Assert.Equal(0f, scroll.MaxOffsetY);
    }

    // ── ClampOffset ───────────────────────────────────────────────

    [Fact]
    public void ClampOffset_OffsetWithinBounds_Unchanged()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = 200, OffsetY = 500
        };
        scroll.ClampOffset();
        Assert.Equal(200f, scroll.OffsetX);
        Assert.Equal(500f, scroll.OffsetY);
    }

    [Fact]
    public void ClampOffset_OffsetBelowZero_ClampsToZero()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = -50, OffsetY = -100
        };
        scroll.ClampOffset();
        Assert.Equal(0f, scroll.OffsetX);
        Assert.Equal(0f, scroll.OffsetY);
    }

    [Fact]
    public void ClampOffset_OffsetAboveMax_ClampsToMax()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = 1000, OffsetY = 5000
        };
        scroll.ClampOffset();
        Assert.Equal(600f, scroll.OffsetX);
        Assert.Equal(1400f, scroll.OffsetY);
    }

    // ── ScrollBy ──────────────────────────────────────────────────

    [Fact]
    public void ScrollBy_MovesOffset()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = 100, OffsetY = 100
        };
        scroll.ScrollBy(50, 100);
        Assert.Equal(150f, scroll.OffsetX);
        Assert.Equal(200f, scroll.OffsetY);
    }

    [Fact]
    public void ScrollBy_ClampsResult()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 500, ViewportWidth = 400,
            ContentHeight = 500, ViewportHeight = 400,
            OffsetX = 80, OffsetY = 80
        };
        scroll.ScrollBy(1000, 1000); // Way past max (100, 100)
        Assert.Equal(100f, scroll.OffsetX);
        Assert.Equal(100f, scroll.OffsetY);
    }

    [Fact]
    public void ScrollBy_Negative_ClampsToZero()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = 10, OffsetY = 10
        };
        scroll.ScrollBy(-1000, -1000);
        Assert.Equal(0f, scroll.OffsetX);
        Assert.Equal(0f, scroll.OffsetY);
    }

    [Fact]
    public void ScrollBy_ZeroDeltas_DoesNotMove()
    {
        var scroll = new ScrollState
        {
            ContentWidth = 1000, ViewportWidth = 400,
            ContentHeight = 2000, ViewportHeight = 600,
            OffsetX = 100, OffsetY = 200
        };
        scroll.ScrollBy(0, 0);
        Assert.Equal(100f, scroll.OffsetX);
        Assert.Equal(200f, scroll.OffsetY);
    }
}

