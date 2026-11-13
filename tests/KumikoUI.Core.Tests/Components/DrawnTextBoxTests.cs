using System.Collections.ObjectModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn text box and the text-input contract (#39): input-method composition, grapheme-cluster
/// editing, undo/redo, selection gestures, input rules for pasted and composed text, and the edit
/// session's clipboard requests.
/// </summary>
public class DrawnTextBoxTests
{
    // The recording context measures every UTF-16 code unit as 8 px; padding is 0, so index i is at x = 8i.
    private const float W = RecordingDrawingContext.CharWidth;

    private static DrawnTextBox Box(string text = "", float width = 400,
        EditTextSelectionMode mode = EditTextSelectionMode.CursorAtEnd, Action<DrawnTextBox>? configure = null)
    {
        var box = new DrawnTextBox { Bounds = new KumikoRect(0, 0, width, 30), Padding = 0, InitialSelectionMode = mode };
        configure?.Invoke(box);
        box.Text = text;
        box.SetMeasureContext(new RecordingDrawingContext());
        var focus = new DrawnFocusManager();
        focus.Register(box);
        focus.SetFocus(box);
        return box;
    }

    private static bool Key(DrawnTextBox box, KumikoKey key, InputModifiers modifiers = InputModifiers.None,
        char? character = null, string? text = null) =>
        box.OnKeyDown(new KumikoKeyEventArgs { Key = key, Modifiers = modifiers, Character = character, Text = text });

    private static void Type(DrawnTextBox box, string text)
    {
        foreach (char c in text)
            Key(box, KumikoKey.Character, character: c);
    }

    private static void Press(DrawnTextBox box, float x, long time, InputModifiers modifiers = InputModifiers.None) =>
        box.OnPointerDown(new KumikoPointerEventArgs { X = x, Y = 10, Action = InputAction.Pressed, TimestampMs = time, Modifiers = modifiers });

    private static ITextInputSession Session(DrawnTextBox box) => box;

    // ── Composition ───────────────────────────────────────────────

    [Fact]
    public void Composition_StartUpdateCommit_IsOneUndoStep()
    {
        var box = Box("ab");
        var ime = Session(box);

        ime.SetComposition("k", 1);
        Assert.Equal("abk", box.Text);
        Assert.Equal(new KumikoTextRange(2, 1), box.Composition);

        ime.SetComposition("か", 1);
        ime.SetComposition("かな", 2);
        Assert.Equal("abかな", box.Text);
        Assert.Equal(new KumikoTextRange(2, 2), box.Composition);
        Assert.Equal(4, box.CursorPosition);

        ime.CommitComposition();
        Assert.Equal("abかな", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal(4, box.CursorPosition);

        box.Undo();
        Assert.Equal("ab", box.Text);
        Assert.False(box.CanUndo);
    }

    [Fact]
    public void Composition_ReplacesTheSelection_AndCancelRestoresIt()
    {
        var box = Box("hello", mode: EditTextSelectionMode.SelectAll);
        var ime = Session(box);

        ime.SetComposition("x", 1);
        Assert.Equal("x", box.Text);

        ime.CancelComposition();
        Assert.Equal("hello", box.Text);
        Assert.Equal(new KumikoTextRange(0, 5), box.Selection);
        Assert.Null(box.Composition);
        Assert.False(box.CanUndo);
    }

    [Fact]
    public void Composition_EmptyTextCancels_AndInsertTextCommitsTheGivenText()
    {
        var box = Box("a");
        var ime = Session(box);

        ime.SetComposition("ni", 2);
        ime.SetComposition("", 0);
        Assert.Equal("a", box.Text);
        Assert.Null(box.Composition);

        ime.SetComposition("ni", 2);
        ime.InsertText("に");
        Assert.Equal("aに", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal(2, box.CursorPosition);
    }

    [Fact]
    public void Composition_TheInputRulesApplyWhenItCommits()
    {
        var box = Box("1", configure: b => b.NumericOnly = true);
        var ime = Session(box);

        ime.SetComposition("2a", 2);
        Assert.Equal("12a", box.Text); // marked text is shown as typed

        ime.CommitComposition();
        Assert.Equal("12", box.Text); // the committed text is filtered
    }

    [Fact]
    public void Composition_DeleteBackwardShrinksIt_AndEmptyingItCancels()
    {
        var box = Box("a");
        var ime = Session(box);
        ime.SetComposition("xy", 2);

        ime.DeleteBackward();
        Assert.Equal("ax", box.Text);
        Assert.Equal(new KumikoTextRange(1, 1), box.Composition);

        ime.DeleteBackward();
        Assert.Equal("a", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void Composition_EnterCommitsAndEscapeCancels_WithoutEndingTheEdit()
    {
        var box = Box("a");
        int committed = 0, canceled = 0;
        box.Committed += () => committed++;
        box.Cancelled += () => canceled++;

        Session(box).SetComposition("b", 1);
        Key(box, KumikoKey.Enter);
        Assert.Equal("ab", box.Text);
        Assert.Null(box.Composition);

        Session(box).SetComposition("c", 1);
        Key(box, KumikoKey.Escape);
        Assert.Equal("ab", box.Text);

        Assert.Equal(0, committed);
        Assert.Equal(0, canceled);

        Key(box, KumikoKey.Enter);
        Assert.Equal(1, committed);
    }

    [Fact]
    public void StateChanged_IsRaisedForCoreChanges_NotForTheAdaptersOwnCalls()
    {
        var box = Box("abc");
        var ime = Session(box);
        int raised = 0;
        ime.TextInputStateChanged += (_, _) => raised++;

        ime.InsertText("d");
        ime.SetComposition("e", 1);
        ime.CommitComposition();
        ime.SetSelection(0, 1);
        ime.DeleteBackward();
        Assert.Equal(0, raised);

        Key(box, KumikoKey.End);
        Assert.Equal(1, raised);
        box.Undo();
        Assert.Equal(2, raised);
        box.Text = "new";
        Assert.Equal(3, raised);
    }

    // ── Grapheme clusters ─────────────────────────────────────────

    [Theory]
    [InlineData("a\U0001F44D\U0001F3FD")]              // thumbs up + skin tone (two surrogate pairs)
    [InlineData("a\U0001F468‍\U0001F469‍\U0001F467")] // family: ZWJ sequence
    [InlineData("aé")]                            // e + combining acute accent
    [InlineData("a\U0001F1EF\U0001F1F5")]              // flag: regional indicator pair
    public void DeleteBackward_RemovesTheWholeCluster(string text)
    {
        var box = Box(text);

        Key(box, KumikoKey.Backspace);

        Assert.Equal("a", box.Text);
        Assert.Equal(1, box.CursorPosition);
    }

    [Fact]
    public void ArrowsAndDeleteForward_StepOverClusters()
    {
        var box = Box("éx\U0001F600");
        Assert.Equal(5, box.CursorPosition);

        Key(box, KumikoKey.Left);
        Assert.Equal(3, box.CursorPosition); // before the emoji, not inside it
        Key(box, KumikoKey.Left);
        Key(box, KumikoKey.Left);
        Assert.Equal(0, box.CursorPosition); // the accent is skipped with its letter

        Key(box, KumikoKey.Delete);
        Assert.Equal("x\U0001F600", box.Text);
    }

    [Fact]
    public void AnIndexInsideACluster_MovesToItsStart()
    {
        var box = Box("\U0001F44D\U0001F3FDb");

        Session(box).SetSelection(2, 2);

        Assert.Equal(0, box.CursorPosition);
    }

    [Fact]
    public void SurrogatePairs_TypeWhole_FromTextOrFromTwoCharacters()
    {
        var box = Box();

        Key(box, KumikoKey.Character, text: "\U0001F600");
        Assert.Equal("\U0001F600", box.Text);
        Assert.Equal(2, box.CursorPosition);

        // Hosts that still send one UTF-16 unit at a time
        Type(box, "\U0001F601");
        Assert.Equal("\U0001F600\U0001F601", box.Text);
        Assert.Equal(4, box.CursorPosition);
    }

    [Fact]
    public void MaxLength_CutsInsertedTextAtAClusterBoundary()
    {
        var box = Box(configure: b => b.MaxLength = 3);

        box.InsertText("ab\U0001F44D");

        Assert.Equal("ab", box.Text);
    }

    // ── Geometry for text-input adapters ──────────────────────────

    [Fact]
    public void GetTextRangeBounds_RangeAndCaret_FollowTheMeasuredText()
    {
        var box = Box("hello");
        box.Bounds = new KumikoRect(100, 50, 400, 30);

        var range = box.GetTextRangeBounds(new KumikoTextRange(1, 3));
        var caret = box.GetTextRangeBounds(new KumikoTextRange(5, 0));

        // The recording context's text is 14 px tall (ascent -11, descent 3), centered in the 30 px box.
        Assert.Equal(new KumikoRect(100 + W, 58, 3 * W, 14), range);
        Assert.Equal(new KumikoRect(100 + 5 * W, 58, 0, 14), caret);
    }

    [Fact]
    public void GetTextRangeBounds_OutOfRangeAndPadding_AreClampedToTheTextArea()
    {
        var box = Box("abc", configure: b => b.Padding = 4);

        var range = box.GetTextRangeBounds(new KumikoTextRange(-3, 99));

        Assert.Equal(4, range.X);
        Assert.Equal(3 * W, range.Width);
    }

    [Fact]
    public void GetTextRangeBounds_ScrolledText_StaysInsideTheBox()
    {
        var box = Box(new string('x', 100), width: 80); // 800 px of text in an 80 px box, caret at the end
        box.OnDraw(new RecordingDrawingContext()); // scrolls the caret into view

        var start = box.GetTextRangeBounds(new KumikoTextRange(0, 0));
        var caret = box.GetTextRangeBounds(new KumikoTextRange(100, 0));

        Assert.Equal(0, start.X); // scrolled out to the left: pinned to the edge
        Assert.InRange(caret.X, 70, 80);
    }

    [Fact]
    public void GetTextRangeBounds_BeforeMeasuring_IsACaretAtTheStartOfTheTextArea()
    {
        var box = new DrawnTextBox { Bounds = new KumikoRect(10, 20, 100, 30), Padding = 4, Text = "abc" };

        Assert.Equal(new KumikoRect(14, 20, 0, 30), box.GetTextRangeBounds(new KumikoTextRange(1, 2)));
    }

    [Fact]
    public void GetTextIndexAt_IsTheNearestGraphemeBoundary()
    {
        // a at 0..8, the thumb with its skin tone (4 code units) at 8..40, b at 40..48
        var box = Box("a\U0001F44D\U0001F3FDb");

        Assert.Equal(0, box.GetTextIndexAt(-5));
        Assert.Equal(1, box.GetTextIndexAt(10));
        Assert.Equal(5, box.GetTextIndexAt(30));
        Assert.Equal(6, box.GetTextIndexAt(400));
    }

    // ── Undo / redo ───────────────────────────────────────────────

    [Fact]
    public void Typing_UndoesByWord_AndRedoes()
    {
        var box = Box();
        Type(box, "hello world");

        Key(box, KumikoKey.Z, InputModifiers.Control);
        Assert.Equal("hello ", box.Text);
        Key(box, KumikoKey.Z, InputModifiers.Meta); // Command on Apple keyboards
        Assert.Equal("", box.Text);

        Key(box, KumikoKey.Z, InputModifiers.Control | InputModifiers.Shift);
        Assert.Equal("hello ", box.Text);
        Key(box, KumikoKey.Y, InputModifiers.Control);
        Assert.Equal("hello world", box.Text);
        Assert.Equal(11, box.CursorPosition);
    }

    [Fact]
    public void AnEditAfterUndo_ClearsRedo_AndDeletesCoalesce()
    {
        var box = Box("abc");
        Key(box, KumikoKey.Backspace);
        Key(box, KumikoKey.Backspace);
        Key(box, KumikoKey.Backspace);
        Assert.Equal("", box.Text);

        box.Undo();
        Assert.Equal("abc", box.Text); // the run of deletes is one step

        Type(box, "d");
        Assert.False(box.CanRedo);
    }

    [Fact]
    public void Undo_RestoresTheSelection_AndCaretMovesSplitSteps()
    {
        var box = Box("one two", mode: EditTextSelectionMode.SelectAll);
        Type(box, "x");
        Assert.Equal("x", box.Text);

        box.Undo();
        Assert.Equal("one two", box.Text);
        Assert.Equal(new KumikoTextRange(0, 7), box.Selection);

        Key(box, KumikoKey.End);
        Type(box, "a");
        Key(box, KumikoKey.Home);
        Type(box, "b");
        box.Undo();
        Assert.Equal("one twoa", box.Text);
    }

    [Fact]
    public void SettingText_ClearsTheHistory()
    {
        var box = Box();
        Type(box, "abc");

        box.Text = "reset";

        Assert.False(box.CanUndo);
        Assert.False(box.CanRedo);
    }

    // ── Selection gestures ────────────────────────────────────────

    [Fact]
    public void DoubleClickSelectsTheWord_TripleClickSelectsAll()
    {
        var box = Box("hello big world");
        float inBig = 7 * W + 2;

        Press(box, inBig, 1000);
        Assert.Equal(new KumikoTextRange(7, 0), box.Selection);
        Press(box, inBig, 1100);
        Assert.Equal(new KumikoTextRange(6, 3), box.Selection);
        Press(box, inBig, 1200);
        Assert.Equal(new KumikoTextRange(0, 15), box.Selection);

        Press(box, inBig, 5000); // too late: a new single click
        Assert.Equal(new KumikoTextRange(7, 0), box.Selection);
    }

    [Fact]
    public void HostReportedDoubleClick_SelectsTheWord()
    {
        var box = Box("hello big world");

        box.OnPointerDown(new KumikoPointerEventArgs { X = 12 * W, Y = 10, Action = InputAction.Pressed, ClickCount = 2 });

        Assert.Equal(new KumikoTextRange(10, 5), box.Selection);
    }

    [Fact]
    public void Drag_SelectsFromThePress_UntilRelease()
    {
        var box = Box("hello world");

        Press(box, 1, 1000);
        box.OnPointerMove(new KumikoPointerEventArgs { X = 5 * W + 2, Y = 10, Action = InputAction.Moved });
        Assert.Equal(new KumikoTextRange(0, 5), box.Selection);

        box.OnPointerUp(new KumikoPointerEventArgs { X = 5 * W + 2, Y = 10, Action = InputAction.Released });
        box.OnPointerMove(new KumikoPointerEventArgs { X = 9 * W, Y = 10, Action = InputAction.Moved });
        Assert.Equal(new KumikoTextRange(0, 5), box.Selection);

        Press(box, 9 * W + 1, 3000, InputModifiers.Shift);
        Assert.Equal(new KumikoTextRange(0, 9), box.Selection); // Shift+click extends from the anchor
    }

    [Fact]
    public void DragPastTheEdge_ScrollsTheText()
    {
        var box = Box(new string('x', 20), width: 5 * W, mode: EditTextSelectionMode.SelectAll);
        box.Text = new string('x', 20);
        Key(box, KumikoKey.Home);

        Press(box, 1, 1000);
        box.OnPointerMove(new KumikoPointerEventArgs { X = 12 * W, Y = 10, Action = InputAction.Moved });
        var ctx = new RecordingDrawingContext();
        box.OnDraw(ctx);

        Assert.Equal(12, box.CursorPosition);
        Assert.True(ctx.Texts.Single().Rect.X < 0, "the text did not scroll to show the caret");
    }

    [Fact]
    public void ArrowKeys_ExtendAndCollapseTheSelection_AndCommandMovesByWord()
    {
        var box = Box("one two three");

        Key(box, KumikoKey.Left, InputModifiers.Shift);
        Key(box, KumikoKey.Left, InputModifiers.Shift);
        Assert.Equal(new KumikoTextRange(11, 2), box.Selection);

        Key(box, KumikoKey.Left);
        Assert.Equal(new KumikoTextRange(11, 0), box.Selection); // collapses to the start

        Key(box, KumikoKey.Left, InputModifiers.Control);
        Assert.Equal(8, box.CursorPosition);
        Key(box, KumikoKey.Left, InputModifiers.Control);
        Assert.Equal(4, box.CursorPosition);

        Key(box, KumikoKey.Backspace, InputModifiers.Control);
        Assert.Equal("two three", box.Text);
    }

    // ── Input rules ───────────────────────────────────────────────

    [Fact]
    public void Paste_IsSingleLine_AndOneUndoStep()
    {
        var box = Box("x");
        box.PasteRequested += (_, e) => e.Text = "a\r\nb\tc\u0007";

        Key(box, KumikoKey.V, InputModifiers.Control);
        Assert.Equal("xa b c", box.Text);

        box.Undo();
        Assert.Equal("x", box.Text);
    }

    [Fact]
    public void Paste_IntoANumericBox_KeepsOnlyNumericCharacters()
    {
        var box = Box(configure: b => b.NumericOnly = true);
        box.PasteRequested += (_, e) => e.Text = "USD -1,234.50\n";

        Key(box, KumikoKey.V, InputModifiers.Control);

        Assert.Equal("-1,234.50", box.Text);
    }

    [Fact]
    public void RejectedInput_LeavesTheSelectionAlone()
    {
        var box = Box("123", mode: EditTextSelectionMode.SelectAll, configure: b => b.NumericOnly = true);
        box.PasteRequested += (_, e) => e.Text = "abc";

        Type(box, "x");
        Key(box, KumikoKey.V, InputModifiers.Control);
        Session(box).SetComposition("y", 1);
        Session(box).CommitComposition();

        Assert.Equal("123", box.Text);
        Assert.Equal(new KumikoTextRange(0, 3), box.Selection);
        Assert.False(box.CanUndo);

        box.InsertText(""); // an explicitly empty insert deletes the selection
        Assert.Equal("", box.Text);
    }

    [Fact]
    public void Validator_RejectsInput_AndFlagsTheError()
    {
        var box = Box(configure: b => b.InputValidator = t => t.Length <= 2);
        Type(box, "abc");

        Assert.Equal("ab", box.Text);
        Assert.Equal(ValidationState.Error, box.Validation);
    }

    [Fact]
    public void Cut_CopiesAndDeletes_AndIsUndoable()
    {
        var box = Box("hello", mode: EditTextSelectionMode.SelectAll);
        string? copied = null;
        box.CopyRequested += (_, e) => { copied = e.Text; e.Handled = true; };

        Key(box, KumikoKey.X, InputModifiers.Control);

        Assert.Equal("hello", copied);
        Assert.Equal("", box.Text);
        box.Undo();
        Assert.Equal("hello", box.Text);
    }

    [Fact]
    public void Cut_WithNoHostTakingTheText_KeepsIt()
    {
        var box = Box("hello", mode: EditTextSelectionMode.SelectAll);
        string? copied = null;
        box.CopyRequested += (_, e) => copied = e.Text; // never sets Handled

        Key(box, KumikoKey.X, InputModifiers.Control);

        Assert.Equal("hello", copied);
        Assert.Equal("hello", box.Text);
    }

    [Fact]
    public void PasteText_ReplacesTheSelectionAsOneUndoStep()
    {
        var box = Box("hello", mode: EditTextSelectionMode.SelectAll);

        box.PasteText("a\nb");

        Assert.Equal("a b", box.Text);
        box.Undo();
        Assert.Equal("hello", box.Text);
    }

    // ── Edit session ──────────────────────────────────────────────

    private sealed class EditFixture
    {
        public ObservableCollection<Person> People { get; } = new() { new Person { Name = "Alice", Age = 30 } };
        public KumikoSource Source { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public EditSession Edit { get; } = new();

        public EditFixture()
        {
            Source.SetColumns(new[] { Name });
            Source.SetItems(People);
            Assert.True(Edit.BeginEdit(0, 0, Name, Source, new KumikoRect(0, 30, 150, 30)));
        }
    }

    [Fact]
    public void EditSession_RaisesClipboardRequests_FromTheEditor()
    {
        var f = new EditFixture();
        string? copied = null;
        f.Edit.ClipboardCopyRequested += (object? _, ClipboardTextEventArgs e) => copied = e.Text;
        f.Edit.ClipboardPasteRequested += (object? _, ClipboardRequestEventArgs e) => e.Text = "Zoe";

        f.Edit.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.C, Modifiers = InputModifiers.Control });
        Assert.Equal("Alice", copied); // the editor opens with everything selected

        f.Edit.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.V, Modifiers = InputModifiers.Control });
        Assert.True(f.Edit.CommitEdit(f.Source));
        Assert.Equal("Zoe", f.People[0].Name);
    }

    [Fact]
    public void EditSession_ExposesTheTextInput_AndCommitIncludesTheComposition()
    {
        var f = new EditFixture();
        var input = f.Edit.ActiveTextInput;
        Assert.NotNull(input);

        input!.SetComposition("Zo", 2);
        Assert.True(f.Edit.CommitEdit(f.Source));

        Assert.Equal("Zo", f.People[0].Name);
        Assert.Null(f.Edit.ActiveTextInput);
    }

    [Fact]
    public void InputController_EnterDuringComposition_CommitsTheCompositionAndKeepsEditing()
    {
        var people = new ObservableCollection<Person> { new() { Name = "Alice" }, new() { Name = "Bob" } };
        var source = new KumikoSource();
        var name = new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 };
        source.SetColumns(new[] { name });
        source.SetItems(people);
        var selection = new KumikoSelectionModel(source);
        var edit = new EditSession();
        var input = new KumikoInputController { EditSession = edit };
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 200 };
        var style = new KumikoStyle();
        new KumikoRenderer().Render(new RecordingDrawingContext(), source, scroll, selection, style, editSession: edit);
        selection.CurrentCell = new CellPosition(0, 0);
        input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.F2 }, scroll, selection, style, source);
        Assert.True(edit.IsEditing);

        edit.ActiveTextInput!.SetComposition("Zo", 2);
        input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter }, scroll, selection, style, source);

        Assert.True(edit.IsEditing);
        Assert.Null(edit.ActiveTextInput!.Composition);
        Assert.Equal("Zo", edit.ActiveTextInput.Text);

        input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter }, scroll, selection, style, source);
        Assert.False(edit.IsEditing);
        Assert.Equal("Zo", people[0].Name);
    }
}
