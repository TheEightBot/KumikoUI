using KumikoUI.Core.Input;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Clipboard support (#44): Ctrl/Cmd+C copies the selected rows as TSV, and the open editor copies, cuts and
/// pastes through the host's <see cref="IKumikoClipboard"/>.
/// </summary>
public class ClipboardTests
{
    private sealed class FakeClipboard : IKumikoClipboard
    {
        public string? Text { get; set; }
        public bool Accepts { get; set; } = true;
        public Task<string?> Pending { get; set; } = Task.FromResult<string?>(null);
        public bool Async { get; set; }
        public int Sets { get; private set; }

        public bool TrySetText(string text)
        {
            if (!Accepts) return false;
            Sets++;
            Text = text;
            return true;
        }

        public Task<string?> GetTextAsync() => Async ? Pending : Task.FromResult(Text);
    }

    private static (InputGrid Grid, FakeClipboard Clipboard) Grid(Action<InputGrid>? configure = null)
    {
        var clipboard = new FakeClipboard();
        var g = new InputGrid(configure: x => { x.Input.Clipboard = clipboard; configure?.Invoke(x); });
        return (g, clipboard);
    }

    private static void SelectRows(InputGrid g, params int[] rows)
    {
        foreach (int row in rows) g.Selection.HandleRowSelection(row, false, true);
    }

    // ── TSV formatting ────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("a\tb", "\"a\tb\"")]
    [InlineData("a\nb", "\"a\nb\"")]
    [InlineData("a\r\nb", "\"a\r\nb\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    public void EscapeField_QuotesOnlyWhatNeedsIt(string? field, string expected)
    {
        Assert.Equal(expected, KumikoTsvFormatter.EscapeField(field));
    }

    [Fact]
    public void Format_WritesVisibleColumnsInDisplayOrder_OneRowPerLine()
    {
        var g = new InputGrid();
        g.Dept.IsVisible = false;

        string text = KumikoTsvFormatter.Format(g.Source, new[] { 2, 1 }, includeHeaders: false);

        Assert.Equal("N01\t21\tRome\t10\r\nN02\t22\tOslo\t20\r\n", text);
    }

    [Fact]
    public void Format_WritesFrozenPanesInTheOrderTheGridShowsThem()
    {
        var g = new InputGrid();
        g.Dept.IsVisible = false;
        g.City.IsFrozen = true;       // drawn first, in the left pane
        g.Name.IsFrozenRight = true;  // drawn last, in the right pane

        string text = KumikoTsvFormatter.Format(g.Source, new[] { 1 }, includeHeaders: true);

        Assert.Equal("City\tAge\tScore\tName\r\nRome\t21\t10\tN01\r\n", text);
    }

    [Fact]
    public void Format_WithHeaders_StartsWithTheHeaderRow()
    {
        var g = new InputGrid();

        string text = KumikoTsvFormatter.Format(g.Source, new[] { 0 }, includeHeaders: true);

        Assert.Equal("Name\tAge\tCity\tDept\tScore\r\nN00\t20\tOslo\tEng\t0\r\n", text);
    }

    [Fact]
    public void Format_EscapesCellsAndUsesTheDisplayText()
    {
        var g = new InputGrid();
        g.Source.SetCellValue(0, g.Name, "a\tb \"q\"");

        string text = KumikoTsvFormatter.Format(g.Source, new[] { 0 }, includeHeaders: false);

        Assert.StartsWith("\"a\tb \"\"q\"\"\"\t20\t", text);
    }

    [Fact]
    public void Format_WithNothingToWrite_IsEmpty()
    {
        var g = new InputGrid();

        Assert.Equal("", KumikoTsvFormatter.Format(g.Source, Array.Empty<int>(), includeHeaders: true));
        Assert.Equal("", KumikoTsvFormatter.Format(g.Source, new[] { 999 }, includeHeaders: false));
    }

    // ── Ctrl/Cmd+C on the selection ───────────────────────────────

    [Fact]
    public void CtrlC_CopiesTheSelectedRowsAsTsv()
    {
        var (g, clipboard) = Grid();
        SelectRows(g, 2, 1);

        var e = g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.True(e.Handled);
        Assert.Equal("N01\t21\tRome\tOps\t10\r\nN02\t22\tOslo\tOps\t20\r\n", clipboard.Text);
    }

    [Theory]
    [InlineData(InputModifiers.Control)] // Ctrl on Windows and Linux, and Cmd where a host maps it to Control
    [InlineData(InputModifiers.Meta)]    // Cmd reported only as Meta
    [InlineData(InputModifiers.Control | InputModifiers.Meta)]
    public void Copy_AcceptsControlAndCommand(InputModifiers modifiers)
    {
        var (g, clipboard) = Grid();
        SelectRows(g, 0);

        Assert.True(g.Key(KumikoKey.C, modifiers).Handled);
        Assert.NotNull(clipboard.Text);
    }

    [Fact]
    public void CtrlC_WithoutTheCommandModifier_DoesNotCopy()
    {
        var (g, clipboard) = Grid();
        SelectRows(g, 0);

        var e = g.Key(KumikoKey.C, InputModifiers.Shift);

        Assert.False(e.Handled);
        Assert.Null(clipboard.Text);
    }

    [Fact]
    public void CtrlC_WithNothingSelected_IsLeftToTheHost()
    {
        var (g, clipboard) = Grid();

        var e = g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.False(e.Handled);
        Assert.Null(clipboard.Text);
    }

    [Fact]
    public void CtrlC_WithNoClipboard_IsLeftToTheHost()
    {
        var g = new InputGrid();
        SelectRows(g, 0);

        Assert.False(g.Key(KumikoKey.C, InputModifiers.Control).Handled);
    }

    [Fact]
    public void CopyIncludesHeaders_AddsTheHeaderRow()
    {
        var (g, clipboard) = Grid(x => x.Input.CopyIncludesHeaders = true);
        SelectRows(g, 0);

        g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.StartsWith("Name\tAge\tCity\tDept\tScore\r\nN00\t", clipboard.Text);
    }

    [Fact]
    public void CtrlC_RaisesClipboardCopyRequested_AndAHandlerKeepsTheClipboardOut()
    {
        var (g, clipboard) = Grid();
        SelectRows(g, 0);
        string? raised = null;
        g.Input.ClipboardCopyRequested += (_, e) => { raised = e.Text; e.Handled = true; };

        g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.StartsWith("N00\t20\t", raised);
        Assert.Equal(0, clipboard.Sets);
    }

    [Fact]
    public void CtrlA_ThenCtrlC_CopiesEveryRow()
    {
        var (g, clipboard) = Grid();

        g.Key(KumikoKey.A, InputModifiers.Control);
        g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.Equal(30, clipboard.Text!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    // ── Editor copy, cut and paste ────────────────────────────────

    private static (InputGrid Grid, FakeClipboard Clipboard) Editing(string text = "hello")
    {
        var (g, clipboard) = Grid();
        g.Select(0, 0);
        g.Type('x');
        Assert.True(g.Edit.IsEditing);
        g.Key(KumikoKey.A, InputModifiers.Control); // select what the editor holds
        g.Edit.PasteText(text);                     // ... and replace it
        return (g, clipboard);
    }

    private static string? EditorText(InputGrid g) => g.Edit.ActiveTextInput?.Text;

    [Fact]
    public void Editor_CtrlC_CopiesTheSelectionAndKeepsTheText()
    {
        var (g, clipboard) = Editing("hello");

        g.Key(KumikoKey.A, InputModifiers.Control);
        var e = g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.True(e.Handled);
        Assert.Equal("hello", clipboard.Text);
        Assert.Equal("hello", EditorText(g));
    }

    [Fact]
    public void Editor_CtrlX_CutsOnlyAfterTheClipboardTookTheText()
    {
        var (g, clipboard) = Editing("hello");
        g.Key(KumikoKey.A, InputModifiers.Control);

        clipboard.Accepts = false;
        g.Key(KumikoKey.X, InputModifiers.Control);
        Assert.Equal("hello", EditorText(g));

        clipboard.Accepts = true;
        g.Key(KumikoKey.X, InputModifiers.Control);
        Assert.Equal("hello", clipboard.Text);
        Assert.Equal("", EditorText(g));
    }

    [Fact]
    public void Editor_CtrlV_PastesTheClipboardText()
    {
        var (g, clipboard) = Editing("hello");
        clipboard.Text = "world";
        g.Key(KumikoKey.A, InputModifiers.Control);

        g.Key(KumikoKey.V, InputModifiers.Control);

        Assert.Equal("world", EditorText(g));
    }

    [Fact]
    public void Editor_CopyCutPaste_WorkWithCommandAsMeta()
    {
        var (g, clipboard) = Editing("hello");
        g.Key(KumikoKey.A, InputModifiers.Meta);
        g.Key(KumikoKey.X, InputModifiers.Meta);
        Assert.Equal("hello", clipboard.Text);
        Assert.Equal("", EditorText(g));

        g.Key(KumikoKey.V, InputModifiers.Meta);
        Assert.Equal("hello", EditorText(g));
    }

    [Fact]
    public void Editor_CtrlV_WithAnAsynchronousClipboard_PastesWhenItCompletes()
    {
        var (g, clipboard) = Editing("hello");
        var pending = new TaskCompletionSource<string?>();
        clipboard.Async = true;
        clipboard.Pending = pending.Task;
        g.Key(KumikoKey.A, InputModifiers.Control);

        g.Key(KumikoKey.V, InputModifiers.Control);
        Assert.Equal("hello", EditorText(g));

        pending.SetResult("later");
        Assert.Equal("later", EditorText(g));
    }

    [Fact]
    public void Editor_AsynchronousPaste_IsDroppedWhenTheEditorHasClosed()
    {
        var (g, clipboard) = Editing("hello");
        var pending = new TaskCompletionSource<string?>();
        clipboard.Async = true;
        clipboard.Pending = pending.Task;
        g.Key(KumikoKey.V, InputModifiers.Control);

        g.Key(KumikoKey.Escape);
        g.Select(1, 0);
        g.Type('y'); // another editor, another text surface
        pending.SetResult("late");

        Assert.Equal("y", EditorText(g));
    }

    [Fact]
    public void Editor_WithNoClipboard_DoesNothingAndCutKeepsTheText()
    {
        var g = new InputGrid();
        g.Select(0, 0);
        g.Type('a');
        g.Key(KumikoKey.A, InputModifiers.Control);

        g.Key(KumikoKey.X, InputModifiers.Control);
        g.Key(KumikoKey.V, InputModifiers.Control);

        Assert.Equal("a", EditorText(g));
    }

    [Fact]
    public void Editor_Keys_DoNotCopyTheGridSelection()
    {
        var (g, clipboard) = Editing("hello");
        SelectRows(g, 3);

        g.Key(KumikoKey.A, InputModifiers.Control);
        g.Key(KumikoKey.C, InputModifiers.Control);

        Assert.Equal("hello", clipboard.Text); // the editor's text, not the TSV of row 3
    }
}
