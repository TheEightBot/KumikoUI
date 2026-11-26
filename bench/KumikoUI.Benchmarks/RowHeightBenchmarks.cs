using BenchmarkDotNet.Attributes;
using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Benchmarks;

[KumikoGridItem]
public class NoteRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Address { get; set; } = "";
    public int Quantity { get; set; }
}

/// <summary>
/// The row offset index behind variable row height (#320, spec #102 §8): row to y and y to row at 100,000 and
/// 1,000,000 rows of measured, varying heights, re-measuring one row (a point update), and re-mapping every height
/// after a sort (a rebuild of the index from the per-item cache).
/// </summary>
[MemoryDiagnoser]
public class RowLayoutBenchmarks
{
    [Params(100_000, 1_000_000)]
    public int Rows { get; set; }

    private readonly KumikoStyle _style = new() { RowHeightMode = KumikoRowHeightMode.Auto };
    private KumikoSource _source = null!;
    private KumikoRowLayout _layout = null!;
    private double _total;
    private int _probe;

    [GlobalSetup]
    public void Setup()
    {
        _source = new KumikoSource();
        _source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Id", PropertyName = nameof(NoteRow.Id), Width = 80 },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(NoteRow.Quantity), Width = 80 },
        });
        _source.SetItems(Enumerable.Range(0, Rows).Select(i => new NoteRow { Id = i, Quantity = (i * 7919) % 1000 }).ToList());
        _layout = _source.RowLayout;
        _layout.BeginFrame(_style, _source.Columns);
        for (int r = 0; r < Rows; r++)
            _layout.SetMeasuredHeight(r, _source.GetItem(r), 24 + (r * 37) % 90);
        _total = _layout.GetTotalHeight(_style);
    }

    /// <summary>Tops of rows far apart (no sequential shortcut): two Fenwick prefix queries each.</summary>
    [Benchmark]
    public double RowToY()
    {
        _probe = (_probe + 7_919) % Rows;
        return _layout.GetRowTop(_probe, _style);
    }

    /// <summary>The row at a y: binary lifting over both trees.</summary>
    [Benchmark]
    public int YToRow()
    {
        _probe = (_probe + 7_919) % Rows;
        return _layout.GetRowAt(_total * _probe / Rows, _style);
    }

    /// <summary>One row measured at a new height: two point updates.</summary>
    [Benchmark]
    public bool MeasureOneRow()
    {
        _probe = (_probe + 7_919) % Rows;
        _layout.InvalidateItem(_source.GetItem(_probe));
        return _layout.SetMeasuredHeight(_probe, _source.GetItem(_probe), 30 + _probe % 50);
    }

    /// <summary>
    /// A view rebuild (here a sort) followed by the first query: one cache lookup per row and a linear build of the
    /// trees, on top of the sort itself.
    /// </summary>
    [Benchmark]
    public double SortThenFirstQuery()
    {
        _source.ToggleSort(_source.Columns[1]);
        return _layout.GetRowTop(Rows / 2, _style);
    }
}

/// <summary>
/// A no-op context that also implements the wrapping members (as the SkiaSharp context does, with its layout cache),
/// so the benchmark measures the render loop and not the allocating default interface members: text is 7 px per
/// character, lines 16 px apart.
/// </summary>
public sealed class NoOpWrappingDrawingContext : IDrawingContext
{
    public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
    public void FillRect(KumikoRect rect, KumikoPaint paint) { }
    public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { }
    public void DrawText(string text, float x, float y, KumikoPaint paint) { }
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true)
    { }
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, KumikoTextAlignment hAlign,
        KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap)
    { }
    public KumikoSize MeasureText(string text, KumikoPaint paint) => new(text.Length * 7f, 14f);
    public KumikoSize MeasureText(string text, KumikoPaint paint, float maxWidth, KumikoTextWrap wrap)
    {
        float width = text.Length * 7f;
        int lines = wrap == KumikoTextWrap.None || maxWidth <= 0 ? 1 : Math.Max(1, (int)MathF.Ceiling(width / maxWidth));
        return new KumikoSize(Math.Min(width, maxWidth), 14f + (lines - 1) * 16f);
    }
    public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => new(-11, 3, 2);
    public void ClipRect(KumikoRect rect) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, KumikoRect destRect) { }
}

/// <summary>
/// One <see cref="KumikoRenderer.Render"/> frame of a 30-row grid in <see cref="KumikoRowHeightMode.Auto"/> mode with
/// two wrapping columns, into <see cref="NoOpWrappingDrawingContext"/> (#320), so rows are of several heights. <c>AutoSteady</c> measures
/// the steady state, where every visible row is measured and nothing allocates; <c>AutoScrolling</c> moves a row and
/// 7 px further each frame, so each frame measures the row it reveals.
/// </summary>
[MemoryDiagnoser]
public class AutoRowHeightRenderBenchmarks
{
    private readonly NoOpWrappingDrawingContext _ctx = new();
    private readonly KumikoRenderer _renderer = new();
    private KumikoSource _source = null!;
    private ScrollState _scroll = null!;
    private KumikoSelectionModel _selection = null!;
    private KumikoStyle _style = null!;
    private int _step;

    [GlobalSetup]
    public void Setup()
    {
        _source = new KumikoSource();
        _source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Id", PropertyName = nameof(NoteRow.Id), Width = 80 },
            new KumikoColumn { Header = "Title", PropertyName = nameof(NoteRow.Title), Width = 160 },
            new KumikoColumn { Header = "Notes", PropertyName = nameof(NoteRow.Notes), Width = 280, TextWrap = KumikoTextWrap.Word },
            new KumikoColumn { Header = "Address", PropertyName = nameof(NoteRow.Address), Width = 180, TextWrap = KumikoTextWrap.Word },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(NoteRow.Quantity), Width = 100, ColumnType = KumikoColumnType.Numeric },
        });
        _source.SetItems(Enumerable.Range(0, 10_000).Select(i => new NoteRow
        {
            Id = i,
            Title = $"Ticket {i}",
            Notes = string.Join(' ', Enumerable.Repeat("note", 1 + i % 9)),
            Address = $"{i} Main Street, Springfield",
            Quantity = i % 97,
        }).ToList());
        _style = new KumikoStyle { RowHeightMode = KumikoRowHeightMode.Auto };
        _scroll = new ScrollState { ViewportWidth = 800, ViewportHeight = _style.HeaderHeight + 30 * _style.RowHeight };
        _selection = new KumikoSelectionModel(_source);
        for (int i = 0; i < 5; i++) Render();
    }

    [Benchmark(Baseline = true)]
    public void Render_30x5_AutoSteady() => Render();

    [Benchmark]
    public void Render_30x5_AutoScrolling()
    {
        _step = (_step + 1) % 200;
        _scroll.OffsetY = _step * (_style.RowHeight + 7);
        Render();
    }

    private void Render() => _renderer.Render(_ctx, _source, _scroll, _selection, _style);
}
