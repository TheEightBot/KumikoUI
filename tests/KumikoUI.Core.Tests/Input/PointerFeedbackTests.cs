using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Hover and pressed feedback driven by Core input (#101): a mouse hovering a sortable header, a filter icon, a group
/// chip or its remove button, a group chevron, a check box or an action button gets the style's hover overlay; a
/// press (any pointer) gets the pressed overlay while the pointer stays on its target. The controller redraws only when
/// the target changes, and the cursor agrees with the feedback.
/// </summary>
public class PointerFeedbackTests
{
    private static readonly KumikoColor Hover = new(1, 2, 3, 40);
    private static readonly KumikoColor Pressed = new(4, 5, 6, 80);

    private static InputGrid Create(Action<InputGrid>? configure = null, int rows = 30) =>
        new(rows, viewportWidth: 700, viewportHeight: 400, configure: g =>
        {
            g.Style.HoverOverlayColor = Hover;
            g.Style.PressedOverlayColor = Pressed;
            configure?.Invoke(g);
        });

    private static List<DrawOp> Overlays(RecordingDrawingContext ctx, KumikoColor color) =>
        ctx.Fills.Where(o => o.Color == color).ToList();

    private static KumikoPointerEventArgs Touch(InputGrid g, InputAction action, float x, float y) =>
        g.Pointer(action, x, y, deviceType: KumikoPointerDeviceType.Touch);

    // ── Headers ──

    [Fact]
    public void HoveringASortableHeader_DrawsTheHoverOverlayOverIt()
    {
        var g = Create();

        g.Hover(150, InputGrid.HeaderY); // Age: 120..200
        var overlays = Overlays(g.RenderRecorded(), Hover);

        var overlay = Assert.Single(overlays);
        Assert.Equal(new KumikoRect(120, 0, 80, g.Style.HeaderHeight).ToString(), overlay.Rect.ToString());
    }

    [Fact]
    public void HoveringAHeaderThatDoesNotSort_DrawsNothing()
    {
        var g = Create(x => x.Age.AllowSorting = false);

        g.Hover(150, InputGrid.HeaderY);

        Assert.Empty(Overlays(g.RenderRecorded(), Hover));
    }

    [Fact]
    public void MovingWithinTheSameTarget_DoesNotRedraw_MovingToAnotherDoes()
    {
        var g = Create();
        g.Hover(130, InputGrid.HeaderY);
        int redraws = g.Redraws;

        g.Hover(140, InputGrid.HeaderY);
        g.Hover(160, InputGrid.HeaderY);
        Assert.Equal(redraws, g.Redraws);

        g.Hover(250, InputGrid.HeaderY); // City
        Assert.Equal(redraws + 1, g.Redraws);
    }

    [Fact]
    public void LeavingTheGrid_ClearsTheHover()
    {
        var g = Create();
        g.Hover(150, InputGrid.HeaderY);
        int redraws = g.Redraws;

        g.Pointer(InputAction.Exited, 150, -5);

        Assert.Equal(redraws + 1, g.Redraws);
        Assert.Empty(Overlays(g.RenderRecorded(), Hover));
    }

    [Fact]
    public void PressingAHeader_DrawsThePressedOverlay_UntilTheRelease()
    {
        var g = Create();

        g.Press(150, InputGrid.HeaderY);
        Assert.Single(Overlays(g.RenderRecorded(), Pressed));

        g.Release(150, InputGrid.HeaderY);
        Assert.Empty(Overlays(g.RenderRecorded(), Pressed));
        Assert.Single(Overlays(g.RenderRecorded(), Hover)); // the mouse still hovers it
    }

    [Fact]
    public void APressDrawnPressed_OnlyWhileThePointerIsOnItsTarget()
    {
        var g = Create();
        g.Press(129, InputGrid.HeaderY); // Age, just right of Name's grip (114..126)

        g.Move(121, InputGrid.HeaderY); // onto the grip, within the drag threshold
        Assert.Empty(Overlays(g.RenderRecorded(), Pressed));

        g.Move(130, InputGrid.HeaderY);
        Assert.Single(Overlays(g.RenderRecorded(), Pressed));
    }

    [Fact]
    public void AFingerNeverHovers_ButShowsThePress_UntilItPans()
    {
        var g = Create();

        Touch(g, InputAction.Moved, 150, InputGrid.HeaderY); // a touch host never sends this, but if one did
        Assert.Empty(Overlays(g.RenderRecorded(), Hover));

        Touch(g, InputAction.Pressed, 150, InputGrid.HeaderY);
        Assert.Single(Overlays(g.RenderRecorded(), Pressed));

        Touch(g, InputAction.Moved, 150 + 40, InputGrid.HeaderY + 30); // a pan
        Assert.Empty(Overlays(g.RenderRecorded(), Pressed));
    }

    [Fact]
    public void DraggingAColumn_DropsThePressedFeedback()
    {
        var g = Create();

        g.Press(150, InputGrid.HeaderY);
        g.Move(220, InputGrid.HeaderY);

        Assert.True(g.Input.DragColumnIndex >= 0);
        Assert.Empty(Overlays(g.RenderRecorded(), Pressed));
    }

    [Fact]
    public void TransparentOverlayColors_TurnTheFeedbackOff()
    {
        var g = Create(x =>
        {
            x.Style.HoverOverlayColor = KumikoColor.Transparent;
            x.Style.PressedOverlayColor = KumikoColor.Transparent;
        });

        g.Hover(150, InputGrid.HeaderY);
        var hovered = g.RenderRecorded();
        g.Press(150, InputGrid.HeaderY);
        var pressed = g.RenderRecorded();

        Assert.DoesNotContain(hovered.Fills, o => o.Color == KumikoColor.Transparent);
        Assert.DoesNotContain(pressed.Fills, o => o.Color == KumikoColor.Transparent);
    }

    // ── Filter icon ──

    [Fact]
    public void HoveringAFilterIcon_DrawsAnOverlayBehindTheIconOnly()
    {
        var g = Create();

        g.Hover(187, InputGrid.HeaderY); // Age's icon at 182..192
        var overlay = Assert.Single(Overlays(g.RenderRecorded(), Hover));

        Assert.Equal(DrawOpKind.FillRoundRect, overlay.Kind);
        Assert.Equal(178, overlay.Rect.X, 2);
        Assert.Equal(18, overlay.Rect.Width, 2);
        Assert.Equal(KumikoCursor.Hand, g.Input.Cursor);
    }

    // ── Grouping ──

    private static InputGrid Grouped() => Create(x => x.Source.AddGroupDescription(new GroupDescription("City")), rows: 6);

    [Fact]
    public void HoveringAChip_AndItsRemoveButton_DrawTheirOwnOverlays()
    {
        var g = Grouped();
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;

        g.Hover(chip.X + 8, 20);
        var onChip = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.Equal(chip.ToString(), onChip.Rect.ToString());

        g.Hover(chip.Right - 10, 20);
        var onRemove = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.Equal(18, onRemove.Rect.Width, 2);
    }

    [Fact]
    public void HoveringAGroupHeader_HighlightsItsChevron()
    {
        var g = Grouped();

        g.Hover(300, 80 + 18); // the first group header row, away from the chevron
        var overlay = Assert.Single(Overlays(g.RenderRecorded(), Hover));

        Assert.Equal(DrawOpKind.FillRoundRect, overlay.Kind);
        Assert.True(overlay.Rect.X < 20, overlay.ToString());
    }

    // ── Cells ──

    [Fact]
    public void HoveringACheckBox_HighlightsIt_AndTheRestOfTheCellDoesNot()
    {
        var g = Create(x =>
        {
            x.Age.ColumnType = KumikoColumnType.Boolean;
            x.Age.ValueAccessor = _ => true;
            x.Age.EditTriggers = EditTrigger.DoubleTap;
        });

        g.Hover(160, g.RowY(1)); // the middle of Age's cell: the box
        var box = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.True(box.Rect.Contains(160, g.RowY(1)), box.ToString());
        Assert.Equal(KumikoCursor.Hand, g.Input.Cursor);

        g.Hover(124, g.RowY(1)); // the cell's left edge
        Assert.Empty(Overlays(g.RenderRecorded(), Hover));
        Assert.Equal(KumikoCursor.Default, g.Input.Cursor);
    }

    [Fact]
    public void HoveringAnActionButton_HighlightsThatButton_AndPressingItPressesIt()
    {
        var buttons = new ActionButtonsCellRenderer
        {
            Buttons =
            {
                new ActionButtonDefinition { Label = "Details" },
                new ActionButtonDefinition { Label = "Delete" },
            },
        };
        var g = Create(x =>
        {
            x.Name.PropertyName = "";
            x.Name.ColumnType = KumikoColumnType.Template;
            x.Name.CustomCellRenderer = buttons;
        });

        // Name's cell spans 0..120: buttons at 12..57 and 63..108
        g.Hover(30, g.RowY(2));
        var first = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.True(first.Rect.Contains(30, g.RowY(2)), first.ToString());

        int redraws = g.Redraws;
        g.Hover(90, g.RowY(2));
        Assert.Equal(redraws + 1, g.Redraws);
        var second = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.True(second.Rect.Contains(90, g.RowY(2)), second.ToString());

        g.Press(90, g.RowY(2));
        var pressed = Assert.Single(Overlays(g.RenderRecorded(), Pressed));
        Assert.Equal(second.Rect.ToString(), pressed.Rect.ToString());
    }

    [Fact]
    public void ScrollingUnderAStillMouse_MovesTheHoverWithThePointer()
    {
        var g = Create(x =>
        {
            x.Age.ColumnType = KumikoColumnType.Boolean;
            x.Age.ValueAccessor = _ => true;
        });
        g.Hover(160, g.RowY(1));

        g.Pointer(InputAction.Scroll, 160, g.RowY(1), scrollDy: -1); // three rows down

        Assert.Equal(4, g.Input.Feedback.Hover.Row);
        var overlay = Assert.Single(Overlays(g.RenderRecorded(), Hover));
        Assert.True(overlay.Rect.Contains(160, g.RowY(1)), overlay.ToString());
    }

    [Fact]
    public void ARowTarget_IsNotDrawnOnceTheGridScrolledAway()
    {
        var g = Create(x =>
        {
            x.Age.ColumnType = KumikoColumnType.Boolean;
            x.Age.ValueAccessor = _ => true;
        });
        g.Hover(160, g.RowY(1));

        g.Scroll.OffsetY = 72; // scrolled by code (a key, a binding), with no pointer event

        Assert.Empty(Overlays(g.RenderRecorded(), Hover));
    }

    [Fact]
    public void DisablingTheGrid_ClearsTheFeedback()
    {
        var g = Create();
        g.Hover(150, InputGrid.HeaderY);

        g.Input.IsEnabled = false;

        Assert.Empty(Overlays(g.RenderRecorded(), Hover));
    }
}
