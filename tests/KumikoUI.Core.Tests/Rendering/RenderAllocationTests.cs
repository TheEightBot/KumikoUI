using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

// Runs alone: other tests change shared state (the accessor registry, the current culture) that rightly
// invalidates the cell text cache, which would show up here as allocation in the measured frames.
[CollectionDefinition(nameof(RenderAllocationCollection), DisableParallelization = true)]
public sealed class RenderAllocationCollection;

/// <summary>
/// The steady-state render loop allocates nothing (#68): once warm-up frames have filled the paint cache and the
/// display text cache, drawing the same data again allocates 0 bytes on the rendering thread, for a bare grid and
/// for one using every feature that draws per frame.
/// </summary>
/// <remarks>
/// Measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which counts only this thread's allocations,
/// so tests running in parallel do not disturb it. The warm-up frames also absorb one-time costs (JIT, type
/// initialization, the caches' first fill); tiered recompilation happens on a background thread and allocates
/// nothing here. The expected value is exactly 0.
/// </remarks>
[Collection(nameof(RenderAllocationCollection))]
public class RenderAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;
    private const int VisibleRows = 30;
    private const int VisibleColumns = 10;
    private const float ColumnWidth = 110f;

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public double Ratio { get; set; }
        public DateTime Date { get; set; }
        public bool? Active { get; set; }
        public string City { get; set; } = "";
        public long Code { get; set; }
        public float Score { get; set; }
        public string Notes { get; set; } = "";
    }

    private sealed record Grid(KumikoRenderer Renderer, KumikoSource Source, ScrollState Scroll,
        KumikoSelectionModel Selection, KumikoStyle Style)
    {
        public void Render(IDrawingContext ctx) => Renderer.Render(ctx, Source, Scroll, Selection, Style);
    }

    [Fact]
    public void SteadyState_PlainGrid_AllocatesNothing()
    {
        var grid = CreateGrid(featured: false);

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void SteadyState_GridUsingEveryPerFrameFeature_AllocatesNothing()
    {
        var grid = CreateGrid(featured: true);

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void SteadyState_RowDragGhostAndDropIndicator_AllocateNothing()
    {
        // A row drag redraws every frame (pointer moves, edge autoscroll): the ghost's texts are read once per drag.
        var grid = CreateGrid(featured: false);
        int frame = 0;

        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            float ghostTop = frame++ % 2 == 0 ? 200 : 260;
            grid.Renderer.Render(ctx, grid.Source, grid.Scroll, grid.Selection, grid.Style,
                dragRowIndex: 3, dragRowScreenY: ghostTop);
        }));
    }

    [Fact]
    public void SteadyState_ScrollingBackAndForthOverCachedRows_AllocatesNothing()
    {
        var grid = CreateGrid(featured: false);
        int frame = 0;

        // Alternate between two offsets whose rows all fit in the display text cache.
        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Scroll.OffsetY = frame++ % 2 == 0 ? 0 : 5 * grid.Style.RowHeight + 7;
            grid.Render(ctx);
        }));
    }

    [Fact]
    public void SteadyState_StickyGroupHeadersPinnedMidGroup_AllocatesNothing()
    {
        // Two group levels (City, then Active), scrolled into the middle of a group so both levels are pinned (#174),
        // alternating with an offset three rows lower where the next inner header pushes the pinned one up.
        var grid = CreateGrid(featured: true);
        grid.Source.AddGroupDescription(new GroupDescription(nameof(Row.Active)));
        float rowHeight = grid.Style.RowHeight;
        int nextInnerHeader = Enumerable.Range(2, grid.Source.RowCount - 2)
            .First(r => grid.Source.GetGroupHeaderInfo(r)?.Level == 1);
        float pushing = (nextInnerHeader - 2) * rowHeight + 20;   // that header 20 px into the inner slot
        float midGroup = pushing - 3 * rowHeight;
        float rowsTop = grid.Style.GroupPanelHeight + grid.Style.HeaderHeight + grid.Style.SummaryRowHeight;

        grid.Scroll.OffsetY = pushing;
        var check = new RecordingDrawingContext();
        grid.Render(check);
        Assert.True(grid.Style.StickyGroupHeaders);
        Assert.Contains(check.Texts, o => o.Text!.StartsWith("City: ", StringComparison.Ordinal) && Math.Abs(o.Rect.Y - rowsTop) < 0.01f);
        Assert.Contains(check.Texts, o => o.Text!.StartsWith("Active: ", StringComparison.Ordinal)
            && Math.Abs(o.Rect.Y - (rowsTop + rowHeight - 20)) < 0.01f);

        int frame = 0;
        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Scroll.OffsetY = frame++ % 2 == 0 ? midGroup : pushing;
            grid.Render(ctx);
        }));
    }

    [Fact]
    public void SteadyState_HeaderFittedAndStarColumns_AllocatesNothing()
    {
        // Columns without an explicit width are fitted to their headers (#182), and star columns are shared out
        // around their header minimums every frame; neither measures nor allocates once the headers are measured.
        var grid = CreateGrid(featured: false);
        var columns = grid.Source.Columns;
        columns[0].Header = "A header much too long for the default width";
        columns[0].HeaderCellStyle = new KumikoCellStyle { Font = new KumikoFont("Default", 15, bold: true) };
        grid.Source.SetColumns(columns.Select((c, i) => new KumikoColumn
        {
            Header = c.Header,
            PropertyName = c.PropertyName,
            ColumnType = c.ColumnType,
            Format = c.Format,
            HeaderCellStyle = c.HeaderCellStyle,
            SizeMode = i % 3 == 0 ? ColumnSizeMode.Star : ColumnSizeMode.Fixed,
        }).ToList());

        Assert.Equal(0, MeasureSteadyState(grid.Render));
        Assert.All(grid.Source.Columns, c => Assert.False(c.HasExplicitWidth));
        Assert.True(grid.Source.Columns[0].Width >= grid.Source.Columns[0].HeaderFitWidth);
    }

    [Fact]
    public void AnOpenTextEditor_WithAVisibleCaretAndSelection_AllocatesNothing()
    {
        var box = new KumikoUI.Core.Components.DrawnTextBox
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Text = "Some editable text",
        };
        box.IsFocused = true;
        box.CursorColor = new KumikoColor(0, 0, 0);
        box.SelectAll();

        // The caret blinks on a wall clock: pin the measurement inside one visible half-period by retrying
        Assert.Equal(0, MeasureSteadyState(box.OnDraw));
    }

    [Fact]
    public void ANumericUpDown_ShowingAndTypingANumber_AllocatesNothing()
    {
        var spin = new KumikoUI.Core.Components.DrawnNumericUpDown
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            Step = 0.1,
            Value = 12.3,
        };
        spin.IsFocused = true;
        Assert.Equal(0, MeasureSteadyState(spin.OnDraw));

        // Typed entry draws its text box
        spin.OnKeyDown(new KumikoUI.Core.Input.KumikoKeyEventArgs { Key = KumikoUI.Core.Input.KumikoKey.Character, Character = '7' });
        Assert.Equal(0, MeasureSteadyState(spin.OnDraw));
    }

    [Fact]
    public void ADatePicker_WithAnOpenCalendar_AllocatesNothing()
    {
        var picker = new KumikoUI.Core.Components.DrawnDatePicker
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            ShowTimePicker = true,
            MinDate = new DateTime(2000, 1, 1),
            MaxDate = new DateTime(2100, 1, 1),
        };
        picker.SelectedDate = new DateTime(2024, 3, 15);
        picker.OpenCalendar();
        Assert.Equal(0, MeasureSteadyState(picker.OnDraw));

        // The year list, then typed entry in the field
        picker.OnPointerDown(new KumikoUI.Core.Input.KumikoPointerEventArgs { X = 150, Y = 50, Action = KumikoUI.Core.Input.InputAction.Pressed });
        Assert.Equal(0, MeasureSteadyState(picker.OnDraw));
        picker.OnPointerDown(new KumikoUI.Core.Input.KumikoPointerEventArgs { X = 500, Y = 500, Action = KumikoUI.Core.Input.InputAction.Pressed }); // closes the dropdown
        picker.IsFocused = true;
        picker.OpenCalendar();
        picker.OnKeyDown(new KumikoUI.Core.Input.KumikoKeyEventArgs { Key = KumikoUI.Core.Input.KumikoKey.Character, Character = '2' });
        Assert.Equal(0, MeasureSteadyState(picker.OnDraw));
    }

    [Fact]
    public void SteadyState_WrappedTextColumns_AllocatesNothing()
    {
        // Text cells of wrapping columns take the wrapping DrawTextInRect overload (#23); choosing it allocates nothing.
        var grid = CreateGrid(featured: false);
        grid.Source.Columns[0].TextWrap = KumikoTextWrap.Word;
        grid.Source.Columns[9].TextWrap = KumikoTextWrap.Character;

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void SteadyState_AutoColumnsWhileScrolling_AllocatesNothing()
    {
        // Auto columns sample their cells on the first frame and after a data change (#24); frames of unchanged data
        // reuse the measurement, also while the grid scrolls.
        var grid = CreateGrid(featured: false);
        foreach (var column in grid.Source.Columns)
            column.SizeMode = ColumnSizeMode.Auto;
        grid.Source.Columns[1].SizeMode = ColumnSizeMode.Star;
        int frame = 0;

        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Scroll.OffsetY = frame++ % 2 == 0 ? 0 : 5 * grid.Style.RowHeight + 7;
            grid.Render(ctx);
        }));
        Assert.True(grid.Source.Columns[0].ContentFitWidth > 0);
    }

    [Fact]
    public void ADataChange_MeasuresAutoColumnsOnce_ThenAllocatesNothingAgain()
    {
        var grid = CreateGrid(featured: false);
        grid.Source.Columns[0].SizeMode = ColumnSizeMode.Auto;
        var ctx = new NoOpDrawingContext();
        MeasureSteadyState(grid.Render);

        grid.Source.Refresh();
        long remeasure = Allocated(() => grid.Render(ctx));

        Assert.True(remeasure > 0, "a data change samples the Auto column's cells again");
        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void ChangingTheStyle_RebuildsThePaintsOnce_ThenAllocatesNothingAgain()
    {
        var grid = CreateGrid(featured: false);
        var ctx = new NoOpDrawingContext();
        MeasureSteadyState(grid.Render);

        grid.Style.HeaderBackgroundColor = new KumikoColor(1, 2, 3);
        long rebuild = Allocated(() => grid.Render(ctx));

        Assert.True(rebuild > 0, "a style change rebuilds the paints");
        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void AnItemPropertyChange_ReformatsOnlyThatRow()
    {
        var grid = CreateGrid(featured: false, notifying: true);
        var ctx = new NoOpDrawingContext();
        MeasureSteadyState(grid.Render);

        var item = (NotifyingRow)grid.Source.GetItem(2);
        item.Quantity++;
        long oneRow = Allocated(() => grid.Render(ctx));
        item.Quantity++;
        long secondChange = Allocated(() => grid.Render(ctx));

        // Five boxed ints and their text (the row's Quantity cells); reformatting all 300 visible cells would be ~15 KB.
        Assert.InRange(oneRow, 1, 1024);
        Assert.InRange(secondChange, 1, 1024);
        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    /// <summary>
    /// Bytes the current thread allocates over <see cref="MeasuredFrames"/> frames after warming up: the least of
    /// three measurements. A real per-frame allocation shows in every measurement; a one-off refill of the caches
    /// (the runtime's tiering, or another test touching shared state such as the accessor registry) shows in at
    /// most one, so the minimum is 0 exactly when the render loop allocates nothing.
    /// </summary>
    private static long MeasureSteadyState(Action<IDrawingContext> render)
    {
        var ctx = new NoOpDrawingContext();
        for (int i = 0; i < WarmUpFrames; i++)
            render(ctx);

        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
                render(ctx);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return least;
    }

    private static long Allocated(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    public sealed class NotifyingRow : System.ComponentModel.INotifyPropertyChanged
    {
        private int _quantity;

        public string Name { get; set; } = "";
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Quantity)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    private static Grid CreateGrid(bool featured, bool notifying = false)
    {
        var source = new KumikoSource();
        var style = new KumikoStyle();
        KumikoColumn[] columns;

        if (notifying)
        {
            columns = Enumerable.Range(0, VisibleColumns)
                .Select(i => new KumikoColumn
                {
                    Header = $"C{i}",
                    PropertyName = i % 2 == 0 ? nameof(NotifyingRow.Name) : nameof(NotifyingRow.Quantity),
                    Width = ColumnWidth,
                })
                .ToArray();
            source.SetColumns(columns);
            source.SetItems(Enumerable.Range(0, 1000)
                .Select(i => new NotifyingRow { Name = $"Row {i}", Quantity = i })
                .ToList());
        }
        else
        {
            columns = new[]
            {
                new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = ColumnWidth },
                new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric },
                new KumikoColumn { Header = "Price", PropertyName = nameof(Row.Price), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric, Format = "C2" },
                new KumikoColumn { Header = "Ratio", PropertyName = nameof(Row.Ratio), Width = 60, ColumnType = KumikoColumnType.Numeric, Format = "N3" },
                new KumikoColumn { Header = "Date", PropertyName = nameof(Row.Date), Width = ColumnWidth, ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
                new KumikoColumn { Header = "Active", PropertyName = nameof(Row.Active), Width = ColumnWidth, ColumnType = KumikoColumnType.Boolean },
                new KumikoColumn { Header = "City", PropertyName = nameof(Row.City), Width = ColumnWidth },
                new KumikoColumn { Header = "Code", PropertyName = nameof(Row.Code), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric },
                new KumikoColumn { Header = "Score", PropertyName = nameof(Row.Score), Width = ColumnWidth, ColumnType = KumikoColumnType.Numeric, Format = "N1" },
                new KumikoColumn { Header = "Notes", PropertyName = nameof(Row.Notes), Width = ColumnWidth },
            };
            source.SetColumns(columns);
            source.SetItems(Enumerable.Range(0, 2000).Select(i => new Row
            {
                Name = $"Row {i}",
                Quantity = i % 97,
                Price = 10m + i % 50,
                Ratio = i / 7.0,
                Date = new DateTime(2026, 1, 1).AddDays(i % 365),
                Active = (i % 3) switch { 0 => true, 1 => false, _ => null },
                City = (i % 4) switch { 0 => "Oslo", 1 => "Rome", 2 => "Lima", _ => "Kyiv" },
                Code = 1_000_000L + i,
                Score = i % 100 / 3f,
                Notes = $"Note {i % 10}",
            }).ToList());
        }

        if (featured)
        {
            columns[0].IsFrozen = true;
            columns[9].IsFrozenRight = true;
            columns[2].CellStyle = new KumikoCellStyle { BackgroundColor = new KumikoColor(250, 240, 230), TextColor = new KumikoColor(20, 20, 20) };
            columns[6].HeaderCellStyle = new KumikoCellStyle { BackgroundColor = new KumikoColor(200, 220, 240), TextColor = new KumikoColor(10, 10, 80) };
            columns[3].ColumnType = KumikoColumnType.ProgressBar;
            columns[3].ProgressMaximum = 300;
            columns[5].IsThreeState = true;
            columns[7].CustomCellRenderer = new ProgressBarCellRenderer { Minimum = 1_000_000, Maximum = 1_002_000 };
            columns[8].CustomCellRenderer = new ActionButtonsCellRenderer
            {
                Buttons = new List<IActionButtonDefinition>
                {
                    new ActionButtonDefinition { Label = "Edit", BackgroundColor = new KumikoColor(13, 110, 253) },
                    new ActionButtonDefinition { Label = "Delete", BackgroundColor = new KumikoColor(220, 53, 69) },
                },
            };

            var highlight = new KumikoRowStyle { BackgroundColor = new KumikoColor(255, 250, 200), TextColor = new KumikoColor(90, 0, 0) };
            var emphasis = new KumikoCellStyle { Font = new KumikoFont("Default", 13, bold: true) };
            style.RowStyleResolver = item => ((Row)item).Quantity % 5 == 0 ? highlight : null;
            style.CellStyleResolver = (item, col) => col.PropertyName == nameof(Row.Price) && ((Row)item).Price > 40 ? emphasis : null;
            style.ShowRowDragHandle = true;

            source.BeginUpdate();
            source.ToggleSort(columns[1]);
            source.ToggleMultiSort(columns[2]);
            source.SetColumnFilter(columns[6], new FilterDescription(columns[6])
            {
                Condition1 = new FilterCondition { TextOperator = TextFilterOperator.NotEquals, Value = "Never" },
            });
            source.AddGroupDescription(new GroupDescription(nameof(Row.City)));
            source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Top", Title = "Totals", Position = SummaryPosition.Top,
                // Ratio is too narrow for its value, which is drawn after the title instead
                Columns = { new SummaryColumnDescription(nameof(Row.Quantity), SummaryType.Sum), new SummaryColumnDescription(nameof(Row.Ratio), SummaryType.Max) },
            });
            source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Bottom", Title = "Averages", Position = SummaryPosition.Bottom,
                Columns = { new SummaryColumnDescription(nameof(Row.Price), SummaryType.Average) },
            });
            source.AddGroupSummaryRow(new GroupSummaryRow
            {
                Name = "Group", Title = "Group total",
                Columns = { new SummaryColumnDescription(nameof(Row.Quantity), SummaryType.Sum), new SummaryColumnDescription(nameof(Row.Ratio), SummaryType.Max) },
            });
            source.CaptionSummary = new CaptionSummaryDescription
            {
                Columns = { new SummaryColumnDescription(nameof(Row.Price), SummaryType.Sum) },
            };
            source.EndUpdate();
        }

        float width = columns.Sum(c => c.Width) + (featured ? style.RowDragHandleWidth : 0);
        var scroll = new ScrollState
        {
            ViewportWidth = width,
            ViewportHeight = style.HeaderHeight + VisibleRows * style.RowHeight
                + (featured ? style.GroupPanelHeight + 2 * style.SummaryRowHeight : 0),
        };
        var selection = new KumikoSelectionModel(source);
        selection.HandleRowSelection(featured ? 4 : 3, shift: false, ctrl: false);
        selection.SetCurrentCell(featured ? 4 : 3, 1);
        return new Grid(new KumikoRenderer(), source, scroll, selection, style);
    }
}
