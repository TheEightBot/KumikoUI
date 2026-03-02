using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

public class GridColorTests
{
    // ── Construction ─────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsComponents()
    {
        var color = new GridColor(10, 20, 30, 200);
        Assert.Equal(10, color.R);
        Assert.Equal(20, color.G);
        Assert.Equal(30, color.B);
        Assert.Equal(200, color.A);
    }

    [Fact]
    public void Constructor_DefaultAlpha_IsOpaque()
    {
        var color = new GridColor(10, 20, 30);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void FromArgb_SetsComponentsCorrectly()
    {
        var color = GridColor.FromArgb(128, 10, 20, 30);
        Assert.Equal(10, color.R);
        Assert.Equal(20, color.G);
        Assert.Equal(30, color.B);
        Assert.Equal(128, color.A);
    }

    [Fact]
    public void FromRgb_IsFullyOpaque()
    {
        var color = GridColor.FromRgb(10, 20, 30);
        Assert.Equal(255, color.A);
    }

    // ── Static members ────────────────────────────────────────────

    [Fact]
    public void Static_Black_IsBlackOpaque()
    {
        var c = GridColor.Black;
        Assert.Equal(0, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void Static_White_IsWhiteOpaque()
    {
        var c = GridColor.White;
        Assert.Equal(255, c.R);
        Assert.Equal(255, c.G);
        Assert.Equal(255, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void Static_Transparent_HasZeroAlpha()
    {
        var c = GridColor.Transparent;
        Assert.Equal(0, c.A);
    }

    [Fact]
    public void Static_Red_IsCorrect()
    {
        var c = GridColor.Red;
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
    }

    [Fact]
    public void Static_Blue_IsCorrect()
    {
        var c = GridColor.Blue;
        Assert.Equal(0, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(255, c.B);
    }

    // ── WithAlpha ─────────────────────────────────────────────────

    [Fact]
    public void WithAlpha_ReturnsNewColorWithSameRgbAndNewAlpha()
    {
        var original = new GridColor(100, 150, 200, 255);
        var modified = original.WithAlpha(50);
        Assert.Equal(100, modified.R);
        Assert.Equal(150, modified.G);
        Assert.Equal(200, modified.B);
        Assert.Equal(50, modified.A);
    }

    [Fact]
    public void WithAlpha_DoesNotMutateOriginal()
    {
        var original = new GridColor(100, 150, 200, 255);
        _ = original.WithAlpha(50);
        Assert.Equal(255, original.A);
    }

    // ── Equality ──────────────────────────────────────────────────

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new GridColor(10, 20, 30, 40);
        var b = new GridColor(10, 20, 30, 40);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new GridColor(10, 20, 30, 40);
        var b = new GridColor(10, 20, 30, 41);
        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_EqualColors_SameHash()
    {
        var a = new GridColor(10, 20, 30, 40);
        var b = new GridColor(10, 20, 30, 40);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ── ToString ──────────────────────────────────────────────────

    [Fact]
    public void ToString_ContainsAllComponents()
    {
        var color = new GridColor(10, 20, 30, 40);
        var str = color.ToString();
        Assert.Contains("10", str);
        Assert.Contains("20", str);
        Assert.Contains("30", str);
        Assert.Contains("40", str);
    }

    // ── ToUint32 ──────────────────────────────────────────────────

    [Fact]
    public void ToUint32_PacksArgbCorrectly()
    {
        var color = new GridColor(0x12, 0x34, 0x56, 0x78); // RGBA
        uint packed = color.ToUint32();
        // Expected: A=0x78, R=0x12, G=0x34, B=0x56
        uint expected = (0x78u << 24) | (0x12u << 16) | (0x34u << 8) | 0x56u;
        Assert.Equal(expected, packed);
    }

    [Fact]
    public void ToUint32_Black_IsZeroWithFullAlpha()
    {
        var packed = GridColor.Black.ToUint32();
        Assert.Equal(0xFF000000u, packed);
    }

    [Fact]
    public void ToUint32_White_IsAllOnes()
    {
        var packed = GridColor.White.ToUint32();
        Assert.Equal(0xFFFFFFFFu, packed);
    }
}

