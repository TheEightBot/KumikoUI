using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Direction = KumikoUI.Maui.AppleTextInputBridge.Direction;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The UIKit-independent half of the iOS and Mac Catalyst text-input adapter (#40): UIKit's <c>UITextInput</c> calls,
/// in UTF-16 indices, applied to a real grid's input controller and editor. Marked text, typing with no editor open,
/// grapheme-cluster positions, keys left to the input method, geometry and the stale-state notifications.
/// </summary>
public class AppleTextInputBridgeTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
    }

    /// <summary>A grid wired to an input controller, with the bridge on top as the MAUI control wires it.</summary>
    private sealed class Harness
    {
        public ObservableCollection<Row> Rows { get; } = new()
        {
            new Row { Name = "Alice", Age = 30, City = "Oslo" },
            new Row { Name = "Bob", Age = 25, City = "Rome" },
        };

        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 200 };
        public KumikoSelectionModel Selection { get; }
        public EditSession Edit { get; } = new() { TextSelectionMode = EditTextSelectionMode.CursorAtEnd };
        public KumikoInputController Input { get; } = new();
        public AppleTextInputBridge Bridge { get; }
        public List<KumikoKeyEventArgs> Keys { get; } = new();
        public List<bool> Invalidations { get; } = new();

        public Harness()
        {
            Selection = new KumikoSelectionModel(Source);
            Source.SetColumns(new[]
            {
                new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 },
                new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.Numeric },
                new KumikoColumn { Header = "City", PropertyName = "City", Width = 150 },
            });
            Source.SetItems(Rows);
            Edit.Style = Style;
            Input.EditSession = Edit;
            Bridge = new AppleTextInputBridge(() => Input.ActiveTextInput, () => Edit.ActiveEditor?.Bounds, SendKey);
            Bridge.Invalidated += sessionChanged => Invalidations.Add(sessionChanged);
        }

        // As KumikoDataGrid.SendKeyEvent: the key goes to the controller, then the bridge follows the session.
        public void SendKey(KumikoKeyEventArgs e)
        {
            Keys.Add(e);
            Input.HandleKey(e, Scroll, Selection, Style, Source);
            Bridge.Sync();
        }

        public void Select(int row, int column) => Selection.CurrentCell = new CellPosition(row, column);

        /// <summary>Opens the editor of a cell with F2, the caret at the end of its text.</summary>
        public DrawnTextBox EditCell(int row, int column)
        {
            Select(row, column);
            SendKey(new KumikoKeyEventArgs { Key = KumikoKey.F2 });
            return Box;
        }

        public DrawnTextBox Box => Assert.IsType<DrawnTextBox>(Edit.ActiveEditor);
    }

    // ── Marked text ──────────────────────────────────────────────

    [Fact]
    public void MarkedText_JapaneseSequence_ComposesThenCommitsIntoTheCell()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        h.Bridge.SetMarkedText("k", 1, 0);
        Assert.Equal("Alicek", h.Bridge.Text);
        Assert.Equal(new KumikoTextRange(5, 1), h.Bridge.MarkedRange);

        h.Bridge.SetMarkedText("か", 1, 0);
        h.Bridge.SetMarkedText("かな", 2, 0);
        Assert.Equal("Aliceかな", h.Bridge.Text);
        Assert.Equal(new KumikoTextRange(5, 2), h.Bridge.MarkedRange);
        Assert.Equal(new KumikoTextRange(7, 0), h.Bridge.SelectedRange);

        h.Bridge.SetMarkedText("仮名", 2, 0); // the candidate chosen from the bar
        h.Bridge.UnmarkText();

        Assert.Null(h.Bridge.MarkedRange);
        Assert.False(h.Bridge.HasMarkedText);
        Assert.Equal("Alice仮名", h.Bridge.Text);

        h.SendKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter });
        Assert.False(h.Edit.IsEditing);
        Assert.Equal("Alice仮名", h.Rows[0].Name);
    }

    [Fact]
    public void MarkedText_SelectedRangeWithinIt_PutsTheCaretAtTheEndOfThatRange()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        h.Bridge.SetMarkedText("漢字変換", 0, 2); // the first clause is being converted

        Assert.Equal(new KumikoTextRange(7, 0), h.Bridge.SelectedRange);
    }

    [Fact]
    public void MarkedText_EmptyText_CancelsTheComposition()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetMarkedText("かな", 2, 0);

        h.Bridge.SetMarkedText("", 0, 0);

        Assert.Equal("Alice", h.Bridge.Text);
        Assert.Null(h.Bridge.MarkedRange);
    }

    [Fact]
    public void InsertText_WhileComposing_ReplacesTheMarkedText()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetMarkedText("かんじ", 3, 0);

        h.Bridge.InsertText("漢字");

        Assert.Equal("Alice漢字", h.Bridge.Text);
        Assert.Null(h.Bridge.MarkedRange);
    }

    [Fact]
    public void DeleteBackward_WhileComposing_RemovesFromTheMarkedText()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetMarkedText("かな", 2, 0);

        h.Bridge.DeleteBackward();

        Assert.Equal("Aliceか", h.Bridge.Text);
        Assert.Equal(new KumikoTextRange(5, 1), h.Bridge.MarkedRange);
    }

    [Fact]
    public void MarkedText_WithNoEditorOpen_OpensTheCellAndComposesItsWholeText()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.SetMarkedText("k", 1, 0);

        Assert.True(h.Edit.IsEditing);
        Assert.Equal("k", h.Bridge.Text);
        Assert.Equal(new KumikoTextRange(0, 1), h.Bridge.MarkedRange);

        h.Bridge.SetMarkedText("か", 1, 0);
        h.Bridge.UnmarkText();
        Assert.Equal("か", h.Bridge.Text);
        Assert.Null(h.Bridge.MarkedRange);
    }

    [Fact]
    public void MarkedText_WithNoEditorOpen_CanceledLeavesTheCellEmptyAsTypingWould()
    {
        var h = new Harness();
        h.Select(0, 0);
        h.Bridge.SetMarkedText("k", 1, 0);

        h.Bridge.SetMarkedText(null, 0, 0);

        Assert.True(h.Edit.IsEditing);
        Assert.Equal("", h.Bridge.Text);
    }

    [Fact]
    public void MarkedText_WithNoEditableCell_IsDropped()
    {
        var h = new Harness();
        h.Edit.EditTriggers = EditTrigger.DoubleTap; // typing does not edit
        h.Select(0, 0);

        h.Bridge.SetMarkedText("k", 1, 0);

        Assert.False(h.Edit.IsEditing);
        Assert.Null(h.Bridge.MarkedRange);
        Assert.Equal("", h.Bridge.Text);
    }

    // ── Committed text ───────────────────────────────────────────

    [Fact]
    public void InsertText_EmojiWithNoEditorOpen_OpensTheCellWithTheWholeSurrogatePair()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.InsertText("😀");

        Assert.True(h.Edit.IsEditing);
        Assert.Equal("😀", h.Bridge.Text);
        Assert.Equal(new KumikoTextRange(2, 0), h.Bridge.SelectedRange);
    }

    [Fact]
    public void InsertText_WordWithNoEditorOpen_OpensTheCellWithTheWholeWord()
    {
        var h = new Harness();
        h.Select(1, 2);

        h.Bridge.InsertText("Paris"); // a suggestion-bar word or dictation

        Assert.Equal("Paris", h.Bridge.Text);
    }

    [Fact]
    public void InsertText_SingleCharacterWithNoEditorOpen_IsTypedIntoTheGridAsAKey()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.InsertText("Q");

        var key = Assert.Single(h.Keys);
        Assert.Equal(KumikoKey.Character, key.Key);
        Assert.Equal('Q', key.Character);
        Assert.Equal("Q", h.Bridge.Text);
    }

    [Fact]
    public void InsertText_EmojiWithSkinTone_IsOneCharacterForDeleteBackward()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        h.Bridge.InsertText("👍🏽");
        Assert.Equal("Alice👍🏽", h.Bridge.Text);

        h.Bridge.DeleteBackward();
        Assert.Equal("Alice", h.Bridge.Text);
    }

    [Fact]
    public void InsertText_Return_IsEnterAndCommitsTheEdit()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.InsertText("!");

        h.Bridge.InsertText("\n");

        Assert.False(h.Edit.IsEditing);
        Assert.Equal("Alice!", h.Rows[0].Name);
        Assert.Equal(KumikoKey.Enter, h.Keys[^1].Key);
    }

    [Fact]
    public void InsertText_Tab_IsTheTabKey()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.InsertText("\t");

        Assert.Equal(KumikoKey.Tab, Assert.Single(h.Keys).Key);
        Assert.Equal(1, h.Selection.CurrentCell.Column);
    }

    [Fact]
    public void DeleteBackward_WithNoEditorOpen_IsBackspaceForTheGrid()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.DeleteBackward();

        Assert.Equal(KumikoKey.Backspace, Assert.Single(h.Keys).Key);
    }

    [Fact]
    public void ReplaceRange_ReplacesAndDeletes()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        h.Bridge.ReplaceRange(0, 1, "M");
        Assert.Equal("Mlice", h.Bridge.Text);

        h.Bridge.ReplaceRange(5, 1, "");
        Assert.Equal("M", h.Bridge.Text);
    }

    [Fact]
    public void ReplaceRange_WhileComposing_CommitsTheCompositionFirst()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetMarkedText("か", 1, 0);

        h.Bridge.ReplaceRange(0, 5, "Bob");

        Assert.Equal("Bobか", h.Bridge.Text);
        Assert.Null(h.Bridge.MarkedRange);
    }

    // ── Selection and positions (UTF-16 indices, grapheme clusters) ──

    [Fact]
    public void SetSelectedRange_InsideACluster_SnapsToItsStart()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.InsertText("👍🏽");

        h.Bridge.SetSelectedRange(7, 7); // between the thumb and its skin tone

        Assert.Equal(new KumikoTextRange(5, 0), h.Bridge.SelectedRange);
    }

    [Fact]
    public void SetSelectedRange_SelectsInEitherOrder()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        h.Bridge.SetSelectedRange(4, 1);

        Assert.Equal(new KumikoTextRange(1, 3), h.Bridge.SelectedRange);
    }

    [Fact]
    public void PositionInDirection_LeftAndRight_StepByGraphemeCluster()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetSelectedRange(0, 5);
        h.Bridge.InsertText("a👍🏽éb"); // a, thumbs up with skin tone (4 units), e + combining acute, b

        Assert.Equal(5, h.Bridge.PositionInDirection(1, Direction.Right, 1));
        Assert.Equal(7, h.Bridge.PositionInDirection(1, Direction.Right, 2));
        Assert.Equal(1, h.Bridge.PositionInDirection(5, Direction.Left, 1));
        Assert.Equal(1, h.Bridge.PositionInDirection(7, Direction.Right, -2));
        Assert.Null(h.Bridge.PositionInDirection(0, Direction.Left, 1));
        Assert.Null(h.Bridge.PositionInDirection(8, Direction.Right, 2));
        Assert.Equal(0, h.Bridge.PositionInDirection(5, Direction.Up, 1));
        Assert.Equal(8, h.Bridge.PositionInDirection(5, Direction.Down, 1));
    }

    [Fact]
    public void CharacterRangeByExtending_IsTheWholeCluster()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetSelectedRange(0, 5);
        h.Bridge.InsertText("a👍🏽b");

        Assert.Equal(new KumikoTextRange(1, 4), h.Bridge.CharacterRangeByExtending(1, Direction.Right));
        Assert.Equal(new KumikoTextRange(1, 4), h.Bridge.CharacterRangeByExtending(5, Direction.Left));
        Assert.Equal(new KumikoTextRange(0, 5), h.Bridge.CharacterRangeByExtending(5, Direction.Up));
    }

    [Fact]
    public void PositionFromPosition_OutsideTheText_IsNull()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        Assert.Equal(3, h.Bridge.PositionFromPosition(1, 2));
        Assert.Null(h.Bridge.PositionFromPosition(4, 2));
        Assert.Null(h.Bridge.PositionFromPosition(1, -2));
    }

    [Fact]
    public void TextInRange_IsClampedAndOrdered()
    {
        var h = new Harness();
        h.EditCell(0, 0);

        Assert.Equal("lic", h.Bridge.TextInRange(4, 1));
        Assert.Equal("ice", h.Bridge.TextInRange(2, 99));
        Assert.Equal("", h.Bridge.TextInRange(-5, 0));
    }

    [Fact]
    public void WithNoEditorOpen_TheDocumentIsEmptyButHasText()
    {
        var h = new Harness();

        Assert.Equal("", h.Bridge.Text);
        Assert.Equal(default, h.Bridge.SelectedRange);
        Assert.True(h.Bridge.HasText); // Backspace still reaches the grid
        Assert.Null(h.Bridge.PositionFromPosition(0, 1));
    }

    [Fact]
    public void HasText_EmptyEditor_IsFalse()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.SetSelectedRange(0, 5);
        h.Bridge.DeleteBackward();

        Assert.False(h.Bridge.HasText);
    }

    [Fact]
    public void IsNumeric_FollowsTheEditor()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        Assert.False(h.Bridge.IsNumeric);

        h.SendKey(new KumikoKeyEventArgs { Key = KumikoKey.Escape });
        h.EditCell(0, 1);
        Assert.True(h.Bridge.IsNumeric);
    }

    // ── Keys left to the input method ────────────────────────────

    [Fact]
    public void ShouldLeaveKeyToTextSystem_EveryKeyWhileComposing_AndSpaceWhileEditing()
    {
        var h = new Harness();
        h.Select(0, 0);
        Assert.False(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Space)); // toggles the row selection
        Assert.False(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Left));

        h.EditCell(0, 0);
        Assert.True(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Space)); // a space is text
        Assert.False(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Left));
        Assert.False(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Enter));

        h.Bridge.SetMarkedText("かな", 2, 0);
        Assert.True(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Left));
        Assert.True(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Enter));
        Assert.True(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Escape));
        Assert.True(h.Bridge.ShouldLeaveKeyToTextSystem(KumikoKey.Tab));
    }

    // ── Geometry ─────────────────────────────────────────────────

    [Fact]
    public void CaretAndFirstRect_AreInTheEditedCell()
    {
        var h = new Harness();
        h.EditCell(1, 2);
        var cell = h.Box.Bounds;

        var caret = h.Bridge.CaretRect(0);
        var first = h.Bridge.FirstRect(0, 2);

        Assert.Equal(cell.X + h.Box.Padding, caret.X);
        Assert.Equal(0, caret.Width);
        Assert.InRange(caret.Y, cell.Y, cell.Bottom);
        Assert.InRange(caret.Bottom, cell.Y, cell.Bottom);
        Assert.InRange(first.X, cell.X, cell.Right);
        Assert.InRange(first.Right, cell.X, cell.Right);
        Assert.True(cell.Y > h.Style.HeaderHeight); // the second row, below the header
    }

    [Fact]
    public void Geometry_WithNoEditorOpen_IsEmpty()
    {
        var h = new Harness();

        Assert.Equal(default, h.Bridge.CaretRect(0));
        Assert.Equal(0, h.Bridge.ClosestPosition(50));
    }

    // ── Stale-state notifications ────────────────────────────────

    [Fact]
    public void Invalidated_WhenAnEditOpensOrCloses_ReportsANewSession()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        Assert.Equal(new[] { true }, h.Invalidations);

        h.SendKey(new KumikoKeyEventArgs { Key = KumikoKey.Escape });
        Assert.Equal(new[] { true, true }, h.Invalidations);
    }

    [Fact]
    public void Invalidated_WhenCoreMovesTheCaret_ButNotForTheBridgesOwnCalls()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Invalidations.Clear();

        h.Bridge.InsertText("x");
        h.Bridge.SetMarkedText("か", 1, 0);
        h.Bridge.UnmarkText();
        h.Bridge.SetSelectedRange(0, 0);
        h.Bridge.DeleteBackward();
        Assert.Empty(h.Invalidations);

        h.SendKey(new KumikoKeyEventArgs { Key = KumikoKey.Right }); // a hardware arrow handled by the editor
        Assert.Equal(new[] { false }, h.Invalidations);
    }

    [Fact]
    public void Invalidated_TypingThatOpensAnEditor_IsNotReportedBackToUIKit()
    {
        var h = new Harness();
        h.Select(0, 0);

        h.Bridge.InsertText("Q");

        Assert.Empty(h.Invalidations);
        Assert.True(h.Edit.IsEditing);
    }

    [Fact]
    public void Detach_StopsFollowingTheSession()
    {
        var h = new Harness();
        h.EditCell(0, 0);
        h.Bridge.Detach();
        h.Invalidations.Clear();

        h.Input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.Left }, h.Scroll, h.Selection, h.Style, h.Source);

        Assert.Empty(h.Invalidations);
    }
}
