using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Editors draw with the grid's cell font (or the column's) instead of a fixed one, and popups (a combo box
/// dropdown, a date calendar, a filter panel) stay inside the viewport: below the cell when they fit, above
/// it when only that fits, slid sideways into view, and a list shrinks to the room left (#36).
/// </summary>
public class EditorFontAndPopupTests
{
    private static readonly KumikoRect Viewport = new(0, 0, 300, 256);

    private static InputGrid Grid(Action<InputGrid>? configure = null) => new(configure: g =>
    {
        g.Style.CellFont = new KumikoFont("Noto Sans CJK", 17, bold: true, italic: true);
        configure?.Invoke(g);
    });

    private static DrawnComponent Open(InputGrid g, int row, int column)
    {
        g.Select(row, column);
        g.Type('o');
        Assert.True(g.Edit.IsEditing, "no editor opened");
        return g.Edit.ActiveEditor!;
    }

    private static KumikoFont[] FontsDrawn(InputGrid g)
    {
        var ctx = new RecordingDrawingContext();
        g.Edit.DrawEditor(ctx);
        return ctx.Texts.Select(o => o.Font!.Value).ToArray();
    }

    // ── Font ──

    [Fact]
    public void TextEditor_DrawsWithTheGridsCellFont()
    {
        var g = Grid();
        var editor = Open(g, 1, 0);

        var fonts = FontsDrawn(g);

        Assert.NotEmpty(fonts);
        Assert.All(fonts, f =>
        {
            Assert.Equal("Noto Sans CJK", f.Family);
            Assert.Equal(17f, f.Size);
            Assert.True(f.IsBold);
            Assert.True(f.IsItalic);
        });
        Assert.Equal(g.Style.CellFont, ((DrawnTextBox)editor).Font);
    }

    [Fact]
    public void NumericEditor_DrawsWithTheGridsCellFont()
    {
        var g = Grid();
        Open(g, 1, 1);

        Assert.All(FontsDrawn(g), f => Assert.Equal(17f, f.Size));
    }

    [Fact]
    public void ColumnCellStyleFont_WinsOverTheGridsCellFont()
    {
        var g = Grid(x => x.Name.CellStyle = new KumikoCellStyle { Font = new KumikoFont("Mono", 9) });
        Open(g, 1, 0);

        Assert.All(FontsDrawn(g), f =>
        {
            Assert.Equal("Mono", f.Family);
            Assert.Equal(9f, f.Size);
        });
    }

    [Fact]
    public void DynamicCellStyleFont_WinsOverTheColumnsFont()
    {
        var g = Grid(x =>
        {
            x.Name.CellStyle = new KumikoCellStyle { Font = new KumikoFont("Mono", 9) };
            x.Style.CellStyleResolver = (_, _) => new KumikoCellStyle { Font = new KumikoFont("Serif", 21) };
        });
        Open(g, 1, 0);

        Assert.All(FontsDrawn(g), f => Assert.Equal(("Serif", 21f), (f.Family, f.Size)));
    }

    [Fact]
    public void ComboBox_DatePicker_AndPicker_UseTheGridsFont()
    {
        var combo = new DrawnComboBox { Bounds = new KumikoRect(0, 0, 100, 28) };
        var date = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 100, 28) };
        var picker = new DrawnScrollPicker { Bounds = new KumikoRect(0, 0, 100, 200) };
        var style = new KumikoStyle { CellFont = new KumikoFont("Noto Sans CJK", 17) };

        combo.ApplyTheme(style);
        date.ApplyTheme(style);
        picker.ApplyTheme(style);

        Assert.Equal(("Noto Sans CJK", 17f), (combo.Font.Family, combo.Font.Size));
        Assert.Equal(("Noto Sans CJK", 17f), (date.Font.Family, date.Font.Size));
        Assert.Equal(("Noto Sans CJK", 16f), (date.DayFont.Family, date.DayFont.Size));
        Assert.Equal(("Noto Sans CJK", 17f), (picker.Font.Family, picker.Font.Size));
        Assert.True(picker.SelectedFont.IsBold);
        Assert.Equal(19f, picker.SelectedFont.Size);
    }

    [Fact]
    public void TextBox_MeasuresTheCaretWithTheNewFont()
    {
        var box = new DrawnTextBox { Bounds = new KumikoRect(0, 0, 200, 30), Text = "abc" };
        var ctx = new RecordingDrawingContext();
        box.OnDraw(ctx);
        var small = ctx.Ops.Count;

        box.ApplyFont(new KumikoFont("Default", 30));
        var ctx2 = new RecordingDrawingContext();
        box.OnDraw(ctx2);

        Assert.Equal(30f, ctx2.Texts.First().Font!.Value.Size);
        Assert.True(small > 0);
    }

    // ── Popups ──

    private static DrawnComboBox TenItems(KumikoRect bounds, KumikoRect viewport)
    {
        var combo = new DrawnComboBox { Bounds = bounds, PopupViewport = viewport };
        combo.SetItemsFromSource(Enumerable.Range(0, 10).Select(i => $"Item {i}").ToList());
        combo.OpenDropdown();
        return combo;
    }

    [Fact]
    public void Dropdown_WithRoomBelow_OpensBelow()
    {
        var combo = TenItems(new KumikoRect(10, 40, 100, 28), new KumikoRect(0, 0, 300, 600));

        var drop = combo.GetDropdownBounds();

        Assert.Equal(68f, drop.Y);
        Assert.Equal(200f, drop.Height);
    }

    [Fact]
    public void Dropdown_NearTheBottom_FlipsAboveTheCell()
    {
        var combo = TenItems(new KumikoRect(10, 220, 100, 28), Viewport);

        var drop = combo.GetDropdownBounds();

        Assert.Equal(200f, drop.Height);
        Assert.Equal(220f, drop.Bottom);
        Assert.True(drop.Y >= 0);
    }

    [Fact]
    public void Dropdown_InAShortViewport_ShrinksToTheRoomAndScrolls()
    {
        // Room below the cell: 120 - 68 = 52; above: 40; so the list takes the 52 below
        var combo = TenItems(new KumikoRect(10, 40, 100, 28), new KumikoRect(0, 0, 300, 120));

        var drop = combo.GetDropdownBounds();

        Assert.Equal(68f, drop.Y);
        Assert.Equal(52f, drop.Height);
        Assert.True(drop.Bottom <= 120);
        combo.OnScroll(new KumikoUI.Core.Input.KumikoPointerEventArgs { X = 50, Y = 90, Action = KumikoUI.Core.Input.InputAction.Scroll, ScrollDeltaY = -3 });
        Assert.True(combo.DropdownScrollOffset > 0, "a list taller than its room scrolls");
    }

    [Fact]
    public void Dropdown_WithoutAViewport_KeepsItsOldPlacement()
    {
        var combo = TenItems(new KumikoRect(10, 220, 100, 28), default);

        var drop = combo.GetDropdownBounds();

        Assert.Equal(248f, drop.Y);
        Assert.Equal(200f, drop.Height);
    }

    [Fact]
    public void ComboBoxEditor_OnTheBottomRow_FlipsAboveInsideTheGrid()
    {
        var g = Grid(x =>
        {
            x.City.ColumnType = KumikoColumnType.ComboBox;
            x.City.EditorItems = Enumerable.Range(0, 10).Select(i => $"City {i}").ToList();
        });
        var combo = (DrawnComboBox)Open(g, 5, 2);

        var drop = combo.GetDropdownBounds();

        Assert.True(drop.Bottom <= combo.Bounds.Y + 0.01f, "the list is above the cell");
        Assert.True(drop.Y >= 0);
        Assert.Equal(Viewport, combo.PopupViewport);
    }

    [Fact]
    public void Calendar_NearTheBottom_FlipsAboveTheCell()
    {
        var date = new DrawnDatePicker { Bounds = new KumikoRect(10, 360, 100, 28), PopupViewport = new KumikoRect(0, 0, 300, 400) };
        date.OpenCalendar();

        var cal = date.GetCalendarBounds();

        Assert.True(cal.Bottom <= 360f);
        Assert.True(cal.Y >= 0);
    }

    [Fact]
    public void Calendar_NearTheRightEdge_SlidesInside()
    {
        var date = new DrawnDatePicker { Bounds = new KumikoRect(250, 40, 50, 28), PopupViewport = new KumikoRect(0, 0, 300, 400) };
        date.OpenCalendar();

        var cal = date.GetCalendarBounds();

        Assert.True(cal.Right <= 300f + 0.01f);
        Assert.True(cal.X >= 0);
        Assert.Equal(68f + 2f, cal.Y);
    }

    [Fact]
    public void Calendar_TallerThanTheViewport_StaysAtItsTop()
    {
        var date = new DrawnDatePicker { Bounds = new KumikoRect(10, 60, 100, 28), PopupViewport = new KumikoRect(0, 0, 300, 100) };
        date.OpenCalendar();

        var cal = date.GetCalendarBounds();

        Assert.True(cal.Y >= 0);
    }

    [Fact]
    public void PickerEditor_NearTheBottom_IsSlidIntoTheViewport()
    {
        var g = Grid(x =>
        {
            x.City.ColumnType = KumikoColumnType.Picker;
            x.City.EditorItems = Enumerable.Range(0, 10).Select(i => $"City {i}").ToList();
        });
        var picker = Open(g, 5, 2);

        Assert.True(picker.Bounds.Bottom <= 256f + 0.01f);
        Assert.True(picker.Bounds.Y >= 0);
    }

    [Fact]
    public void PopupManager_FlipsAndClampsToItsViewport()
    {
        var manager = new PopupManager { Viewport = Viewport };
        var popup = new DrawnTextBox { Bounds = new KumikoRect(0, 0, 120, 100) };

        manager.Show(popup, new KumikoRect(250, 230, 50, 20), PopupPlacement.Below);

        Assert.True(popup.Bounds.Bottom <= 230f, "flipped above the anchor");
        Assert.Equal(180f, popup.Bounds.X); // slid left so the 120 px popup fits
        Assert.True(popup.Bounds.Right <= 300f);
    }
}
