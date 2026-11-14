using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// A grid wired to an input controller, with helpers that synthesize pointer and key sequences.
/// </summary>
/// <remarks>
/// Default geometry: header 40 px, rows 36 px, summary rows 36 px, grouping panel 40 px.
/// Columns: Name 120 (x 0), Age 80 (x 120), City 100 (x 200), Dept 100 (x 300), Score 100 (x 400),
/// so 500 px of content in a 300 px viewport. Row <c>r</c> sits at viewport y <c>40 + 36 r</c>.
/// </remarks>
internal sealed class InputGrid
{
    public sealed class Item
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
        public string Dept { get; set; } = "";
        public int Score { get; set; }
    }

    public const float ViewportWidth = 300f;
    public const float DefaultViewportHeight = 256f; // header + 6 rows
    public const float HeaderY = 20f;

    public List<Item> Items { get; }
    public KumikoSource Source { get; } = new();
    public KumikoStyle Style { get; } = new();
    public ScrollState Scroll { get; }
    public KumikoSelectionModel Selection { get; }
    public EditSession Edit { get; } = new();
    public KumikoInputController Input { get; } = new();
    public KumikoRenderer Renderer { get; } = new();
    public KumikoHitTester Tester { get; } = new();
    public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 120 };
    public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", Width = 80, ColumnType = KumikoColumnType.Numeric };
    public KumikoColumn City { get; } = new() { Header = "City", PropertyName = "City", Width = 100 };
    public KumikoColumn Dept { get; } = new() { Header = "Dept", PropertyName = "Dept", Width = 100 };
    public KumikoColumn Score { get; } = new() { Header = "Score", PropertyName = "Score", Width = 100, ColumnType = KumikoColumnType.Numeric };
    public int Redraws { get; private set; }
    /// <summary>Every <see cref="KumikoInputController.CursorChanged"/>, in order.</summary>
    public List<KumikoCursor> Cursors { get; } = new();

    public InputGrid(int rows = 30, float viewportHeight = DefaultViewportHeight, float viewportWidth = ViewportWidth,
        bool withEditSession = true, Action<InputGrid>? configure = null)
    {
        Items = Enumerable.Range(0, rows).Select(i => new Item
        {
            Name = $"N{i:D2}",
            Age = 20 + i % 5,
            City = i % 2 == 0 ? "Oslo" : "Rome",
            Dept = i % 3 == 0 ? "Eng" : "Ops",
            Score = i * 10,
        }).ToList();
        Scroll = new ScrollState { ViewportWidth = viewportWidth, ViewportHeight = viewportHeight };
        Selection = new KumikoSelectionModel(Source);
        Source.SetColumns(new[] { Name, Age, City, Dept, Score });
        Source.SetItems(Items);
        Edit.Style = Style;
        if (withEditSession) Input.EditSession = Edit;
        Input.NeedsRedraw += (_, _) => Redraws++;
        Input.CursorChanged += (_, cursor) => Cursors.Add(cursor);
        configure?.Invoke(this);
        Render();
    }

    /// <summary>Re-render so content sizes follow the current columns, rows and bands.</summary>
    public void Render()
    {
        Renderer.Render(new RecordingDrawingContext(), Source, Scroll, Selection, Style, editSession: Edit);
    }

    /// <summary>Viewport y of the vertical middle of row <paramref name="row"/> with no scroll, bands or grouping.</summary>
    public float RowY(int row) => Style.HeaderHeight + row * Style.RowHeight + Style.RowHeight / 2;

    // ── Key input ──

    public KumikoKeyEventArgs Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None, char? character = null)
    {
        var e = new KumikoKeyEventArgs { Key = key, Modifiers = modifiers, Character = character };
        Input.HandleKey(e, Scroll, Selection, Style, Source);
        return e;
    }

    public KumikoKeyEventArgs Type(char c) => Key(KumikoKey.Character, character: c);

    public void Select(int row, int column) => Selection.CurrentCell = new CellPosition(row, column);

    // ── Pointer input ──

    public KumikoPointerEventArgs Pointer(InputAction action, float x, float y, InputModifiers modifiers = InputModifiers.None,
        int clicks = 1, long timestampMs = 0, PointerButton button = PointerButton.Primary, float scrollDx = 0, float scrollDy = 0,
        long pointerId = 0, KumikoScrollUnit scrollUnit = KumikoScrollUnit.Notches,
        KumikoPointerDeviceType deviceType = KumikoPointerDeviceType.Mouse)
    {
        var e = new KumikoPointerEventArgs
        {
            X = x, Y = y, Action = action, Modifiers = modifiers, ClickCount = clicks,
            TimestampMs = timestampMs, Button = button, ScrollDeltaX = scrollDx, ScrollDeltaY = scrollDy,
            PointerId = pointerId, ScrollUnit = scrollUnit, DeviceType = deviceType,
        };
        Input.HandlePointer(e, Scroll, Selection, Style, Source);
        return e;
    }

    public void Press(float x, float y, InputModifiers modifiers = InputModifiers.None, int clicks = 1) =>
        Pointer(InputAction.Pressed, x, y, modifiers, clicks);

    public void Move(float x, float y, long timestampMs = 0) => Pointer(InputAction.Moved, x, y, timestampMs: timestampMs);

    /// <summary>A mouse move with no button pressed.</summary>
    public KumikoPointerEventArgs Hover(float x, float y) => Pointer(InputAction.Hover, x, y, button: PointerButton.None);

    public void Release(float x, float y, InputModifiers modifiers = InputModifiers.None, int clicks = 1) =>
        Pointer(InputAction.Released, x, y, modifiers, clicks);

    public void Tap(float x, float y, InputModifiers modifiers = InputModifiers.None, int clicks = 1)
    {
        Press(x, y, modifiers, clicks);
        Release(x, y, modifiers, clicks);
    }

    public KumikoHitTestResult Hit(float x, float y) =>
        Tester.HitTest(x, y, Scroll, Style, Source.Columns, Source.RowCount, Source);

    public string NameAt(int row) => (string)Source.GetCellValue(row, Name)!;
}
