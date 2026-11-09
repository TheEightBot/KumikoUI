using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// <see cref="PopupPlacement"/>: where <see cref="PopupManager.Show(DrawnComponent, KumikoRect, PopupPlacement)"/>
/// puts a popup relative to its anchor for each placement, and how it sizes and layers the popup.
/// </summary>
public class PopupPlacementTests
{
    private static readonly KumikoRect Anchor = new(200, 100, 80, 30);

    private static ProbeComponent Popup(float width = 100, float height = 50) =>
        new() { Bounds = new KumikoRect(0, 0, width, height) };

    [Theory]
    [InlineData(PopupPlacement.Below, 200, 130)]
    [InlineData(PopupPlacement.Above, 200, 50)]
    [InlineData(PopupPlacement.Right, 280, 100)]
    [InlineData(PopupPlacement.Left, 100, 100)]
    public void Show_PlacesThePopupNextToTheAnchor(PopupPlacement placement, float x, float y)
    {
        var popups = new PopupManager();
        var popup = Popup();

        popups.Show(popup, Anchor, placement);

        Assert.Equal(new KumikoRect(x, y, 100, 50), popup.Bounds);
        Assert.True(popup.IsVisible);
        Assert.True(popups.HasActivePopups);
    }

    [Fact]
    public void Show_DefaultsToBelow()
    {
        var popups = new PopupManager();
        var popup = Popup();

        popups.Show(popup, Anchor);

        Assert.Equal(new KumikoRect(200, 130, 100, 50), popup.Bounds);
    }

    [Theory]
    [InlineData(PopupPlacement.Below, 200, 130)]
    [InlineData(PopupPlacement.Above, 200, -100)]
    public void Show_WithoutASize_UsesTheAnchorWidthAndADefaultHeight(PopupPlacement placement, float x, float y)
    {
        var popups = new PopupManager();
        var popup = new ProbeComponent();

        popups.Show(popup, Anchor, placement);

        Assert.Equal(new KumikoRect(x, y, 80, 200), popup.Bounds);
    }

    [Fact]
    public void Show_StacksLaterPopupsAboveEarlierOnes()
    {
        var popups = new PopupManager();
        var first = Popup();
        var second = Popup();

        popups.Show(first, Anchor);
        popups.Show(second, Anchor);

        Assert.True(second.ZOrder > first.ZOrder);
        Assert.Same(second, popups.HitTest(210, 140));
    }

    [Fact]
    public void ADrawnPopup_FollowsAnAnchorThatMoves()
    {
        var popups = new PopupManager();
        var popup = Popup();
        var anchor = Anchor;
        popups.Show(popup, () => anchor, PopupPlacement.Right);

        anchor = new KumikoRect(300, 200, 80, 30);
        popups.DrawPopups(new RecordingDrawingContext());

        Assert.Equal(new KumikoRect(380, 200, 100, 50), popup.Bounds);
    }

    [Fact]
    public void ClosingAPopup_HidesItAndReportsIt()
    {
        var popups = new PopupManager();
        var popup = Popup();
        DrawnComponent? closed = null;
        popups.PopupClosed += c => closed = c;
        popups.Show(popup, Anchor, PopupPlacement.Left);

        popups.Close(popup);

        Assert.Same(popup, closed);
        Assert.False(popup.IsVisible);
        Assert.False(popups.HasActivePopups);
    }
}
