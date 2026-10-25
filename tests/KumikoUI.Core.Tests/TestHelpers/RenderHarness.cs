using System.Reflection;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// A grid wired to a <see cref="DataGridRenderer"/> and a <see cref="RecordingDrawingContext"/>.
/// Every <see cref="GridColor"/> on the style is given a distinct value, so a recorded colour
/// identifies the style role that produced it.
/// </summary>
/// <remarks>
/// Default geometry (DataGridStyle defaults): header 40 px, rows 36 px, summary rows 36 px,
/// grouping panel 40 px, cell and header padding 8 px. Default columns: Name 150 px (x 0),
/// Age 100 px (x 150), City 150 px (x 250), Salary 150 px (x 400) — 550 px of content.
/// </remarks>
internal sealed class RenderHarness
{
    public sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
        public decimal Salary { get; set; }
        public bool Active { get; set; }
    }

    public DataGridSource Source { get; } = new();
    public DataGridStyle Style { get; } = WithDistinctColors(new DataGridStyle());
    public ScrollState Scroll { get; }
    public DataGridSelectionModel Selection { get; } = new();
    public DataGridRenderer Renderer { get; } = new();
    public List<Person> People { get; }

    private RenderHarness(float width, float height, List<Person> people)
    {
        Scroll = new ScrollState { ViewportWidth = width, ViewportHeight = height };
        People = people;
    }

    /// <summary>
    /// Creates a grid with <paramref name="rows"/> people (Name "P00", "P01", …; Age 20–24 cycling;
    /// City alternating Oslo/Rome; Salary 1000 + 10·i; Active every third row).
    /// </summary>
    public static RenderHarness Create(int rows = 20, float width = 600, float height = 400,
        Action<RenderHarness>? configure = null)
    {
        var people = Enumerable.Range(0, rows).Select(i => new Person
        {
            Name = $"P{i:D2}",
            Age = 20 + i % 5,
            City = i % 2 == 0 ? "Oslo" : "Rome",
            Salary = 1000m + i * 10m,
            Active = i % 3 == 0,
        }).ToList();

        var h = new RenderHarness(width, height, people);
        h.Source.SetColumns(new[]
        {
            new DataGridColumn { Header = "Name", PropertyName = "Name", Width = 150 },
            new DataGridColumn { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = DataGridColumnType.Numeric },
            new DataGridColumn { Header = "City", PropertyName = "City", Width = 150 },
            new DataGridColumn { Header = "Salary", PropertyName = "Salary", Width = 150, ColumnType = DataGridColumnType.Numeric },
        });
        h.Source.SetItems(people);
        configure?.Invoke(h);
        return h;
    }

    public DataGridColumn Column(string propertyName) =>
        Source.Columns.Single(c => c.PropertyName == propertyName);

    public RecordingDrawingContext Render(
        int dragColumnIndex = -1, float dragColumnScreenX = 0,
        int dragRowIndex = -1, float dragRowScreenY = 0,
        EditSession? editSession = null, PopupManager? popupManager = null)
    {
        var ctx = new RecordingDrawingContext();
        Renderer.Render(ctx, Source, Scroll, Selection, Style,
            dragColumnIndex, dragColumnScreenX, dragRowIndex, dragRowScreenY, editSession, popupManager);
        return ctx;
    }

    /// <summary>Viewport Y of a scrollable row's top edge (no grouping panel, frozen rows or top summaries).</summary>
    public float RowTop(int row) => Style.HeaderHeight + row * Style.RowHeight - Scroll.OffsetY;

    /// <summary>The full-width row background fill at <paramref name="y"/>, drawn by the pass with that clip.</summary>
    public static IEnumerable<DrawOp> RowBackgrounds(RecordingDrawingContext ctx, float y, float viewportWidth) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRect && Near(o.Rect.Y, y) && Near(o.Rect.X, 0) && o.Rect.Width >= viewportWidth);

    public static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

    private static DataGridStyle WithDistinctColors(DataGridStyle style)
    {
        byte n = 1;
        foreach (var p in typeof(DataGridStyle).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType == typeof(GridColor) && p.CanWrite)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            p.SetValue(style, new GridColor(n, (byte)(255 - n), 77));
            n++;
        }
        return style;
    }
}

/// <summary>A drawn component that fills its bounds, to observe where and when it is drawn.</summary>
internal sealed class ProbeComponent : DrawnComponent
{
    public static readonly GridColor ProbeColor = new(1, 2, 3);

    public override void OnDraw(IDrawingContext ctx) =>
        ctx.FillRect(Bounds, new GridPaint { Color = ProbeColor });
}
