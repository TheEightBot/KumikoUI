using KumikoUI.Core;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.SkiaSharp.Tests;

/// <summary>
/// A steady-state frame drawn through <see cref="SkiaDrawingContext"/> onto a real <see cref="SKSurface"/> allocates
/// no managed memory (#251). The Core render loop is already allocation-free against a no-op drawing context; this
/// covers the context itself: its path and paint, the text runs, the font lookups.
/// </summary>
/// <remarks>
/// Measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/>, the fewest bytes over several runs after
/// warm-up (as the Core performance budget tests do): a one-off allocation (a type loaded, a method tiered up)
/// lands in at most one run, an allocation the frame makes lands in every one.
/// </remarks>
[Collection(SkiaRegistrarCollection.Name)]
public class SkiaAllocationTests : IDisposable
{
    private const int Width = 900;
    private const int Height = 520;

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public bool? Active { get; set; }
        public string City { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public SkiaAllocationTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        TestFonts.Reset(withCjkFallback: true);
    }

    public void Dispose() => SkiaFontRegistrar.Clear();

    /// <summary>A grid with headers, sort, grouping with summaries and a Boolean column.</summary>
    private sealed class Scene
    {
        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; }
        public ScrollState Scroll { get; } = new() { ViewportWidth = Width, ViewportHeight = Height };
        public KumikoSelectionModel Selection { get; }
        public KumikoRenderer Renderer { get; } = new();
        public EditSession Edit { get; } = new();
        public KumikoInputController Input { get; } = new();
        public KumikoColumn[] Columns { get; }

        public Scene(string[] cities, string[] notes)
        {
            Style = new KumikoStyle
            {
                HeaderFont = new KumikoFont(TestFonts.Latin, 14, bold: true),
                CellFont = new KumikoFont(TestFonts.Latin, 13),
            };
            Columns = new[]
            {
                new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 150 },
                new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 100, ColumnType = KumikoColumnType.Numeric },
                new KumikoColumn { Header = "Price", PropertyName = nameof(Row.Price), Width = 100, ColumnType = KumikoColumnType.Numeric, Format = "C2" },
                new KumikoColumn { Header = "Active", PropertyName = nameof(Row.Active), Width = 90, ColumnType = KumikoColumnType.Boolean, IsThreeState = true },
                new KumikoColumn { Header = "City", PropertyName = nameof(Row.City), Width = 150 },
                new KumikoColumn { Header = "Notes", PropertyName = nameof(Row.Notes), Width = 250 },
            };
            Source.SetColumns(Columns);
            Source.SetItems(Enumerable.Range(0, 400).Select(i => new Row
            {
                Name = $"Row {i}",
                Quantity = i % 97,
                Price = 10m + i % 50,
                Active = (i % 3) switch { 0 => true, 1 => false, _ => null },
                City = cities[i % cities.Length],
                Notes = notes[i % notes.Length],
            }).ToList());

            Source.BeginUpdate();
            Source.ToggleSort(Columns[1]);
            Source.AddGroupDescription(new GroupDescription(nameof(Row.City)));
            Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Top", Title = "Totals", Position = SummaryPosition.Top,
                Columns = { new SummaryColumnDescription(nameof(Row.Quantity), SummaryType.Sum) },
            });
            Source.AddGroupSummaryRow(new GroupSummaryRow
            {
                Name = "Group", Title = "Group total",
                Columns = { new SummaryColumnDescription(nameof(Row.Price), SummaryType.Average) },
            });
            Source.EndUpdate();

            Selection = new KumikoSelectionModel(Source);
            Selection.HandleRowSelection(4, shift: false, ctrl: false);
            Selection.SetCurrentCell(4, 1);
        }

        /// <summary>Whether the frame draws the popups too, as the hosts do (they pass the popup manager every frame).</summary>
        public bool DrawPopups { get; set; }

        public void Render(IDrawingContext ctx) =>
            Renderer.Render(ctx, Source, Scroll, Selection, Style, editSession: Edit,
                popupManager: DrawPopups ? Input.PopupManager : null);
    }

    private static readonly string[] Cities = { "Oslo", "Rome", "Lima", "Kyiv" };
    private static readonly string[] Notes =
        { "Plain note", "A much longer note that will be cut with an ellipsis at the cell edge", "José Müller 東京" };

    private const int WarmUpFrames = 10;
    private const int FramesPerRun = 20;
    private const int Runs = 5;

    /// <summary>
    /// The fewest bytes <see cref="FramesPerRun"/> frames allocated over several runs after warm-up, in the whole
    /// frame or (<paramref name="insideContextCallsOnly"/>) only inside calls on the drawing context.
    /// </summary>
    private static long Allocated(SKCanvas canvas, SkiaResourceCache cache, Scene scene, bool insideContextCallsOnly = false)
    {
        var counting = new AllocationCountingContext();
        void Frame()
        {
            using var ctx = cache.CreateContext(canvas);
            if (insideContextCallsOnly)
            {
                counting.Inner = ctx;
                scene.Render(counting);
            }
            else
            {
                scene.Render(ctx);
            }
        }

        for (int i = 0; i < WarmUpFrames; i++) Frame();
        long fewest = long.MaxValue;
        for (int run = 0; run < Runs && fewest > 0; run++)
        {
            counting.Bytes = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < FramesPerRun; i++) Frame();
            long total = GC.GetAllocatedBytesForCurrentThread() - before;
            fewest = Math.Min(fewest, insideContextCallsOnly ? counting.Bytes : total);
        }
        return fewest;
    }

    [Fact]
    public void A_full_featured_grid_allocates_nothing_in_a_steady_state_frame()
    {
        var scene = new Scene(Cities, Notes);
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        using var cache = new SkiaResourceCache();

        long bytes = Allocated(surface.Canvas, cache, scene);

        _output.WriteLine($"{bytes} B per {FramesPerRun} frames");
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void An_open_editor_and_filter_popup_cost_the_drawing_context_no_allocation()
    {
        // The editor and the popup draw themselves in Core, which still builds a few paints per frame for their chrome
        // (EditSession, PopupManager, DrawnFilterPopup); that is not the drawing context's cost, so only the bytes
        // allocated inside calls on the context are counted here.
        var scene = new Scene(Cities, Notes);
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        using var cache = new SkiaResourceCache();
        using (var ctx = cache.CreateContext(surface.Canvas)) scene.Render(ctx);

        // The funnel button is at the right end of the City header.
        float cityX = scene.Columns[0].Width + scene.Columns[1].Width + scene.Columns[2].Width + scene.Columns[3].Width;
        float headerY = scene.Style.GroupPanelHeight + scene.Style.HeaderHeight / 2;
        foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
            scene.Input.HandlePointer(new KumikoPointerEventArgs { X = cityX + 130, Y = headerY, Action = action },
                scene.Scroll, scene.Selection, scene.Style, scene.Source);
        Assert.True(scene.Input.PopupManager.HasActivePopups, "the filter popup is open");
        Assert.True(scene.Edit.BeginEdit(2, 0, scene.Columns[0], scene.Source, new KumikoRect(0, 200, 150, 36)));
        scene.DrawPopups = true;

        long bytes = Allocated(surface.Canvas, cache, scene, insideContextCallsOnly: true);

        _output.WriteLine($"{bytes} B inside context calls per {FramesPerRun} frames");
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void A_frame_started_with_the_constructor_allocates_only_the_context_object()
    {
        var scene = new Scene(Cities, Notes);
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        using var cache = new SkiaResourceCache();
        void Frame()
        {
            using var ctx = new SkiaDrawingContext(surface.Canvas, cache);
            scene.Render(ctx);
        }

        for (int i = 0; i < WarmUpFrames; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frame();
        long perFrame = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(perFrame, 1, 128);
    }

    [Fact]
    public void A_nested_frame_gets_its_own_context_and_the_shared_one_is_reused_afterwards()
    {
        using var surface = SKSurface.Create(new SKImageInfo(10, 10));
        using var cache = new SkiaResourceCache();

        var first = cache.CreateContext(surface.Canvas);
        var nested = cache.CreateContext(surface.Canvas);
        Assert.NotSame(first, nested);
        nested.Dispose();
        first.Dispose();

        var again = cache.CreateContext(surface.Canvas);
        Assert.Same(first, again);
        again.Dispose();
        Assert.Throws<ArgumentNullException>(() => cache.CreateContext(null!));
    }

    /// <summary>Forwards to a drawing context and adds up the bytes allocated inside its methods.</summary>
    private sealed class AllocationCountingContext : IDrawingContext
    {
        public IDrawingContext Inner { get; set; } = null!;
        public long Bytes { get; set; }

        private long Begin() => GC.GetAllocatedBytesForCurrentThread();
        private void End(long start) => Bytes += GC.GetAllocatedBytesForCurrentThread() - start;

        public void DrawRect(KumikoRect rect, KumikoPaint paint) { long s = Begin(); Inner.DrawRect(rect, paint); End(s); }
        public void FillRect(KumikoRect rect, KumikoPaint paint) { long s = Begin(); Inner.FillRect(rect, paint); End(s); }
        public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { long s = Begin(); Inner.DrawRoundRect(rect, cornerRadius, paint); End(s); }
        public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { long s = Begin(); Inner.FillRoundRect(rect, cornerRadius, paint); End(s); }
        public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { long s = Begin(); Inner.DrawLine(x1, y1, x2, y2, paint); End(s); }
        public void FillPolygon(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint) { long s = Begin(); Inner.FillPolygon(points, paint); End(s); }
        public void DrawPolyline(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint) { long s = Begin(); Inner.DrawPolyline(points, paint); End(s); }
        public void DrawText(string text, float x, float y, KumikoPaint paint) { long s = Begin(); Inner.DrawText(text, x, y, paint); End(s); }
        public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
            KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true) { long s = Begin(); Inner.DrawTextInRect(text, rect, paint, hAlign, vAlign, clip); End(s); }
        public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, KumikoTextAlignment hAlign,
            KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap) { long s = Begin(); Inner.DrawTextInRect(text, rect, paint, hAlign, vAlign, clip, wrap); End(s); }
        public KumikoSize MeasureText(string text, KumikoPaint paint) { long s = Begin(); var r = Inner.MeasureText(text, paint); End(s); return r; }
        public KumikoSize MeasureText(string text, KumikoPaint paint, float maxWidth, KumikoTextWrap wrap) { long s = Begin(); var r = Inner.MeasureText(text, paint, maxWidth, wrap); End(s); return r; }
        public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) { long s = Begin(); var r = Inner.GetFontMetrics(paint); End(s); return r; }
        public void ClipRect(KumikoRect rect) { long s = Begin(); Inner.ClipRect(rect); End(s); }
        public void Save() { long s = Begin(); Inner.Save(); End(s); }
        public void Restore() { long s = Begin(); Inner.Restore(); End(s); }
        public void Translate(float dx, float dy) { long s = Begin(); Inner.Translate(dx, dy); End(s); }
        public void DrawImage(object image, KumikoRect destRect) { long s = Begin(); Inner.DrawImage(image, destRect); End(s); }
    }
}
