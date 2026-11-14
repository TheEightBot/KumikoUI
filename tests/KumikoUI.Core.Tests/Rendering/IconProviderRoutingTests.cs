using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Components;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Every built-in glyph of the grid and of the drawn components is drawn by the style's (or the component's)
/// <see cref="IKumikoIconProvider"/> (#119): a provider that draws nothing leaves no triangle, chevron, tick or
/// funnel behind, and each slot asks for its own <see cref="KumikoIcon"/> in its own rectangle and color.
/// </summary>
public class IconProviderRoutingTests
{
    // ── Grid ──────────────────────────────────────────────────────

    private static (RenderHarness H, RecordingIconProvider Icons) Grid(Action<RenderHarness>? configure = null)
    {
        var icons = new RecordingIconProvider();
        var h = Create(configure: configure);
        h.Style.IconProvider = icons;
        return (h, icons);
    }

    /// <summary>No glyph drawn by anything but the provider: no polygon, no polyline.</summary>
    private static void AssertNoInlineGlyphs(RecordingDrawingContext ctx)
    {
        Assert.Empty(ctx.Polygons);
        Assert.Empty(ctx.Polylines);
    }

    [Fact]
    public void GroupHeaders_AskForTheExpandedOrCollapsedGlyph_InTheChevronSquare()
    {
        var (h, icons) = Grid();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.ToggleGroupExpansion(0);   // Oslo collapsed, Rome below it expanded

        var ctx = h.Render();

        AssertNoInlineGlyphs(ctx);
        var chevrons = icons.Calls.Where(c => c.Color == h.Style.GroupChevronColor).ToList();
        Assert.Equal(new[] { KumikoIcon.GroupCollapsed, KumikoIcon.GroupExpanded }, chevrons.Select(c => c.Icon));
        foreach (var chevron in chevrons)
        {
            // The style's chevron size, at the row's indent (cell padding), centered on the row
            Assert.Equal(new KumikoRect(chevron.Bounds.X, chevron.Bounds.Y, 14, 14), chevron.Bounds);
            Assert.Equal(h.Style.CellPadding, chevron.Bounds.X);
        }
        Assert.Equal(h.Style.RowHeight, chevrons[1].Bounds.Y - chevrons[0].Bounds.Y, 3);
    }

    [Theory]
    [InlineData(KumikoSortDirection.Ascending, KumikoIcon.SortAscending)]
    [InlineData(KumikoSortDirection.Descending, KumikoIcon.SortDescending)]
    public void ColumnHeader_SortIndicator_AsksForTheDirectionsGlyph(KumikoSortDirection direction, KumikoIcon expected)
    {
        var (h, icons) = Grid();
        h.Column("Age").SortDirection = direction;

        var ctx = h.Render();

        AssertNoInlineGlyphs(ctx);
        var sort = Assert.Single(icons.Calls, c => c.Color == h.Style.SortIndicatorColor);
        Assert.Equal(expected, sort.Icon);
        // A 12 px square centered on the header, its right edge 2 px into the cell padding of the Age column (x 150–250)
        Assert.Equal(new KumikoRect(250 - 8 - 8 - 2, h.Style.HeaderHeight / 2 - 6, 12, 12), sort.Bounds);
    }

    [Fact]
    public void ColumnHeader_FilterButton_AsksForFilter_OrFilterActiveWhenFiltered()
    {
        var (h, icons) = Grid();
        var city = h.Column("City");
        h.Source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Oslo" } });

        var ctx = h.Render();

        AssertNoInlineGlyphs(ctx);
        var active = Assert.Single(icons.Calls, c => c.Icon == KumikoIcon.FilterActive);
        Assert.Equal(h.Style.FilterActiveIconColor, active.Color);
        Assert.Equal(new KumikoRect(400 - 8 - 10, h.Style.HeaderHeight / 2 - 5, 10, 10), active.Bounds);
        var idle = icons.Calls.Where(c => c.Icon == KumikoIcon.Filter).ToList();
        Assert.Equal(3, idle.Count);
        Assert.All(idle, c => Assert.Equal(h.Style.FilterIconColor, c.Color));
    }

    [Fact]
    public void GroupingPanel_Chips_AskForSortRemoveAndSeparatorGlyphs()
    {
        var (h, icons) = Grid();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Name") { GroupSortDirection = KumikoSortDirection.Descending });

        var ctx = h.Render();

        AssertNoInlineGlyphs(ctx);
        var arrows = icons.Calls.Where(c => c.Color == h.Style.GroupPanelChipAccentColor).ToList();
        Assert.Equal(new[] { KumikoIcon.SortAscending, KumikoIcon.SortDescending }, arrows.Select(c => c.Icon));
        var removes = icons.Calls.Where(c => c.Icon == KumikoIcon.Remove).ToList();
        Assert.Equal(2, removes.Count);
        Assert.All(removes, c => Assert.Equal(h.Style.GroupPanelChipRemoveColor, c.Color));
        var separator = Assert.Single(icons.Calls, c => c.Icon == KumikoIcon.GroupSeparator);
        Assert.Equal(h.Style.GroupPanelLabelColor, separator.Color);

        // Every chip glyph is centered vertically on the 26 px chip in the middle of the 40 px panel
        Assert.All(arrows.Concat(removes).Append(separator),
            c => Assert.Equal(20, c.Bounds.Y + c.Bounds.Height / 2, 3));
        // The remove glyph sits in the 14 px circle behind it
        foreach (var remove in removes)
        {
            Assert.Contains(ctx.Fills, f => f.Kind == DrawOpKind.FillRoundRect && Near(f.Rect.Width, 14)
                && Near(f.Rect.X + 7, remove.Bounds.X + remove.Bounds.Width / 2));
        }
    }

    [Fact]
    public void DragHandleColumn_AsksForTheGripGlyph_ForEveryDataRow()
    {
        var (h, icons) = Grid();
        h.Style.ShowRowDragHandle = true;

        var ctx = h.Render();

        AssertNoInlineGlyphs(ctx);
        var grips = icons.Calls.Where(c => c.Icon == KumikoIcon.DragHandle).ToList();
        Assert.True(grips.Count >= 9, $"one grip per visible row, got {grips.Count}");
        Assert.All(grips, g =>
        {
            Assert.Equal(h.Style.RowDragHandleColor, g.Color);
            Assert.Equal(16, g.Bounds.Width);
            Assert.Equal(16, g.Bounds.Height);
            Assert.Equal(h.Style.RowDragHandleWidth / 2, g.Bounds.X + 8, 3);
        });
    }

    private sealed class Flag
    {
        public bool? Value { get; set; }
    }

    [Fact]
    public void BooleanCells_AskForCheckAndIndeterminate_InTheCheckBox()
    {
        var icons = new RecordingIconProvider();
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "On", PropertyName = "Value", Width = 100, ColumnType = KumikoColumnType.Boolean, IsThreeState = true },
        });
        source.SetItems(new List<Flag> { new() { Value = true }, new() { Value = false }, new() { Value = null } });
        var style = new KumikoStyle { IconProvider = icons };
        var ctx = new RecordingDrawingContext();

        new KumikoRenderer().Render(ctx, source, new ScrollState { ViewportWidth = 300, ViewportHeight = 300 },
            new KumikoSelectionModel(source), style);

        AssertNoInlineGlyphs(ctx);
        icons.Calls.RemoveAll(c => c.Icon == KumikoIcon.Filter); // the header's filter button
        Assert.Equal(new[] { KumikoIcon.Check, KumikoIcon.Indeterminate }, icons.Icons);
        Assert.Equal(style.AccentColor, icons.Calls[0].Color);
        Assert.Equal(2f, icons.Calls[0].StrokeWidth);
        // The glyph's slot is the drawn box itself
        var boxes = ctx.Ops.Where(o => o.Kind == DrawOpKind.DrawRect && Near(o.Rect.Width, 16)).Select(o => o.Rect).ToList();
        Assert.Contains(icons.Calls[0].Bounds, boxes);
        Assert.Contains(icons.Calls[1].Bounds, boxes);
    }

    [Fact]
    public void AGridUsingEveryGlyph_DrawsNoGlyphOfItsOwn()
    {
        var (h, icons) = Grid();
        h.Style.ShowRowDragHandle = true;
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Age"));
        h.Column("Name").SortDirection = KumikoSortDirection.Descending;
        var city = h.Column("City");
        h.Source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Oslo" } });

        AssertNoInlineGlyphs(h.Render());
        Assert.NotEmpty(icons.Calls);
    }

    // ── Drawn components ──────────────────────────────────────────

    [Fact]
    public void ComboBox_AsksForDropDown_OrDropDownOpenWhileOpen()
    {
        var icons = new RecordingIconProvider();
        var combo = new DrawnComboBox { Bounds = new KumikoRect(0, 0, 200, 30), IconProvider = icons };
        combo.AddItems(new[] { "A", "B" });

        AssertNoInlineGlyphs(ComponentInput.Draw(combo));
        combo.OpenDropdown();
        AssertNoInlineGlyphs(ComponentInput.Draw(combo));

        Assert.Equal(new[] { KumikoIcon.DropDown, KumikoIcon.DropDownOpen }, icons.Icons);
        Assert.All(icons.Calls, c => Assert.Equal(combo.ArrowColor, c.Color));
        // A 12 px square centered in the arrow area at the right end of the box
        var arrow = icons.Calls[0].Bounds;
        Assert.Equal(12, arrow.Width);
        Assert.Equal(15, arrow.Y + arrow.Height / 2, 3);
        Assert.True(arrow.Right <= 200 && arrow.X > 150);
    }

    [Fact]
    public void NumericUpDown_AsksForDecrementAndIncrement_CenteredInTheButtons()
    {
        var icons = new RecordingIconProvider();
        var spin = new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 200, 40), Value = 3, IconProvider = icons };

        AssertNoInlineGlyphs(ComponentInput.Draw(spin));

        Assert.Equal(new[] { KumikoIcon.Decrement, KumikoIcon.Increment }, icons.Icons);
        Assert.All(icons.Calls, c => Assert.Equal(spin.ButtonSymbolColor, c.Color));
        Assert.All(icons.Calls, c => Assert.Equal(20, c.Bounds.Y + c.Bounds.Height / 2, 3));
        Assert.True(icons.Calls[0].Bounds.Right < 100 && icons.Calls[1].Bounds.X > 100);
    }

    [Fact]
    public void DatePicker_AsksForCalendar_AndTheCalendarsNavigationAndTimeArrows()
    {
        var icons = new RecordingIconProvider();
        var picker = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 220, 30), ShowTimePicker = true, IconProvider = icons };
        picker.SelectedDate = new DateTime(2024, 3, 15);

        AssertNoInlineGlyphs(ComponentInput.Draw(picker));
        Assert.Equal(new[] { KumikoIcon.Calendar }, icons.Icons);
        Assert.Equal(16, icons.Calls[0].Bounds.Width);

        icons.Calls.Clear();
        picker.OpenCalendar();
        AssertNoInlineGlyphs(ComponentInput.Draw(picker));

        Assert.Equal(1, icons.Icons.Count(i => i == KumikoIcon.CalendarPrevious));
        Assert.Equal(1, icons.Icons.Count(i => i == KumikoIcon.CalendarNext));
        Assert.Equal(2, icons.Icons.Count(i => i == KumikoIcon.SpinUp));
        Assert.Equal(2, icons.Icons.Count(i => i == KumikoIcon.SpinDown));
        var previous = icons.Calls.Single(c => c.Icon == KumikoIcon.CalendarPrevious).Bounds;
        var next = icons.Calls.Single(c => c.Icon == KumikoIcon.CalendarNext).Bounds;
        Assert.Equal(previous.Y, next.Y);
        Assert.True(previous.X < next.X);
        // Each up arrow sits above its down arrow
        var ups = icons.Calls.Where(c => c.Icon == KumikoIcon.SpinUp).ToList();
        var downs = icons.Calls.Where(c => c.Icon == KumikoIcon.SpinDown).ToList();
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(ups[i].Bounds.X, downs[i].Bounds.X);
            Assert.True(ups[i].Bounds.Bottom <= downs[i].Bounds.Y);
        }
    }

    [Theory]
    [InlineData("Checked", KumikoIcon.Check)]
    [InlineData("Indeterminate", KumikoIcon.Indeterminate)]
    public void CheckBox_AsksForTheStatesGlyph_InItsBox(string stateName, KumikoIcon expected)
    {
        var icons = new RecordingIconProvider();
        var box = new DrawnCheckBox { Bounds = new KumikoRect(0, 0, 40, 40), State = Enum.Parse<DrawnCheckState>(stateName), IconProvider = icons };

        var ctx = ComponentInput.Draw(box);

        AssertNoInlineGlyphs(ctx);
        var call = Assert.Single(icons.Calls);
        Assert.Equal(expected, call.Icon);
        Assert.Equal(ctx.Ops.Single(o => o.Kind == DrawOpKind.DrawRoundRect).Rect, call.Bounds);
    }

    [Fact]
    public void CheckBox_Unchecked_AsksForNothing()
    {
        var icons = new RecordingIconProvider();
        var box = new DrawnCheckBox { Bounds = new KumikoRect(0, 0, 40, 40), IconProvider = icons };

        ComponentInput.Draw(box);

        Assert.Empty(icons.Calls);
    }

    private sealed class Named
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void FilterPopup_AsksForBothSortGlyphs_AndTheCheckOfSelectAllAndOfCheckedValues()
    {
        var source = new KumikoSource();
        var column = new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 };
        source.SetColumns(new[] { column });
        source.SetItems(new List<Named> { new() { Name = "A" }, new() { Name = "B" } });
        var popup = new DrawnFilterPopup(column, source);
        var icons = new RecordingIconProvider();
        popup.ApplyTheme(new KumikoStyle { IconProvider = icons });
        new PopupManager().Show(popup, new KumikoRect(0, 0, 150, 0));

        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);

        AssertNoInlineGlyphs(ctx);
        Assert.Same(icons, popup.IconProvider);
        Assert.Contains(KumikoIcon.SortAscending, icons.Icons);
        Assert.Contains(KumikoIcon.SortDescending, icons.Icons);
        // Select all, then each of the two (checked) values
        Assert.Equal(3, icons.Icons.Count(i => i == KumikoIcon.Check));
    }

    // ── Wiring ────────────────────────────────────────────────────

    [Fact]
    public void AnEditorTheGridOpens_TakesTheStylesIconProvider()
    {
        var icons = new RecordingIconProvider();
        var style = new KumikoStyle { IconProvider = icons };
        DrawnComponent[] editors =
        {
            new DrawnTextBox(), new DrawnComboBox(), new DrawnNumericUpDown(), new DrawnDatePicker(),
            new DrawnScrollPicker(), new DrawnCheckBox(),
        };

        foreach (var editor in editors)
            CellEditorFactory.ApplyThemeToEditor(editor, style);

        Assert.All(editors, e => Assert.Same(icons, e.IconProvider));
    }

    [Fact]
    public void Defaults_AreTheBuiltInSet_AndNullIsRejected()
    {
        var style = new KumikoStyle();
        var box = new DrawnCheckBox();

        Assert.Same(KumikoIconProvider.Default, style.IconProvider);
        Assert.Same(KumikoIconProvider.Default, box.IconProvider);
        Assert.Throws<ArgumentNullException>(() => style.IconProvider = null!);
        Assert.Throws<ArgumentNullException>(() => box.IconProvider = null!);
    }

    [Fact]
    public void ChangingTheStylesIconProvider_ChangesItsVersion_AndRedrawsAComponent()
    {
        var style = new KumikoStyle();
        int version = style.Version;
        style.IconProvider = new RecordingIconProvider();
        Assert.NotEqual(version, style.Version);

        var box = new DrawnCheckBox();
        int redraws = 0;
        box.RedrawRequested += (_, _) => redraws++;
        box.IconProvider = new RecordingIconProvider();
        Assert.Equal(1, redraws);
    }

    [Fact]
    public void EveryKumikoIcon_IsAskedForBySomeSlot()
    {
        var seen = new HashSet<KumikoIcon>();
        void Collect(RecordingIconProvider p) => seen.UnionWith(p.Icons);

        // Grid: grouped (expanded and collapsed, two levels), sorted both ways, filtered and not, drag handles,
        // and a three-state boolean column
        var (h, gridIcons) = Grid(g => g.Source.SetColumns(g.Source.Columns.Append(
            new KumikoColumn { Header = "Active", PropertyName = "Active", Width = 60, ColumnType = KumikoColumnType.Boolean }).ToList()));
        h.Style.ShowRowDragHandle = true;
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Age") { GroupSortDirection = KumikoSortDirection.Descending });
        h.Source.ToggleGroupExpansion(1);
        h.Column("Name").SortDirection = KumikoSortDirection.Ascending;
        var city = h.Column("City");
        h.Source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Oslo" } });
        h.Render();
        Collect(gridIcons);

        var components = new RecordingIconProvider();
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { Header = "On", PropertyName = "Value", ColumnType = KumikoColumnType.Boolean, IsThreeState = true } });
        source.SetItems(new List<Flag> { new() { Value = null } });
        new KumikoRenderer().Render(new RecordingDrawingContext(), source, new ScrollState { ViewportWidth = 300, ViewportHeight = 300 },
            new KumikoSelectionModel(source), new KumikoStyle { IconProvider = components });

        var combo = new DrawnComboBox { Bounds = new KumikoRect(0, 0, 200, 30), IconProvider = components };
        ComponentInput.Draw(combo);
        combo.OpenDropdown();
        ComponentInput.Draw(combo);
        ComponentInput.Draw(new DrawnNumericUpDown { Bounds = new KumikoRect(0, 0, 200, 30), IconProvider = components });
        var picker = new DrawnDatePicker { Bounds = new KumikoRect(0, 0, 220, 30), ShowTimePicker = true, IconProvider = components };
        picker.OpenCalendar();
        ComponentInput.Draw(picker);
        Collect(components);

        Assert.Equal(Enum.GetValues<KumikoIcon>().OrderBy(i => i), seen.OrderBy(i => i));
    }
}
