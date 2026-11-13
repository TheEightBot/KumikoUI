using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The Windows text-input adapter's bookkeeping (#42): the text services' requests and updates
/// (<c>CoreTextEditContext</c>) applied to a real <see cref="DrawnTextBox"/>, and the notifications that keep the
/// text services in step with it.
/// </summary>
public class TextEditContextStateTests
{
    private static DrawnTextBox Box(string text = "", Action<DrawnTextBox>? configure = null)
    {
        var box = new DrawnTextBox { Bounds = new KumikoRect(0, 0, 400, 30), Padding = 0 };
        configure?.Invoke(box);
        box.Text = text;
        box.SetSelection(text.Length, text.Length);
        return box;
    }

    private static TextEditContextState Attached(DrawnTextBox box)
    {
        var state = new TextEditContextState();
        state.Attach(box);
        return state;
    }

    // ── Attach and requests ──────────────────────────────────────

    [Fact]
    public void Attach_TakesTheEditorStateAsKnown()
    {
        var box = Box("hello");
        box.SetSelection(1, 3);

        var state = Attached(box);

        Assert.Same(box, state.Session);
        Assert.Equal("hello", state.KnownText);
        Assert.Equal(new KumikoTextRange(1, 2), state.KnownSelection);
        Assert.False(state.IsComposing);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void Attach_Null_DetachesAndAnswersEmpty()
    {
        var state = Attached(Box("abc"));

        state.Attach(null);

        Assert.Null(state.Session);
        Assert.Equal(string.Empty, state.GetText(0, 10));
        Assert.Equal(default, state.GetSelection());
        Assert.False(state.ApplyTextUpdate(0, 0, "x", 1, 1));
        Assert.False(state.ApplySelectionUpdate(0, 0));
        Assert.Null(state.TakeChange());
    }

    [Theory]
    [InlineData(0, 5, "hello")]
    [InlineData(1, 3, "el")]
    [InlineData(3, 1, "el")]
    [InlineData(-4, 2, "he")]
    [InlineData(2, int.MaxValue, "llo")]
    [InlineData(9, 12, "")]
    public void GetText_ClampsTheRequestedRange(int start, int end, string expected)
    {
        var state = Attached(Box("hello"));

        Assert.Equal(expected, state.GetText(start, end));
    }

    [Fact]
    public void GetSelection_IsTheEditorSelection()
    {
        var box = Box("hello");
        var state = Attached(box);
        box.SetSelection(4, 2);

        Assert.Equal(new KumikoTextRange(2, 2), state.GetSelection());
    }

    // ── Committed text ───────────────────────────────────────────

    [Fact]
    public void TextUpdate_AtTheCaret_InsertsAndStaysInStep()
    {
        var box = Box("ab");
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(2, 2, "c", 3, 3));

        Assert.Equal("abc", box.Text);
        Assert.Equal(new KumikoTextRange(3, 0), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_OverARange_ReplacesIt()
    {
        var box = Box("hello world");
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(6, 11, "there", 11, 11));

        Assert.Equal("hello there", box.Text);
        Assert.Equal(new KumikoTextRange(11, 0), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_EmptyText_DeletesTheRange()
    {
        var box = Box("abcdef");
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(1, 4, string.Empty, 1, 1));

        Assert.Equal("aef", box.Text);
        Assert.Equal(new KumikoTextRange(1, 0), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_WithAnotherSelection_HonorsIt()
    {
        var box = Box("ab");
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(2, 2, "cd", 2, 4));

        Assert.Equal("abcd", box.Text);
        Assert.Equal(new KumikoTextRange(2, 2), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_SurrogatePair_ArrivesWhole()
    {
        var box = Box("a");
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(1, 1, "😀", 3, 3));

        Assert.Equal("a😀", box.Text);
        Assert.Equal(new KumikoTextRange(3, 0), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\t")]
    public void TextUpdate_LineBreakOrTab_FailsBecauseItIsAKey(string text)
    {
        var box = Box("ab");
        var state = Attached(box);

        Assert.False(state.ApplyTextUpdate(2, 2, text, 3, 3));

        Assert.Equal("ab", box.Text);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_RefusedByTheInputRules_FailsAndKeepsTheKnownText()
    {
        var box = Box("12", b => b.NumericOnly = true);
        var state = Attached(box);

        Assert.False(state.ApplyTextUpdate(2, 2, "x", 3, 3));

        Assert.Equal("12", box.Text);
        Assert.Equal("12", state.KnownText);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_ReadOnlyEditor_Fails()
    {
        var box = Box("ab", b => b.IsReadOnly = true);
        var state = Attached(box);

        Assert.False(state.ApplyTextUpdate(2, 2, "c", 3, 3));
        Assert.Equal("ab", box.Text);
    }

    [Fact]
    public void TextUpdate_AppliedDifferently_ReportsTheDifference()
    {
        // Numeric filtering keeps "5" of "5x"
        var box = Box("12", b => b.NumericOnly = true);
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(2, 2, "5x", 4, 4));

        Assert.Equal("125", box.Text);
        var change = state.TakeChange();
        Assert.NotNull(change);
        // The text services saw "125x"; the "x" at 3..4 is gone
        Assert.Equal(new TextEditChange(3, 4, 0, new KumikoTextRange(3, 0)), change);
        Assert.Equal("125", state.KnownText);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void TextUpdate_CutByMaxLength_ReportsTheDifference()
    {
        var box = Box("abc", b => b.MaxLength = 4);
        var state = Attached(box);

        Assert.True(state.ApplyTextUpdate(3, 3, "def", 6, 6));

        Assert.Equal("abcd", box.Text);
        Assert.Equal(new TextEditChange(4, 6, 0, new KumikoTextRange(4, 0)), state.TakeChange());
    }

    // ── Composition ──────────────────────────────────────────────

    [Fact]
    public void Composition_JapaneseSequence_MarksThenCommits()
    {
        var box = Box("ab");
        var state = Attached(box);

        state.BeginComposition();
        Assert.True(state.IsComposing);

        Assert.True(state.ApplyTextUpdate(2, 2, "k", 3, 3));
        Assert.Equal("abk", box.Text);
        Assert.Equal(new KumikoTextRange(2, 1), box.Composition);

        Assert.True(state.ApplyTextUpdate(2, 3, "か", 3, 3));
        Assert.Equal("abか", box.Text);
        Assert.Equal(new KumikoTextRange(2, 1), box.Composition);

        Assert.True(state.ApplyTextUpdate(2, 3, "かな", 4, 4));
        Assert.Equal("abかな", box.Text);
        Assert.Equal(new KumikoTextRange(2, 2), box.Composition);
        Assert.Equal(new KumikoTextRange(4, 0), box.Selection);

        state.CompleteComposition();

        Assert.False(state.IsComposing);
        Assert.Null(box.Composition);
        Assert.Equal("abかな", box.Text);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void Composition_UpdateInsideTheMarkedText_RewritesOnlyThatPart()
    {
        var box = Box("x");
        var state = Attached(box);
        state.BeginComposition();
        state.ApplyTextUpdate(1, 1, "abc", 4, 4);

        // Replace the "b" in the middle of the composition
        Assert.True(state.ApplyTextUpdate(2, 3, "BB", 4, 4));

        Assert.Equal("xaBBc", box.Text);
        Assert.Equal(new KumikoTextRange(1, 4), box.Composition);
        Assert.Equal(new KumikoTextRange(4, 0), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void Composition_OverASelection_ReplacesIt()
    {
        var box = Box("hello");
        var state = Attached(box);
        state.BeginComposition();

        Assert.True(state.ApplyTextUpdate(1, 4, "ア", 2, 2));

        Assert.Equal("hアo", box.Text);
        Assert.Equal(new KumikoTextRange(1, 1), box.Composition);
    }

    [Fact]
    public void Composition_NumericEditor_ShowsMarkedLettersButCommitsDigits()
    {
        var box = Box("1", b => b.NumericOnly = true);
        var state = Attached(box);
        state.BeginComposition();

        Assert.True(state.ApplyTextUpdate(1, 1, "2a", 3, 3));
        Assert.Equal("12a", box.Text);

        state.CompleteComposition();

        Assert.Equal("12", box.Text);
        // The text services still see "12a": the "a" is reported removed
        Assert.Equal(new TextEditChange(2, 3, 0, new KumikoTextRange(2, 0)), state.TakeChange());
    }

    [Fact]
    public void Composition_DeletedToNothing_CancelsIt()
    {
        var box = Box("ab");
        var state = Attached(box);
        state.BeginComposition();
        state.ApplyTextUpdate(2, 2, "k", 3, 3);

        Assert.True(state.ApplyTextUpdate(2, 3, string.Empty, 2, 2));

        Assert.Equal("ab", box.Text);
        Assert.Null(box.Composition);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void Composition_UpdateOutsideTheMarkedText_CommitsItAndStartsAnother()
    {
        var box = Box("ab");
        var state = Attached(box);
        state.BeginComposition();
        state.ApplyTextUpdate(2, 2, "か", 3, 3);

        Assert.True(state.ApplyTextUpdate(0, 0, "な", 1, 1));

        Assert.Equal("なabか", box.Text);
        Assert.Equal(new KumikoTextRange(0, 1), box.Composition);
    }

    [Fact]
    public void CompleteComposition_WithNoMarkedText_ChangesNothing()
    {
        var box = Box("ab");
        var state = Attached(box);
        state.BeginComposition();

        state.CompleteComposition();

        Assert.False(state.IsComposing);
        Assert.Equal("ab", box.Text);
        Assert.Null(state.TakeChange());
    }

    // ── Selection ────────────────────────────────────────────────

    [Fact]
    public void SelectionUpdate_SetsTheSelection()
    {
        var box = Box("hello");
        var state = Attached(box);

        Assert.True(state.ApplySelectionUpdate(1, 4));

        Assert.Equal(new KumikoTextRange(1, 3), box.Selection);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void SelectionUpdate_InsideASurrogatePair_SnapsAndReportsIt()
    {
        var box = Box("a😀b");
        var state = Attached(box);

        Assert.True(state.ApplySelectionUpdate(2, 2));

        Assert.Equal(new KumikoTextRange(1, 0), box.Selection);
        Assert.Equal(new TextEditChange(0, 0, 0, new KumikoTextRange(1, 0)), state.TakeChange());
    }

    // ── Changes made by Core ─────────────────────────────────────

    [Fact]
    public void CoreChange_ReportsTheReplacedRange()
    {
        var box = Box("hello");
        var state = Attached(box);
        int raised = 0;
        ((ITextInputSession)box).TextInputStateChanged += (_, _) => raised++;

        box.SetSelection(1, 1);
        box.DeleteBackward(); // a key handled in Core

        Assert.Equal(2, raised);
        Assert.Equal("ello", box.Text);
        var change = state.TakeChange();
        Assert.Equal(new TextEditChange(0, 1, 0, new KumikoTextRange(0, 0)), change);
        Assert.True(change!.Value.TextChanged);
        Assert.Null(state.TakeChange());
    }

    [Fact]
    public void CoreChange_SelectionOnly_ReportsNoTextChange()
    {
        var box = Box("hello");
        var state = Attached(box);

        box.SelectAll();

        var change = state.TakeChange();
        Assert.NotNull(change);
        Assert.False(change!.Value.TextChanged);
        Assert.Equal(new KumikoTextRange(0, 5), change.Value.Selection);
    }

    [Fact]
    public void CoreChange_TextReplaced_ReportsTheWholeDifference()
    {
        var box = Box("abc");
        var state = Attached(box);

        box.Text = "xyz";

        Assert.Equal(new TextEditChange(0, 3, 3, box.Selection), state.TakeChange());
    }

    // ── Diff ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("abc", "abXc", 2, 2, 1)]     // insertion
    [InlineData("abXc", "abc", 2, 3, 0)]     // deletion
    [InlineData("abc", "aYc", 1, 2, 1)]      // replacement
    [InlineData("", "abc", 0, 0, 3)]         // from empty
    [InlineData("abc", "", 0, 3, 0)]         // to empty
    [InlineData("aaa", "aaaa", 3, 3, 1)]     // repeated characters: the prefix wins
    public void Diff_IsTheSmallestReplacement(string oldText, string newText, int start, int oldEnd, int newLength)
    {
        var change = TextEditContextState.Diff(oldText, newText, default);

        Assert.Equal(new TextEditChange(start, oldEnd, newLength, default), change);
        // Applying the change to the old text gives the new text
        Assert.Equal(newText, oldText.Remove(start, oldEnd - start).Insert(start, newText.Substring(start, newLength)));
    }

    [Fact]
    public void Diff_NeverSplitsASurrogatePairAtTheStart()
    {
        // 😀 (D83D DE00) becomes 😁 (D83D DE01): the shared high surrogate is part of the change
        var change = TextEditContextState.Diff("a😀", "a😁", default);

        Assert.Equal(new TextEditChange(1, 3, 2, default), change);
    }

    [Fact]
    public void Diff_NeverSplitsASurrogatePairAtTheEnd()
    {
        // 𐐀 (D801 DC00) becomes 𑐀 (D805 DC00): the shared low surrogate is part of the change
        var change = TextEditContextState.Diff("𐐀b", "𑐀b", default);

        Assert.Equal(new TextEditChange(0, 2, 2, default), change);
    }
}
