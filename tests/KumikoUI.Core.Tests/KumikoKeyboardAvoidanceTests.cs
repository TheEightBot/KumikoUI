using KumikoUI.Core.Editing;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The geometry shared by every host for keeping an edited cell above the on-screen keyboard (#258): the part of the
/// grid the keyboard covers, the scroll that clears the cell, and the bottom inset that lets the last rows follow.
/// </summary>
public class KumikoKeyboardAvoidanceTests
{
    [Fact]
    public void ObscuredHeight_IsTheGridPartBelowTheKeyboardsTop()
    {
        // Grid at y 100..700 in the window, keyboard from 450 to the end of the screen
        Assert.Equal(250f, KumikoKeyboardAvoidance.GetObscuredHeight(100, 700, 450, float.PositiveInfinity));
    }

    [Fact]
    public void ObscuredHeight_IsZeroWhenTheKeyboardIsBelowTheGrid()
    {
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetObscuredHeight(100, 400, 450, float.PositiveInfinity));
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetObscuredHeight(100, 450, 450, float.PositiveInfinity));
    }

    [Fact]
    public void ObscuredHeight_IsTheWholeGridWhenTheKeyboardCoversItAll()
    {
        Assert.Equal(300f, KumikoKeyboardAvoidance.GetObscuredHeight(100, 400, 50, float.PositiveInfinity));
    }

    [Fact]
    public void ObscuredHeight_IgnoresAKeyboardThatStopsShortOfTheGridsBottom()
    {
        // A floating keyboard over the middle of the grid leaves the grid's bottom rows visible
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetObscuredHeight(0, 800, 300, 600));
    }

    [Fact]
    public void ObscuredHeight_IsZeroForUnusableInput()
    {
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetObscuredHeight(0, 800, float.NaN, float.PositiveInfinity));
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetObscuredHeight(0, 0, 100, float.PositiveInfinity));
    }

    [Fact]
    public void ScrollDelta_BringsTheCellsBottomAboveTheKeyboardWithTheMargin()
    {
        // 600 high, keyboard covers 300: the visible bottom is 300; the cell ends at 380
        float delta = KumikoKeyboardAvoidance.GetScrollDelta(600, 300, 344, 380, offsetY: 0, maxOffsetY: 1000, margin: 4);
        Assert.Equal(84f, delta);
    }

    [Fact]
    public void ScrollDelta_IsZeroWhenTheCellIsAlreadyClear()
    {
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetScrollDelta(600, 300, 100, 136, 0, 1000));
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetScrollDelta(600, 0, 500, 536, 0, 1000));
    }

    [Fact]
    public void ScrollDelta_StopsAtTheEndOfTheContent()
    {
        Assert.Equal(10f, KumikoKeyboardAvoidance.GetScrollDelta(600, 300, 344, 380, offsetY: 90, maxOffsetY: 100));
        Assert.Equal(0f, KumikoKeyboardAvoidance.GetScrollDelta(600, 300, 344, 380, offsetY: 100, maxOffsetY: 100));
    }

    [Fact]
    public void ScrollDelta_KeepsTheCellsTopBelowTheHeader()
    {
        // A cell 200 high that cannot fit in the 120 visible above the keyboard: its top stays under the 40 header
        float delta = KumikoKeyboardAvoidance.GetScrollDelta(420, 300, 60, 260, 0, 1000, minTop: 40);
        Assert.Equal(20f, delta);
    }

    [Fact]
    public void BottomInset_LetsTheLastRowScrollAboveTheKeyboard()
    {
        // 20 rows of 36 plus a 40 header make 760 in a 400 viewport: the last row's bottom is the content's end
        var scroll = new ScrollState { ContentHeight = 760, ViewportHeight = 400 };
        Assert.Equal(360f, scroll.MaxOffsetY);
        scroll.ScrollTo(0, scroll.MaxOffsetY);

        // The keyboard covers the bottom 200: the last row (364..400 in the viewport) is under it
        const float obscured = 200f;
        scroll.BottomInset = obscured;
        Assert.Equal(560f, scroll.MaxOffsetY);

        float delta = KumikoKeyboardAvoidance.GetScrollDelta(400, obscured, 364, 400, scroll.OffsetY, scroll.MaxOffsetY);
        scroll.ScrollTo(0, scroll.OffsetY + delta);

        Assert.Equal(200f, delta); // all the room the inset gave: the row now ends at the keyboard's top
        Assert.Equal(560f, scroll.OffsetY);
    }

    [Fact]
    public void BottomInset_ResetsTheMaximumAndIgnoresNegativeValues()
    {
        var scroll = new ScrollState { ContentHeight = 760, ViewportHeight = 400, BottomInset = 200 };
        scroll.ScrollTo(0, 500);
        scroll.BottomInset = 0;
        scroll.ClampOffset();
        Assert.Equal(360f, scroll.OffsetY);

        scroll.BottomInset = -5;
        Assert.Equal(0f, scroll.BottomInset);
        scroll.BottomInset = float.NaN;
        Assert.Equal(0f, scroll.BottomInset);
    }

    private static (RenderHarness Grid, EditSession Edit) EditLastRow(int row)
    {
        var grid = RenderHarness.Create(rows: 20, height: 400);
        grid.Render(); // the first frame sets the content extents
        grid.Scroll.ScrollTo(0, grid.Scroll.MaxOffsetY);
        var edit = new EditSession();
        var bounds = new KumikoRect(0, grid.RowTop(row), 150, grid.Style.RowHeight);
        Assert.True(edit.BeginEdit(row, 0, grid.Column("Name"), grid.Source, bounds));
        return (grid, edit);
    }

    [Fact]
    public void ScrollEditorIntoView_BringsTheLastRowAboveTheKeyboardAndMovesTheEditor()
    {
        var (grid, edit) = EditLastRow(19);
        Assert.True(edit.ActiveEditor!.Bounds.Bottom > 200); // under a keyboard covering the bottom 200

        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            grid.Scroll, grid.Source, grid.Style, edit, obscuredHeight: 200, applyInset: true);

        Assert.True(moved > 0);
        Assert.Equal(200f, grid.Scroll.BottomInset);
        Assert.True(edit.ActiveEditor!.Bounds.Bottom <= 200.01f); // the inset is all the room there is: the last row ends at the keyboard
        Assert.Equal(grid.RowTop(19), edit.ActiveEditor.Bounds.Y, 0.01f);
    }

    [Fact]
    public void ScrollEditorIntoView_LeavesAClearEditorAndTheOffsetAlone()
    {
        var (grid, edit) = EditLastRow(19);
        grid.Scroll.ScrollTo(0, 0);
        edit.ActiveEditor!.Bounds = new KumikoRect(0, 44, 150, 36);

        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            grid.Scroll, grid.Source, grid.Style, edit, 200, applyInset: true);

        Assert.Equal(0f, moved);
        Assert.Equal(0f, grid.Scroll.OffsetY);
    }

    [Fact]
    public void ScrollEditorIntoView_WithoutTheInsetStopsAtTheContentsEnd()
    {
        var (grid, edit) = EditLastRow(19);

        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            grid.Scroll, grid.Source, grid.Style, edit, 200, applyInset: false);

        Assert.Equal(0f, moved); // already at the end and no inset to scroll into
        Assert.Equal(0f, grid.Scroll.BottomInset);
    }

    [Fact]
    public void ScrollEditorIntoView_DoesNothingWithoutAnOpenEditor()
    {
        var grid = RenderHarness.Create(rows: 20, height: 400);
        grid.Render();

        float moved = KumikoKeyboardAvoidance.ScrollEditorIntoView(
            grid.Scroll, grid.Source, grid.Style, new EditSession(), 200, applyInset: true);

        Assert.Equal(0f, moved);
        Assert.Equal(200f, grid.Scroll.BottomInset);
    }
}
