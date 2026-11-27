using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// A grid for variable row height tests (#320), drawn on a <see cref="RecordingDrawingContext"/>: text is 8 px per
/// character and 14 px tall, lines are 16 px apart. Columns: Name 150 (x 0), Notes 150 (x 150, wraps by character),
/// Age 100 (x 300). A Notes cell is 134 px wide inside its padding, so 16 characters per line: a note of
/// <c>n</c> lines makes an <see cref="KumikoRowHeightMode.Auto"/> row <c>14 + 16 (n - 1) + 12</c> px tall (at least
/// 36). Viewport 600 x 400; the header is 40 px, so the rows' area is 360 px.
/// </summary>
internal sealed class RowHeightGrid
{
    public sealed class Note : INotifyPropertyChanged
    {
        private string _text = "";
        private int _age;

        public string Name { get; set; } = "";

        public string Text
        {
            get => _text;
            set { _text = value; Raise(); }
        }

        public int Age
        {
            get => _age;
            set { _age = value; Raise(); }
        }

        public bool Pinned { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Counts <see cref="IMeasurableCellRenderer.MeasureHeight"/> calls and measures like the text renderer.</summary>
    public sealed class CountingTextRenderer : TextCellRenderer
    {
        public int Calls { get; set; }

        public override float? MeasureHeight(IDrawingContext ctx, in CellRenderContext cell)
        {
            Calls++;
            return base.MeasureHeight(ctx, in cell);
        }
    }

    public const float Header = 40f;
    public const float Width = 600f;
    public const float Height = 400f;

    /// <summary>A note <paramref name="lines"/> lines long (16 characters per line).</summary>
    public static string Lines(int lines) => new('x', 16 * (lines - 1) + 4);

    /// <summary>The Auto height of a row whose note has <paramref name="lines"/> lines.</summary>
    public static float AutoHeight(int lines) => Math.Max(36f, 14 + 16f * (lines - 1) + 12);

    public List<Note> Items { get; }
    public KumikoSource Source { get; } = new();
    public KumikoStyle Style { get; } = new() { RowHeightMode = KumikoRowHeightMode.Auto };
    public ScrollState Scroll { get; } = new() { ViewportWidth = Width, ViewportHeight = Height };
    public KumikoSelectionModel Selection { get; }
    public KumikoRenderer Renderer { get; } = new();
    public KumikoInputController Input { get; } = new();
    public KumikoHitTester Tester { get; } = new();
    public CountingTextRenderer Counter { get; } = new();
    public KumikoColumn NameColumn { get; } = new() { Header = "Name", PropertyName = nameof(Note.Name), Width = 150 };
    public KumikoColumn NotesColumn { get; } = new() { Header = "Notes", PropertyName = nameof(Note.Text), Width = 150, TextWrap = KumikoTextWrap.Character };
    public KumikoColumn AgeColumn { get; } = new() { Header = "Age", PropertyName = nameof(Note.Age), Width = 100, ColumnType = KumikoColumnType.Numeric };

    /// <summary>Builds a grid of notes whose length follows <paramref name="lines"/>.</summary>
    /// <param name="lines">The number of lines of each row's note, by row index.</param>
    /// <param name="rows">How many rows.</param>
    public RowHeightGrid(Func<int, int> lines, int rows = 40)
    {
        Items = Enumerable.Range(0, rows)
            .Select(i => new Note { Name = $"N{i:D3}", Text = Lines(lines(i)), Age = 20 + i % 7, Pinned = i % 5 == 0 })
            .ToList();
        NotesColumn.CustomCellRenderer = Counter;
        Source.SetColumns(new[] { NameColumn, NotesColumn, AgeColumn });
        Source.SetItems(Items);
        Selection = new KumikoSelectionModel(Source);
        Renderer.InputController = Input;
    }

    public KumikoRowLayout Rows => Source.RowLayout;

    public RecordingDrawingContext Render()
    {
        var ctx = new RecordingDrawingContext();
        Renderer.Render(ctx, Source, Scroll, Selection, Style);
        return ctx;
    }

    /// <summary>The viewport y of <paramref name="row"/>'s top edge now.</summary>
    public float Y(int row) => KumikoScroller.GetRowY(Source, Style, Scroll, row);

    /// <summary>The height of <paramref name="row"/> now.</summary>
    public float H(int row) => KumikoScroller.GetRowHeight(Source, Style, row);

    public KumikoHitTestResult HitTest(float x, float y) =>
        Tester.HitTest(x, y, Scroll, Style, Source.Columns, Source.RowCount, Source);

    public void Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None) =>
        Input.HandleKey(new KumikoKeyEventArgs { Key = key, Modifiers = modifiers }, Scroll, Selection, Style, Source);
}
