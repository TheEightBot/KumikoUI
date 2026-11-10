using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Type-aware first sort direction (#173): the first header tap sorts Boolean columns checked first and Date
/// columns newest first, text and numbers ascending; later taps step to the opposite direction and then to none;
/// <see cref="KumikoColumn.InitialSortDirection"/> overrides the default; the filter popup follows.
/// </summary>
public class SortDirectionTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public DateTime When { get; set; }
        public bool Done { get; set; }
        public string Kind { get; set; } = "";
    }

    private static readonly Row[] Rows =
    {
        new() { Name = "b", Count = 2, When = new DateTime(2024, 2, 1), Done = false, Kind = "y" },
        new() { Name = "c", Count = 3, When = new DateTime(2024, 3, 1), Done = true, Kind = "z" },
        new() { Name = "a", Count = 1, When = new DateTime(2024, 1, 1), Done = true, Kind = "x" },
    };

    private static KumikoColumn Col(string property, KumikoColumnType type) =>
        new() { Header = property, PropertyName = property, ColumnType = type, Width = 100 };

    private static (KumikoSource Source, KumikoColumn Name, KumikoColumn Count, KumikoColumn When, KumikoColumn Done, KumikoColumn Kind) Create()
    {
        var name = Col("Name", KumikoColumnType.Text);
        var count = Col("Count", KumikoColumnType.Numeric);
        var when = Col("When", KumikoColumnType.Date);
        var done = Col("Done", KumikoColumnType.Boolean);
        var kind = Col("Kind", KumikoColumnType.ComboBox);
        var source = new KumikoSource();
        source.SetColumns(new[] { name, count, when, done, kind });
        source.SetItems(Rows);
        return (source, name, count, when, done, kind);
    }

    private static List<string> Names(KumikoSource source) =>
        Enumerable.Range(0, source.RowCount).Select(r => (string)source.GetCellValue(r, source.Columns[0])!).ToList();

    [Fact]
    public void FirstTap_FollowsTheColumnType()
    {
        var (source, name, count, when, done, kind) = Create();

        foreach (var (column, expected) in new[]
        {
            (name, KumikoSortDirection.Ascending), (count, KumikoSortDirection.Ascending),
            (kind, KumikoSortDirection.Ascending), (when, KumikoSortDirection.Descending),
            (done, KumikoSortDirection.Descending),
        })
        {
            source.ClearSort();
            source.ToggleSort(column);
            Assert.Equal(expected, column.SortDirection);
            Assert.Equal(expected, column.FirstSortDirection);
        }
    }

    [Fact]
    public void Boolean_FirstTapPutsCheckedRowsFirst_ThenUncheckedFirst_ThenNone()
    {
        var (source, _, _, _, done, _) = Create();

        source.ToggleSort(done);
        Assert.Equal(KumikoSortDirection.Descending, done.SortDirection);
        Assert.All(Enumerable.Range(0, 2), r => Assert.True((bool)source.GetCellValue(r, done)!));
        Assert.False((bool)source.GetCellValue(2, done)!);

        source.ToggleSort(done);
        Assert.Equal(KumikoSortDirection.Ascending, done.SortDirection);
        Assert.False((bool)source.GetCellValue(0, done)!);

        source.ToggleSort(done);
        Assert.Equal(KumikoSortDirection.None, done.SortDirection);
        Assert.Equal(new[] { "b", "c", "a" }, Names(source));
    }

    [Fact]
    public void Date_FirstTapIsNewestFirst_ThenOldestFirst_ThenNone()
    {
        var (source, _, _, _, _, _) = Create();
        var when = source.Columns[2];

        source.ToggleSort(when);
        Assert.Equal(new[] { "c", "b", "a" }, Names(source));
        source.ToggleSort(when);
        Assert.Equal(new[] { "a", "b", "c" }, Names(source));
        source.ToggleSort(when);
        Assert.Equal(KumikoSortDirection.None, when.SortDirection);
        Assert.Equal(new[] { "b", "c", "a" }, Names(source));
    }

    [Fact]
    public void Text_StillCyclesAscendingDescendingNone()
    {
        var (source, name, _, _, _, _) = Create();

        source.ToggleSort(name);
        Assert.Equal(new[] { "a", "b", "c" }, Names(source));
        source.ToggleSort(name);
        Assert.Equal(new[] { "c", "b", "a" }, Names(source));
        source.ToggleSort(name);
        Assert.Equal(KumikoSortDirection.None, name.SortDirection);
    }

    [Fact]
    public void InitialSortDirection_OverridesTheTypeDefault_AndTheCycleStartsFromIt()
    {
        var (source, _, count, when, done, _) = Create();
        count.InitialSortDirection = KumikoSortDirection.Descending;
        when.InitialSortDirection = KumikoSortDirection.Ascending;
        done.InitialSortDirection = KumikoSortDirection.Ascending;

        source.ToggleSort(count);
        Assert.Equal(KumikoSortDirection.Descending, count.SortDirection);
        source.ToggleSort(count);
        Assert.Equal(KumikoSortDirection.Ascending, count.SortDirection);
        source.ToggleSort(count);
        Assert.Equal(KumikoSortDirection.None, count.SortDirection);

        source.ToggleSort(when);
        Assert.Equal(KumikoSortDirection.Ascending, when.SortDirection);
        source.ToggleSort(when);
        Assert.Equal(KumikoSortDirection.Descending, when.SortDirection);

        source.ToggleSort(done);
        Assert.Equal(KumikoSortDirection.Ascending, done.SortDirection);
    }

    [Fact]
    public void InitialSortDirection_NoneOrNull_MeansTheTypeDefault()
    {
        var (_, _, _, when, _, _) = Create();
        Assert.Null(when.InitialSortDirection);
        Assert.Equal(KumikoSortDirection.Descending, when.FirstSortDirection);

        when.InitialSortDirection = KumikoSortDirection.None;
        Assert.Equal(KumikoSortDirection.Descending, when.FirstSortDirection);
    }

    [Fact]
    public void SetSort_IsNotAffectedByTheFirstDirection_AndATapContinuesFromTheActualDirection()
    {
        var (source, _, _, when, _, _) = Create();

        source.SetSort(when, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "a", "b", "c" }, Names(source));
        // Ascending is the opposite of newest first, so the next tap removes the sort.
        source.ToggleSort(when);
        Assert.Equal(KumikoSortDirection.None, when.SortDirection);
    }

    [Fact]
    public void ToggleMultiSort_UsesEachColumnsFirstDirection_AndReportsItInTheEvents()
    {
        var (source, name, _, _, done, _) = Create();
        var changing = new List<KumikoSortDirection>();
        var changedDirections = new List<KumikoSortDirection>();
        source.SortColumnsChanging += (_, e) => changing.Add(e.NewDirection);
        source.SortColumnsChanged += (_, e) => changedDirections.Add(e.Column.SortDirection);

        source.ToggleMultiSort(done);
        source.ToggleMultiSort(name);

        Assert.Equal(new[] { KumikoSortDirection.Descending, KumikoSortDirection.Ascending }, changing);
        Assert.Equal(changing, changedDirections);
        Assert.Equal(KumikoSortDirection.Descending, done.SortDirection);
        Assert.Equal(1, done.SortOrder);
        Assert.Equal(KumikoSortDirection.Ascending, name.SortDirection);
        Assert.Equal(2, name.SortOrder);
        // Checked rows first, then by name within each group.
        Assert.Equal(new[] { "a", "c", "b" }, Names(source));

        source.ToggleMultiSort(done); // second step of the Boolean cycle: unchecked first, same place
        Assert.Equal(KumikoSortDirection.Ascending, done.SortDirection);
        Assert.Equal(1, done.SortOrder);
        Assert.Equal(new[] { "b", "a", "c" }, Names(source));
    }

    [Fact]
    public void HeaderTap_WithShiftOrControl_JoinsTheMultiSortWithTheFirstDirection()
    {
        var g = new InputGrid(rows: 6, configure: g => g.Score.ColumnType = KumikoColumnType.Boolean);

        g.Tap(60, InputGrid.HeaderY);                         // Name: ascending
        g.Tap(450, InputGrid.HeaderY, InputModifiers.Shift);  // Score (Boolean here): checked first, second place
        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
        Assert.Equal(KumikoSortDirection.Descending, g.Score.SortDirection);
        Assert.Equal(2, g.Score.SortOrder);

        g.Tap(450, InputGrid.HeaderY, InputModifiers.Control);
        Assert.Equal(KumikoSortDirection.Ascending, g.Score.SortDirection);
        g.Tap(450, InputGrid.HeaderY, InputModifiers.Control);
        Assert.Equal(KumikoSortDirection.None, g.Score.SortDirection);
        Assert.Equal(1, g.Name.SortOrder);
    }

    [Fact]
    public void HeaderGlyph_PointsInTheActualDirection()
    {
        var h = RenderHarness.Create();
        var active = new KumikoColumn { Header = "Active", PropertyName = "Active", Width = 100, ColumnType = KumikoColumnType.Boolean };
        h.Source.SetColumns(h.Source.Columns.Append(active).ToList());

        // Checked first is descending: a downward arrow, flat edge at the top (y 17) and apex at the bottom (y 23).
        h.Source.ToggleSort(active);
        var arrow = h.Render().Lines.Where(l => l.Color == h.Style.SortIndicatorColor).ToList();
        Assert.Equal(3, arrow.Count);
        Assert.Single(arrow, l => l.Y1 == 17f && l.Y2 == 17f);

        // Unchecked first is ascending: flat edge at the bottom (y 23).
        h.Source.ToggleSort(active);
        arrow = h.Render().Lines.Where(l => l.Color == h.Style.SortIndicatorColor).ToList();
        Assert.Equal(3, arrow.Count);
        Assert.Single(arrow, l => l.Y1 == 23f && l.Y2 == 23f);
    }

    // ── Filter popup ──────────────────────────────────────────────

    private static (DrawnFilterPopup Popup, RecordingDrawingContext Ctx) Popup(KumikoColumn column, KumikoSource source)
    {
        var popup = new DrawnFilterPopup(column, source);
        new PopupManager().Show(popup, new KumikoRect(0, 0, 150, 0));
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        return (popup, ctx);
    }

    /// <summary>The sort row labels, left to right.</summary>
    private static string[] SortRows(KumikoColumn column, KumikoSource source) =>
        Popup(column, source).Ctx.Texts.Where(t => t.Text!.StartsWith("Sort ", StringComparison.Ordinal))
            .OrderBy(t => t.Rect.X).Select(t => t.Text!).ToArray();

    [Fact]
    public void FilterPopup_BooleanColumn_ShowsCheckedFirstThenUncheckedFirst()
    {
        var (source, _, _, _, done, _) = Create();

        var rows = SortRows(done, source);

        Assert.Equal(new[] { "Sort Checked First", "Sort Unchecked First" }, rows);
    }

    [Fact]
    public void FilterPopup_FirstRowIsTheFirstTapDirection_ForEveryType()
    {
        var (source, name, count, when, done, kind) = Create();

        Assert.Equal(new[] { "Sort A to Z", "Sort Z to A" }, SortRows(name, source));
        Assert.Equal(new[] { "Sort Smallest to Largest", "Sort Largest to Smallest" }, SortRows(count, source));
        Assert.Equal(new[] { "Sort A to Z", "Sort Z to A" }, SortRows(kind, source));
        Assert.Equal(new[] { "Sort Newest to Oldest", "Sort Oldest to Newest" }, SortRows(when, source));
        Assert.Equal(new[] { "Sort Checked First", "Sort Unchecked First" }, SortRows(done, source));

        count.InitialSortDirection = KumikoSortDirection.Descending;
        Assert.Equal(new[] { "Sort Largest to Smallest", "Sort Smallest to Largest" }, SortRows(count, source));
    }

    [Fact]
    public void FilterPopup_Rows_RequestTheirDirection()
    {
        var (source, _, _, _, done, _) = Create();
        var popup = new DrawnFilterPopup(done, source);
        var manager = new PopupManager();
        manager.Show(popup, new KumikoRect(0, 0, 150, 0));
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        var requested = new List<KumikoSortDirection>();
        popup.SortRequested += requested.Add;

        foreach (var label in new[] { "Sort Checked First", "Sort Unchecked First" })
        {
            var rect = ctx.Texts.Single(t => t.Text == label).Rect;
            float x = rect.X + rect.Width / 2, y = rect.Y + rect.Height / 2;
            manager.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed });
            manager.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released });
        }

        Assert.Equal(new[] { KumikoSortDirection.Descending, KumikoSortDirection.Ascending }, requested);
    }

    [Fact]
    public void FilterPopup_BooleanLabelsComeFromTheSourcesStrings()
    {
        var (source, _, _, _, done, _) = Create();
        source.Strings = new KumikoStrings
        {
            SortBooleanCheckedFirst = "Angehakte zuerst",
            SortBooleanUncheckedFirst = "Nicht angehakte zuerst",
        };

        var texts = Popup(done, source).Ctx.Texts.Select(t => t.Text).ToList();

        Assert.Contains("Angehakte zuerst", texts);
        Assert.Contains("Nicht angehakte zuerst", texts);
        Assert.DoesNotContain("Sort Checked First", texts);
    }
}
