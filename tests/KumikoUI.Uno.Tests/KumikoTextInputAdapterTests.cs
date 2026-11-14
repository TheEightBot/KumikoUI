using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using static KumikoUI.Uno.Input.InputMapping;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The Uno text-input adapter (#43): keyboard and input-method sequences played on a fake proxy text box pass
/// through <see cref="KumikoTextInputAdapter"/> into a Core <see cref="ITextInputSession"/>, and keys report
/// handled only when Core handled them.
/// </summary>
public class KumikoTextInputAdapterTests
{
    private static (DrawnTextBox Box, FakeTextInputProxy Proxy, KumikoTextInputAdapter Adapter, TextBoxTarget Target)
        Editor(string text, Action<DrawnTextBox>? configure = null)
    {
        var box = new DrawnTextBox { InitialSelectionMode = EditTextSelectionMode.CursorAtEnd };
        configure?.Invoke(box);
        box.Text = text;
        var target = new TextBoxTarget(box);
        var proxy = new FakeTextInputProxy();
        var adapter = new KumikoTextInputAdapter(proxy, target);
        proxy.Adapter = adapter;
        adapter.Sync(); // the host syncs when the edit begins
        return (box, proxy, adapter, target);
    }

    private static (CoreGridTarget Grid, FakeTextInputProxy Proxy, KumikoTextInputAdapter Adapter) Grid()
    {
        var grid = new CoreGridTarget();
        var proxy = new FakeTextInputProxy();
        var adapter = new KumikoTextInputAdapter(proxy, grid);
        proxy.Adapter = adapter;
        adapter.Sync();
        return (grid, proxy, adapter);
    }

    private static ITextInputSession Session(DrawnTextBox box) => box;

    // ── Mirroring ────────────────────────────────────────────────

    [Fact]
    public void Sync_MirrorsTheEditorsTextAndCaretIntoTheProxy()
    {
        var (box, proxy, _, _) = Editor("abc");

        Assert.Equal("abc", proxy.Text);
        Assert.Equal(box.CursorPosition, proxy.SelectionStart);
        Assert.Equal(box.Selection.Length, proxy.SelectionLength);
    }

    [Fact]
    public void Typing_InsertsIntoTheEditor_AndTheProxyKeepsTheText()
    {
        var (box, proxy, _, _) = Editor("ab");

        proxy.Type("c");
        proxy.Type("d");

        Assert.Equal("abcd", box.Text);
        Assert.Equal(4, box.CursorPosition);
        Assert.Equal("abcd", proxy.Text); // never reset: an input method keeps its context
    }

    [Fact]
    public void Typing_NextToTheSameLetter_LandsAtTheCaret()
    {
        var (box, proxy, _, _) = Editor("aa");
        Session(box).SetSelection(1, 1);
        proxy.SetTextAndSelection("aa", 1, 0);

        proxy.Type("a");

        Assert.Equal("aaa", box.Text);
        Assert.Equal(2, box.CursorPosition);
    }

    [Fact]
    public void SoftKeyboardBackspace_DeletesBeforeTheCaret()
    {
        var (box, proxy, _, _) = Editor("abc");

        proxy.Backspace();

        Assert.Equal("ab", box.Text);
        Assert.Equal("ab", proxy.Text);
    }

    [Fact]
    public void ASurrogatePair_ArrivesWhole()
    {
        var (box, proxy, _, _) = Editor("a");

        proxy.Type("👍🏽");

        Assert.Equal("a👍🏽", box.Text);
        Assert.Equal(5, box.CursorPosition);
    }

    [Fact]
    public void DeletingPartOfAnEmojiSequence_DeletesTheWholeCluster()
    {
        var (box, proxy, _, _) = Editor("a👍🏽b");
        Session(box).SetSelection(5, 5);
        proxy.SetTextAndSelection("a👍🏽b", 5, 0);

        proxy.Replace(3, 2, ""); // the platform removes only the skin-tone modifier

        Assert.Equal("ab", box.Text);
        Assert.Equal("ab", proxy.Text);
    }

    [Fact]
    public void ASuggestionReplacingAWord_ReplacesItInTheEditor()
    {
        var (box, proxy, _, _) = Editor("teh");

        proxy.Replace(0, 3, "the "); // Android autocorrect or a suggestion-bar pick

        Assert.Equal("the ", box.Text);
        Assert.Equal(4, box.CursorPosition);
    }

    [Fact]
    public void TextTheEditorRejects_IsTakenBackOutOfTheProxy()
    {
        var (box, proxy, _, _) = Editor("12", b => b.NumericOnly = true);

        proxy.Type("x");

        Assert.Equal("12", box.Text);
        Assert.Equal("12", proxy.Text);
        Assert.Equal(2, proxy.SelectionStart);
    }

    [Fact]
    public void TypingAtTheEnd_WhileTheTextBoxRaisesItsChange_LeavesTheProxyToTheSyncAfterIt()
    {
        // Uno's TextBox cannot take a selection while it raises TextChanging (typing at the end of a cell threw,
        // which crashed the Android input connection): the adapter writes the proxy only in the sync after it
        var (box, proxy, adapter, _) = Editor("ab");
        proxy.RaisesChangesLikeUno = true;

        proxy.Type("c");
        proxy.Type("d");

        Assert.Equal("abcd", box.Text);
        Assert.Equal(4, box.CursorPosition);

        adapter.Sync(); // what the host queues after each change
        Assert.Equal("abcd", proxy.Text);
        Assert.Equal(4, proxy.SelectionStart);
    }

    [Fact]
    public void TextTheEditorRejects_WhileTheTextBoxRaisesItsChange_IsTakenBackByTheSync()
    {
        var (box, proxy, adapter, _) = Editor("12", b => b.NumericOnly = true);
        proxy.RaisesChangesLikeUno = true;

        proxy.Type("x");
        adapter.Sync();

        Assert.Equal("12", box.Text);
        Assert.Equal("12", proxy.Text);
        Assert.Equal(2, proxy.SelectionStart);
    }

    [Fact]
    public void AnEditorChangeFromAnotherRoute_UpdatesTheProxy()
    {
        var (box, proxy, _, _) = Editor("abc");

        box.Text = "xyz"; // a programmatic change raises TextInputStateChanged

        Assert.Equal("xyz", proxy.Text);
    }

    [Fact]
    public void AnAppKitFunctionKeyCharacter_IsNotTyped()
    {
        var (box, proxy, _, _) = Editor("ab");

        proxy.Type(""); // F2 on macOS

        Assert.Equal("ab", box.Text);
        Assert.Equal("ab", proxy.Text);
    }

    // ── Composition ──────────────────────────────────────────────

    [Fact]
    public void Japanese_KanaComposition_IsMarkedWhileComposing_AndCommittedAtTheEnd()
    {
        var (box, proxy, adapter, _) = Editor("ab");
        int writes = proxy.Writes;

        proxy.StartComposition();
        Assert.True(adapter.IsComposing);

        proxy.UpdateComposition("k", 1);
        Assert.Equal("abk", box.Text);
        Assert.Equal(new KumikoTextRange(2, 1), box.Composition);

        proxy.UpdateComposition("か", 1);
        proxy.UpdateComposition("かn", 2);
        proxy.UpdateComposition("かな", 2);
        Assert.Equal("abかな", box.Text);
        Assert.Equal(new KumikoTextRange(2, 2), box.Composition);
        Assert.Equal(writes, proxy.Writes); // the proxy is never rewritten under a composition

        proxy.CompleteComposition("かな");

        Assert.False(adapter.IsComposing);
        Assert.Null(box.Composition);
        Assert.Equal("abかな", box.Text);
        Assert.Equal(4, box.CursorPosition);
        Assert.Equal("abかな", proxy.Text);
    }

    [Fact]
    public void Japanese_KanjiConversion_CommitsTheConvertedText()
    {
        var (box, proxy, _, _) = Editor("");

        proxy.StartComposition();
        proxy.UpdateComposition("に", 1);
        proxy.UpdateComposition("にほん", 3);
        proxy.UpdateComposition("日本", 2); // the conversion key replaces the reading
        proxy.CompleteComposition("日本");

        Assert.Equal("日本", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void Composition_InTheMiddleOfText_LeavesTheTextAroundItAlone()
    {
        var (box, proxy, _, _) = Editor("ad");
        Session(box).SetSelection(1, 1);
        proxy.SetTextAndSelection("ad", 1, 0);

        proxy.StartComposition();
        proxy.UpdateComposition("b", 1);
        proxy.UpdateComposition("bc", 2);
        Assert.Equal("abcd", box.Text);
        Assert.Equal(new KumikoTextRange(1, 2), box.Composition);
        proxy.CompleteComposition("bc");

        Assert.Equal("abcd", box.Text);
        Assert.Equal(3, box.CursorPosition);
    }

    [Fact]
    public void CancelingAComposition_RestoresTheText()
    {
        var (box, proxy, _, _) = Editor("ab");

        proxy.StartComposition();
        proxy.UpdateComposition("k", 1);
        proxy.UpdateComposition("か", 1);
        proxy.CancelComposition();

        Assert.Equal("ab", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal("ab", proxy.Text);
    }

    [Fact]
    public void Composition_ReplacesTheSelection()
    {
        var (box, proxy, _, _) = Editor("hello");
        Session(box).SetSelection(0, 5);
        proxy.SetTextAndSelection("hello", 0, 5);

        proxy.StartComposition();
        proxy.UpdateComposition("に", 1);
        Assert.Equal("に", box.Text);
        Assert.Equal(new KumikoTextRange(0, 1), box.Composition);
        proxy.CompleteComposition("に");

        Assert.Equal("に", box.Text);
    }

    [Fact]
    public void Composition_PartialCommit_CommitsTheFirstPartAndKeepsMarkingTheRest()
    {
        var (box, proxy, _, _) = Editor("ab");

        proxy.StartComposition();
        proxy.UpdateComposition("にほん", 3);
        proxy.PartiallyCommit("日", "ほん"); // Chinese/Japanese input methods commit a phrase at a time
        Assert.Equal("ab日ほん", box.Text);
        Assert.Equal(new KumikoTextRange(3, 2), box.Composition);

        proxy.UpdateComposition("本", 1);
        proxy.CompleteComposition("本");

        Assert.Equal("ab日本", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void DeadKey_ComposesTheAccentThenCommitsTheLetter()
    {
        var (box, proxy, _, _) = Editor("caf");

        proxy.StartComposition();
        proxy.UpdateComposition("´", 1);
        Assert.Equal(new KumikoTextRange(3, 1), box.Composition);
        proxy.CompleteComposition("é");

        Assert.Equal("café", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void Composition_TheEditorsRulesApplyAtCommit_AndTheProxyFollows()
    {
        var (box, proxy, _, _) = Editor("1", b => b.NumericOnly = true);

        proxy.StartComposition();
        proxy.UpdateComposition("ab", 2);
        Assert.Equal("1ab", box.Text); // marked letters show while composing
        proxy.CompleteComposition("ab");

        Assert.Equal("1", box.Text);
        Assert.Equal("1", proxy.Text);
    }

    [Fact]
    public void KeysDuringAComposition_BelongToTheInputMethod()
    {
        var (_, proxy, adapter, target) = Editor("ab");
        proxy.StartComposition();
        proxy.UpdateComposition("か", 1);

        Assert.Null(adapter.OnKeyDown(KumikoKey.Enter));
        Assert.Null(adapter.OnKeyDown(KumikoKey.Backspace));
        Assert.Empty(target.Keys);
    }

    [Fact]
    public void AnEditorChangeDuringAComposition_AbandonsIt()
    {
        var (box, proxy, adapter, _) = Editor("ab");
        proxy.StartComposition();
        proxy.UpdateComposition("か", 1);

        box.Text = "xyz"; // for example a click that moved the caret elsewhere

        Assert.False(adapter.IsComposing);
        Assert.Equal("xyz", proxy.Text);
    }

    // ── Type-to-edit ─────────────────────────────────────────────

    [Fact]
    public void TypingOnACell_StartsAnEdit_AndTheProxyMirrorsTheEditor()
    {
        var (grid, proxy, adapter) = Grid();
        grid.Select(1, 0);

        proxy.Type("Z");
        Assert.True(grid.Edit.IsEditing);
        Assert.Equal("Z", proxy.Text);

        proxy.Type("ed");
        var result = adapter.OnKeyDown(KumikoKey.Enter);

        Assert.Equal(ProxyKeyOutcome.Handled, result?.Outcome);
        Assert.Equal("Zed", grid.Items[1].Name);
        Assert.Equal("", proxy.Text); // no editor open: the proxy is empty
    }

    [Fact]
    public void ComposingOnACell_StartsAnEdit_AndTheCompositionContinuesInTheEditor()
    {
        var (grid, proxy, adapter) = Grid();
        grid.Select(0, 0);

        proxy.StartComposition();
        proxy.UpdateComposition("k", 1);
        Assert.True(grid.Edit.IsEditing);
        var session = grid.Input.ActiveTextInput!;
        Assert.Equal("k", session.Text);
        Assert.Equal(new KumikoTextRange(0, 1), session.Composition);

        proxy.UpdateComposition("か", 1);
        proxy.UpdateComposition("かな", 2);
        proxy.CompleteComposition("仮名");
        Assert.Null(session.Composition);
        Assert.Equal("仮名", proxy.Text);

        adapter.OnKeyDown(KumikoKey.Enter);
        Assert.Equal("仮名", grid.Items[0].Name);
    }

    [Fact]
    public void CancelingACompositionThatStartedAnEdit_LeavesTheEditorEmpty()
    {
        var (grid, proxy, _) = Grid();
        grid.Select(0, 0);

        proxy.StartComposition();
        proxy.UpdateComposition("k", 1);
        proxy.CancelComposition();

        Assert.True(grid.Edit.IsEditing);
        Assert.Equal("", grid.Input.ActiveTextInput!.Text);
        Assert.Equal("", proxy.Text);
    }

    // ── Keys: handled only when Core handled them ────────────────

    [Fact]
    public void Tab_InsideTheGrid_IsHandled_AndPastTheLastTabStop_MovesFocusOut()
    {
        var (grid, _, adapter) = Grid();
        grid.Select(2, 0);

        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.Tab)?.Outcome); // to Age, editing
        var result = adapter.OnKeyDown(KumikoKey.Tab); // past the last tab stop

        Assert.Equal(ProxyKeyOutcome.MoveFocusOut, result?.Outcome);
        Assert.False(result?.Backwards);
        Assert.False(grid.Edit.IsEditing); // the edit was committed on the way out
    }

    [Fact]
    public void ShiftTab_BeforeTheFirstTabStop_MovesFocusOutBackwards()
    {
        var (grid, _, adapter) = Grid();
        grid.Select(0, 0);
        grid.Modifiers = InputModifiers.Shift;

        var result = adapter.OnKeyDown(KumikoKey.Tab);

        Assert.Equal(ProxyKeyOutcome.MoveFocusOut, result?.Outcome);
        Assert.True(result?.Backwards);
    }

    [Fact]
    public void Tab_WithNoCurrentCell_MovesFocusOut()
    {
        var (_, _, adapter) = Grid();

        Assert.Equal(ProxyKeyOutcome.MoveFocusOut, adapter.OnKeyDown(KumikoKey.Tab)?.Outcome);
    }

    [Fact]
    public void Escape_WithNothingToCancel_ReachesThePage()
    {
        var (_, _, adapter) = Grid();

        Assert.Equal(ProxyKeyOutcome.PassToPage, adapter.OnKeyDown(KumikoKey.Escape)?.Outcome);
    }

    [Fact]
    public void Escape_ClearingTheSelection_OrCancelingAnEdit_IsHandled()
    {
        var (grid, proxy, adapter) = Grid();
        grid.Select(0, 0);
        proxy.Type("Q"); // type-to-edit

        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.Escape)?.Outcome); // cancels the edit
        Assert.False(grid.Edit.IsEditing);
        Assert.Equal("Ann", grid.Items[0].Name);
        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.Escape)?.Outcome); // clears the selection
        Assert.Equal(ProxyKeyOutcome.PassToPage, adapter.OnKeyDown(KumikoKey.Escape)?.Outcome); // nothing left
    }

    [Fact]
    public void AnUnhandledKey_IsNotMarkedHandled()
    {
        var (_, _, adapter) = Grid();

        // No current cell: Enter, F2 and Backspace do nothing in Core.
        Assert.Equal(ProxyKeyOutcome.PassToPage, adapter.OnKeyDown(KumikoKey.Enter)?.Outcome);
        Assert.Equal(ProxyKeyOutcome.PassToPage, adapter.OnKeyDown(KumikoKey.F2)?.Outcome);
        Assert.Equal(ProxyKeyOutcome.PassToPage, adapter.OnKeyDown(KumikoKey.Backspace)?.Outcome);
    }

    [Fact]
    public void ArrowKeysWhileEditing_GoToTheEditor_AndTheProxyCaretFollows()
    {
        var (box, proxy, adapter, _) = Editor("abc");

        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.Left)?.Outcome);

        Assert.Equal(2, box.CursorPosition);
        Assert.Equal(2, proxy.SelectionStart);
    }

    [Fact]
    public void BackspaceFromAHardwareKeyboard_GoesToCore_AndTheProxyFollows()
    {
        var (box, proxy, adapter, _) = Editor("abc");

        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.Backspace)?.Outcome);

        Assert.Equal("ab", box.Text);
        Assert.Equal("ab", proxy.Text);
    }

    [Theory]
    [InlineData(KumikoKey.A)]
    [InlineData(KumikoKey.C)]
    [InlineData(KumikoKey.V)]
    [InlineData(KumikoKey.X)]
    [InlineData(KumikoKey.Z)]
    [InlineData(KumikoKey.Y)]
    public void APlainShortcutLetter_IsLeftToTheTextBoxToType(KumikoKey key)
    {
        var (_, _, adapter, target) = Editor("ab");

        Assert.Null(adapter.OnKeyDown(key));
        Assert.Empty(target.Keys);
    }

    [Fact]
    public void ACommandShortcut_GoesToCore()
    {
        var (grid, _, adapter) = Grid();
        grid.Modifiers = InputModifiers.Control;

        Assert.Equal(ProxyKeyOutcome.Handled, adapter.OnKeyDown(KumikoKey.A)?.Outcome); // select all rows
    }

    [Fact]
    public void SpaceWhileEditing_IsTyped()
    {
        var (_, _, adapter, target) = Editor("ab");

        Assert.Null(adapter.OnKeyDown(KumikoKey.Space));
        Assert.Empty(target.Keys);
    }

    [Fact]
    public void TheTextFormOfAnUnhandledTab_IsNotDeliveredAgain()
    {
        var (grid, proxy, adapter) = Grid();

        adapter.OnKeyDown(KumikoKey.Tab); // no current cell: unhandled
        proxy.Type("\t"); // the text box echoes it

        Assert.Single(grid.Keys);
        Assert.Equal("", proxy.Text);
    }

    [Fact]
    public void ASoftKeyboardReturn_CommitsTheEdit()
    {
        var (grid, proxy, _) = Grid();
        grid.Select(0, 0);
        proxy.Type("Q");

        proxy.Type("\n"); // a soft keyboard's Return arrives as text, with no KeyDown

        Assert.False(grid.Edit.IsEditing);
        Assert.Equal("Q", grid.Items[0].Name);
    }

    [Fact]
    public void Sync_WhenTheEditEnds_EmptiesTheProxy()
    {
        var (grid, proxy, adapter) = Grid();
        grid.Select(0, 0);
        proxy.Type("Q");

        grid.Edit.CancelEdit(); // by another route, for example a click elsewhere
        adapter.Sync();

        Assert.Equal("", proxy.Text);
    }
}
