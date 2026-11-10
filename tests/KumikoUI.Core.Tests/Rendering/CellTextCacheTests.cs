using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The display text cache (#68): the renderer reads and formats a drawn cell once, then reuses its text while the
/// row shows the same item, and reads it again after anything that can change it: the item's PropertyChanged,
/// <see cref="KumikoSource.SetCellValue"/>, <see cref="KumikoSource.Refresh"/>, a view rebuild, a collection
/// reset, a culture change, or a change to the column's format or value path.
/// </summary>
public class CellTextCacheTests
{
    public sealed class Item : INotifyPropertyChanged
    {
        private string _name = "";
        private int _count;

        public string Name { get => _name; set => Set(ref _name, value, nameof(Name)); }
        public int Count { get => _count; set => Set(ref _count, value, nameof(Count)); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, string name)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>A grid of <see cref="Item"/>s whose Count column reads through a counting accessor.</summary>
    private sealed class Fixture
    {
        public KumikoSource Source { get; } = new();
        public KumikoRenderer Renderer { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 400 };
        public KumikoSelectionModel Selection { get; }
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = nameof(Item.Name), Width = 200 };
        public KumikoColumn Count { get; }
        public int Reads { get; set; }

        public Fixture(IEnumerable<Item> items)
        {
            Count = new KumikoColumn
            {
                Header = "Count", PropertyName = nameof(Item.Count), Width = 200,
                ValueAccessor = item => { Reads++; return ((Item)item).Count; },
            };
            Source.SetColumns(new[] { Name, Count });
            Source.SetItems(items);
            Selection = new KumikoSelectionModel(Source);
        }

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style);
            return ctx;
        }

        /// <summary>The text drawn in <paramref name="column"/>'s cell of visible row <paramref name="row"/>.</summary>
        public string CellText(RecordingDrawingContext ctx, int row, KumikoColumn column)
        {
            float top = Style.HeaderHeight + row * Style.RowHeight - Scroll.OffsetY;
            float left = column == Name ? 0 : 200;
            return Assert.Single(ctx.Texts, o => Near(o.Rect.Y, top) && o.Rect.X >= left && o.Rect.X < left + 200).Text!;
        }
    }

    private static List<Item> Items(int count) =>
        Enumerable.Range(0, count).Select(i => new Item { Name = $"N{i}", Count = i }).ToList();

    [Fact]
    public void RepeatedFrames_ReadEachDrawnCellOnce()
    {
        var f = new Fixture(Items(100));

        f.Render();
        int firstFrame = f.Reads;
        f.Render();
        f.Render();

        Assert.True(firstFrame > 0);
        Assert.Equal(firstFrame, f.Reads);
    }

    [Fact]
    public void ItemPropertyChanged_RedrawsThatItemsNewText_AndRereadsOnlyThatItem()
    {
        var items = Items(100);
        var f = new Fixture(items);
        f.Render();
        int reads = f.Reads;

        items[2].Count = 42;
        var ctx = f.Render();

        Assert.Equal("42", f.CellText(ctx, 2, f.Count));
        Assert.Equal(reads + 1, f.Reads);
    }

    [Fact]
    public void ItemPropertyChanged_ForAnotherProperty_KeepsTheCachedText()
    {
        var items = Items(100);
        var f = new Fixture(items);
        f.Render();
        int reads = f.Reads;

        items[2].Name = "Renamed";
        var ctx = f.Render();

        Assert.Equal("Renamed", f.CellText(ctx, 2, f.Name));
        // The Count column has a value accessor, which may read any property, so it is read again too.
        Assert.Equal(reads + 1, f.Reads);
    }

    [Fact]
    public void ChangeToAnItemWithoutNotification_ShowsAfterRefresh()
    {
        var people = Create(rows: 5);
        people.Render();

        people.People[1].Name = "Changed";
        var stale = people.Render();
        people.Source.Refresh();
        var fresh = people.Render();

        Assert.Empty(stale.TextsEqual("Changed"));
        Assert.Single(fresh.TextsEqual("Changed"));
    }

    [Fact]
    public void SetCellValue_OnAnItemWithoutNotification_ShowsTheNewValue()
    {
        var h = Create(rows: 5);
        h.Render();

        Assert.True(h.Source.SetCellValue(1, h.Column("Name"), "Edited"));
        var ctx = h.Render();

        ctx.SingleText("Edited", 1, h);
    }

    [Fact]
    public void Sorting_DrawsEachRowsOwnItem()
    {
        var f = new Fixture(Items(10));
        f.Render();

        f.Source.ToggleSort(f.Count); // ascending
        f.Source.ToggleSort(f.Count); // descending
        var ctx = f.Render();

        Assert.Equal("N9", f.CellText(ctx, 0, f.Name));
        Assert.Equal("9", f.CellText(ctx, 0, f.Count));
        Assert.Equal("N0", f.CellText(ctx, 9, f.Name));
    }

    [Fact]
    public void ScrollingFarAwayAndBack_DrawsTheRightRows()
    {
        var f = new Fixture(Items(1000));
        f.Render();

        f.Scroll.OffsetY = 500 * f.Style.RowHeight;
        Assert.Equal("N500", f.CellText(f.Render(), 500, f.Name));
        f.Scroll.OffsetY = 0;
        var back = f.Render();

        Assert.Equal("N0", f.CellText(back, 0, f.Name));
        Assert.Equal("3", f.CellText(back, 3, f.Count));
    }

    [Fact]
    public void ReplacingAnItemInTheCollection_DrawsTheNewItem()
    {
        var items = new ObservableCollection<Item>(Items(10));
        var f = new Fixture(items);
        f.Render();

        items[3] = new Item { Name = "New", Count = 77 };
        var ctx = f.Render();

        Assert.Equal("New", f.CellText(ctx, 3, f.Name));
        Assert.Equal("77", f.CellText(ctx, 3, f.Count));
    }

    [Fact]
    public void ChangingTheColumnFormat_ReformatsTheCells()
    {
        var f = new Fixture(Items(10));
        f.Render();

        f.Count.Format = "D3";
        var ctx = f.Render();

        Assert.Equal("007", f.CellText(ctx, 7, f.Count));
    }

    [Fact]
    public void ChangingTheValuePath_ReadsTheNewProperty()
    {
        var f = new Fixture(Items(10));
        f.Render();

        f.Name.PropertyName = nameof(Item.Count);
        var ctx = f.Render();

        Assert.Equal("4", f.CellText(ctx, 4, f.Name));
    }

    [Fact]
    public void ChangingTheSourceCulture_ReformatsTheCells()
    {
        var f = new Fixture(new[] { new Item { Name = "A", Count = 1234567 } });
        f.Count.Format = "N0";
        f.Source.Culture = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("1,234,567", f.CellText(f.Render(), 0, f.Count));

        f.Source.Culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("1.234.567", f.CellText(f.Render(), 0, f.Count));
    }

    [Fact]
    public void ChangingTheCurrentCulture_ReformatsTheCells_WhenTheSourceHasNoCulture()
    {
        var f = new Fixture(new[] { new Item { Name = "A", Count = 1234567 } });
        f.Count.Format = "N0";
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("1,234,567", f.CellText(f.Render(), 0, f.Count));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("1.234.567", f.CellText(f.Render(), 0, f.Count));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FrozenRows_AndScrollingRows_KeepTheirOwnText()
    {
        var f = new Fixture(Items(500));
        f.Source.FrozenRowCount = 2;
        f.Render();

        // Scroll so that scrolling rows whose ring slots would otherwise coincide with the frozen rows show.
        f.Scroll.OffsetY = 128 * f.Style.RowHeight;
        var ctx = f.Render();

        var frozenTexts = ctx.Texts.Where(o => o.Rect.X < 200 && (o.Text == "N0" || o.Text == "N1")).ToList();
        Assert.Equal(2, frozenTexts.Count);
        Assert.Contains(ctx.Texts, o => o.Text == "N130");
    }

    [Fact]
    public void ARendererSeesTheCachedValueAndText()
    {
        var f = new Fixture(Items(3));
        var capture = new CapturingRenderer();
        f.Count.CustomCellRenderer = capture;

        f.Render();
        f.Render();

        var cells = capture.Cells.Where(c => c.Row == 2).ToList();
        Assert.Equal(2, cells.Count);
        Assert.All(cells, c => Assert.Equal(2, c.Value));
        Assert.Same(cells[0].Text, cells[1].Text);
    }

    private sealed class CapturingRenderer : ICellRenderer
    {
        public List<(int Row, object? Value, string Text)> Cells { get; } = new();

        public void Render(IDrawingContext ctx, in CellRenderContext cell) =>
            Cells.Add((cell.Row, cell.Value, cell.DisplayText));
    }
}
