using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// The host row height settings both hosts share (#320): only what the host set is written, it is written again into a
/// replaced style, and NaN limits and a null resolver clear the style's values.
/// </summary>
public class RowHeightOptionsTests
{
    [Fact]
    public void NothingSet_LeavesTheStyleAlone()
    {
        var style = new KumikoStyle { RowHeightMode = KumikoRowHeightMode.Auto, MinRowHeight = 40, CellVerticalPadding = 2 };

        new KumikoRowHeightOptions().Apply(style);

        Assert.Equal(KumikoRowHeightMode.Auto, style.RowHeightMode);
        Assert.Equal(40f, style.MinRowHeight);
        Assert.Equal(2f, style.CellVerticalPadding);
    }

    [Fact]
    public void SetValues_AreWrittenNow_AndIntoAReplacementStyle()
    {
        var options = new KumikoRowHeightOptions();
        var style = new KumikoStyle();
        KumikoRowHeightResolver resolver = (in KumikoRowHeightContext _) => 50;

        options.SetMode(KumikoRowHeightMode.Auto, style);
        options.SetMinRowHeight(28, style);
        options.SetMaxRowHeight(double.NaN, style);
        options.SetCellVerticalPadding(-3, style);
        options.SetResolver(resolver, style);

        var replacement = new KumikoStyle { MaxRowHeight = 70 };
        options.Apply(replacement);

        foreach (var s in new[] { style, replacement })
        {
            Assert.Equal(KumikoRowHeightMode.Auto, s.RowHeightMode);
            Assert.Equal(28f, s.MinRowHeight);
            Assert.Null(s.MaxRowHeight);
            Assert.Equal(0f, s.CellVerticalPadding);
            Assert.Same(resolver, s.RowHeightResolver);
        }
    }

    [Theory]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(0.25, 1f)]
    [InlineData(48.5, 48.5f)]
    public void HostLimits_MapToTheStyle(double host, float? expected) =>
        Assert.Equal(expected, KumikoRowHeightOptions.ToLimit(host));
}
