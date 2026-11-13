using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using M = KumikoUI.Maui.AndroidTextInputMapper;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The platform-independent half of the Android text-input adapter (#41): how an input method's
/// <c>InputConnection</c> calls (composition, committed text, surrounding-text deletion, selection) reach Core's
/// <see cref="ITextInputSession"/>, what the input method is told back, hardware key mapping, dead keys and the
/// soft keyboard's input type.
/// </summary>
public class AndroidTextInputMapperTests
{
    private static DrawnTextBox Box(string text = "", Action<DrawnTextBox>? configure = null)
    {
        var box = new DrawnTextBox { Bounds = new KumikoRect(0, 0, 400, 30), Padding = 0, InitialSelectionMode = EditTextSelectionMode.CursorAtEnd };
        configure?.Invoke(box);
        box.Text = text;
        var focus = new DrawnFocusManager();
        focus.Register(box);
        focus.SetFocus(box);
        ((ITextInputSession)box).SetSelection(text.Length, text.Length);
        return box;
    }

    private static (M Mapper, DrawnTextBox Box, List<KumikoKeyEventArgs> Keys) Bound(string text = "", Action<DrawnTextBox>? configure = null)
    {
        var box = Box(text, configure);
        var keys = new List<KumikoKeyEventArgs>();
        var mapper = new M { KeySink = e => { keys.Add(e); return true; } };
        mapper.Bind(box);
        return (mapper, box, keys);
    }

    private static (M Mapper, List<KumikoKeyEventArgs> Keys) Sessionless()
    {
        var keys = new List<KumikoKeyEventArgs>();
        var mapper = new M { KeySink = e => { keys.Add(e); return true; } };
        mapper.Bind(null);
        return (mapper, keys);
    }

    private static void Select(DrawnTextBox box, int start, int end) => ((ITextInputSession)box).SetSelection(start, end);

    // ── Composition lifecycle ─────────────────────────────────────

    [Fact]
    public void Japanese_ComposingThenFinishing_CommitsTheReadingAsOneUndoStep()
    {
        var (m, box, _) = Bound("ab");

        m.SetComposingText("k", 1);
        Assert.Equal("abk", box.Text);
        Assert.Equal(new TextInputSnapshot("abk", 3, 3, 2, 3), m.Current);

        m.SetComposingText("か", 1);
        m.SetComposingText("かな", 1);
        Assert.Equal("abかな", box.Text);
        Assert.Equal(new KumikoTextRange(2, 2), box.Composition);

        m.SetComposingText("仮名", 1); // conversion
        m.FinishComposingText();
        Assert.Equal("ab仮名", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal(4, box.CursorPosition);

        box.Undo();
        Assert.Equal("ab", box.Text);
    }

    [Fact]
    public void Korean_EachSyllableIsCommittedAndTheNextOneComposed()
    {
        var (m, box, _) = Bound();

        m.SetComposingText("ㅎ", 1);
        m.SetComposingText("하", 1);
        m.SetComposingText("한", 1);
        m.CommitText("한", 1);
        m.SetComposingText("ㄱ", 1);
        m.SetComposingText("그", 1);
        m.SetComposingText("글", 1);

        Assert.Equal("한글", box.Text);
        Assert.Equal(new KumikoTextRange(1, 1), box.Composition);

        m.FinishComposingText();
        Assert.Equal("한글", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal(2, box.CursorPosition);
    }

    [Theory]
    [InlineData(1, 5)]   // just after the composition
    [InlineData(0, 2)]   // at its start
    [InlineData(-4, 2)]  // before it: Core keeps the caret inside, so its start
    [InlineData(9, 5)]   // past it: its end
    public void SetComposingText_NewCursorPosition_IsRelativeToTheComposition(int newCursorPosition, int caret)
    {
        var (m, box, _) = Bound("ab");

        m.SetComposingText("xyz", newCursorPosition);

        Assert.Equal("abxyz", box.Text);
        Assert.Equal(caret, box.CursorPosition);
    }

    [Fact]
    public void SetComposingText_Empty_CancelsTheComposition()
    {
        var (m, box, _) = Bound("ab");
        m.SetComposingText("xy", 1);

        m.SetComposingText(string.Empty, 1);

        Assert.Equal("ab", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void SetComposingText_ReplacesTheSelection()
    {
        var (m, box, _) = Bound("hello");
        Select(box, 1, 4);

        m.SetComposingText("EY", 1);

        Assert.Equal("hEYo", box.Text);
        Assert.Equal(new KumikoTextRange(1, 2), box.Composition);
    }

    [Fact]
    public void SetComposingRegion_MarksExistingText_ThenComposingReplacesIt()
    {
        var (m, box, _) = Bound("helo world");
        Select(box, 4, 4);

        m.SetComposingRegion(0, 4);
        Assert.Equal("helo world", box.Text);
        Assert.Equal(new KumikoTextRange(0, 4), box.Composition);
        Assert.Equal(4, box.CursorPosition);

        m.SetComposingText("hello", 1);
        m.FinishComposingText();
        Assert.Equal("hello world", box.Text);
        Assert.Equal(5, box.CursorPosition);
    }

    [Fact]
    public void SetComposingRegion_InsideAnEmoji_WidensToTheWholeCluster()
    {
        var (m, box, _) = Bound("a👍🏽b");

        m.SetComposingRegion(2, 3); // inside the thumbs-up's surrogate pair

        Assert.Equal(new KumikoTextRange(1, 4), box.Composition);
    }

    // ── Committed text ────────────────────────────────────────────

    [Fact]
    public void CommitText_ReplacesTheComposition()
    {
        var (m, box, _) = Bound("ab");
        m.SetComposingText("teh", 1);

        m.CommitText("the ", 1);

        Assert.Equal("abthe ", box.Text);
        Assert.Null(box.Composition);
        Assert.Equal(6, box.CursorPosition);
    }

    [Fact]
    public void CommitText_Empty_RemovesTheCompositionOrDeletesTheSelection()
    {
        var (m, box, _) = Bound("hello");
        m.SetComposingText("xx", 1);
        m.CommitText(string.Empty, 1);
        Assert.Equal("hello", box.Text);

        Select(box, 1, 3);
        m.CommitText(string.Empty, 1);
        Assert.Equal("hlo", box.Text);
        Assert.Equal(1, box.CursorPosition);
    }

    [Theory]
    [InlineData(1, 4)]  // after the text
    [InlineData(0, 2)]  // before it
    [InlineData(2, 5)]  // one past it
    [InlineData(-1, 1)] // one before it
    public void CommitText_NewCursorPosition_IsRelativeToTheInsertedText(int newCursorPosition, int caret)
    {
        var (m, box, _) = Bound("abcd");
        Select(box, 2, 2);

        m.CommitText("XY", newCursorPosition);

        Assert.Equal("abXYcd", box.Text);
        Assert.Equal(caret, box.CursorPosition);
    }

    [Fact]
    public void CommitText_LineBreakAndTab_AreEnterAndTabKeys()
    {
        var (m, box, keys) = Bound("ab");
        m.SetComposingText("c", 1);

        m.CommitText("\n", 1);
        m.CommitText("\t", 1);

        Assert.Equal("abc", box.Text); // the composition is finished first
        Assert.Null(box.Composition);
        Assert.Equal([KumikoKey.Enter, KumikoKey.Tab], keys.Select(k => k.Key));
    }

    [Fact]
    public void CommitText_TheEditorsRulesApply()
    {
        var (m, box, _) = Bound("12", b => b.NumericOnly = true);

        m.CommitText("a", 1);
        Assert.Equal("12", box.Text);
        Assert.False(m.TryTakeSelectionReport(out _)); // nothing moved: the input method is told nothing new

        m.CommitText("3", 1);
        Assert.Equal("123", box.Text);
    }

    [Fact]
    public void CommitText_ReadOnly_ChangesNothing()
    {
        var (m, box, _) = Bound("ab", b => b.IsReadOnly = true);

        m.CommitText("c", 1);
        m.SetComposingText("d", 1);

        Assert.Equal("ab", box.Text);
    }

    [Fact]
    public void CommitText_SurrogatePairArrivesWhole()
    {
        var (m, box, _) = Bound("a");

        m.CommitText("😀", 1);

        Assert.Equal("a😀", box.Text);
        Assert.Equal(3, box.CursorPosition);
    }

    // ── Surrounding-text deletion: UTF-16 code units versus grapheme clusters ──

    [Theory]
    [InlineData("a😀", 1)]                 // one code unit of a surrogate pair
    [InlineData("a👍🏽", 1)]               // emoji + skin tone
    [InlineData("a👨‍👩‍👧", 1)]           // ZWJ family
    [InlineData("a🇯🇵", 1)]               // flag
    [InlineData("aé", 1)]            // e + combining acute
    [InlineData("a😀", 2)]                 // the whole pair, as most input methods send it
    public void DeleteSurroundingText_Before_DeletesWholeClusters(string text, int before)
    {
        var (m, box, _) = Bound(text);

        Assert.True(m.DeleteSurroundingText(before, 0));

        Assert.Equal("a", box.Text);
        Assert.Equal(1, box.CursorPosition);
    }

    [Fact]
    public void DeleteSurroundingText_After_DeletesWholeClusters()
    {
        var (m, box, _) = Bound("a🇯🇵b");
        Select(box, 1, 1);

        m.DeleteSurroundingText(0, 1);

        Assert.Equal("ab", box.Text);
        Assert.Equal(1, box.CursorPosition);
    }

    [Fact]
    public void DeleteSurroundingText_AroundASelection_KeepsTheSelection()
    {
        var (m, box, _) = Bound("abcdef");
        Select(box, 2, 4);

        m.DeleteSurroundingText(1, 1);

        Assert.Equal("acdf", box.Text);
        Assert.Equal(new KumikoTextRange(1, 2), box.Selection);
    }

    [Fact]
    public void DeleteSurroundingText_PastTheEnds_IsClamped()
    {
        var (m, box, _) = Bound("abc");
        Select(box, 1, 1);

        m.DeleteSurroundingText(5, 9);

        Assert.Equal(string.Empty, box.Text);
    }

    [Fact]
    public void DeleteSurroundingText_DuringAComposition_CommitsItFirst()
    {
        var (m, box, _) = Bound("ab");
        m.SetComposingText("cd", 1);

        m.DeleteSurroundingText(1, 0);

        Assert.Equal("abc", box.Text);
        Assert.Null(box.Composition);
    }

    [Fact]
    public void DeleteSurroundingText_NegativeLength_IsRefused()
    {
        var (m, box, _) = Bound("abc");

        Assert.False(m.DeleteSurroundingText(-1, 0));
        Assert.False(m.DeleteSurroundingTextInCodePoints(0, -1));
        Assert.Equal("abc", box.Text);
    }

    [Fact]
    public void DeleteSurroundingTextInCodePoints_CountsASurrogatePairOnce()
    {
        var (m, box, _) = Bound("x😀😀");

        m.DeleteSurroundingTextInCodePoints(1, 0);

        Assert.Equal("x😀", box.Text);
    }

    // ── Selection and reading text ────────────────────────────────

    [Fact]
    public void SetSelection_InRange_SelectsAndOutOfRangeIsIgnored()
    {
        var (m, box, _) = Bound("hello");

        m.SetSelection(1, 3);
        Assert.Equal(new KumikoTextRange(1, 2), box.Selection);

        m.SetSelection(2, 99);
        Assert.Equal(new KumikoTextRange(1, 2), box.Selection);
    }

    [Fact]
    public void SetSelection_InsideAClusterMovesToItsStart()
    {
        var (m, box, _) = Bound("a😀b");

        m.SetSelection(2, 2);

        Assert.Equal(1, box.CursorPosition);
    }

    [Fact]
    public void ReadingText_IsRelativeToTheSelection()
    {
        var (m, box, _) = Bound("hello world");
        Select(box, 4, 7);

        Assert.Equal("ll", m.GetTextBeforeCursor(2));
        Assert.Equal("hell", m.GetTextBeforeCursor(50));
        Assert.Equal("or", m.GetTextAfterCursor(2));
        Assert.Equal("o w", m.GetSelectedText());

        Select(box, 4, 4);
        Assert.Null(m.GetSelectedText());
    }

    [Fact]
    public void ReplaceText_ReplacesTheRangeWithCommittedText()
    {
        var (m, box, _) = Bound("hello world");

        m.ReplaceText(6, 11, "there", 1);

        Assert.Equal("hello there", box.Text);
        Assert.Equal(11, box.CursorPosition);
    }

    // ── What the input method is told ─────────────────────────────

    [Fact]
    public void SelectionReport_FollowsTheAdapterCalls()
    {
        var (m, box, _) = Bound("ab");

        Assert.False(m.TryTakeSelectionReport(out _));

        m.SetComposingText("c", 1);
        Assert.True(m.TryTakeSelectionReport(out var state));
        Assert.Equal(new TextInputSnapshot("abc", 3, 3, 2, 3), state);
        Assert.False(m.TryTakeSelectionReport(out _));

        m.FinishComposingText();
        Assert.True(m.TryTakeSelectionReport(out state));
        Assert.Equal((-1, -1), (state.CompositionStart, state.CompositionEnd));
    }

    [Fact]
    public void ExternalChange_TextRestartsTheInputMethod_SelectionIsReported()
    {
        var (m, box, _) = Bound("hello");
        int raised = 0;
        ((ITextInputSession)box).TextInputStateChanged += (_, _) => raised++;

        box.SelectAll(); // a Core shortcut or gesture
        Assert.Equal(1, raised);
        Assert.Equal(TextInputReport.Selection, m.TakeExternalChange(out var state));
        Assert.Equal((0, 5), (state.SelectionStart, state.SelectionEnd));

        box.Text = "bye"; // undo, paste, a programmatic change
        Assert.Equal(TextInputReport.Restart, m.TakeExternalChange(out _));

        Assert.Equal(TextInputReport.None, m.TakeExternalChange(out _));
    }

    [Fact]
    public void Bind_StartsFromTheNewSurfacesState()
    {
        var (m, _, _) = Bound("one");
        var other = Box("second");

        m.Bind(other);

        Assert.Same(other, m.Session);
        Assert.False(m.TryTakeSelectionReport(out _));
        Assert.Equal(TextInputReport.None, m.TakeExternalChange(out _));
        Assert.Equal("second", m.Current.Text);
    }

    // ── No text surface (combo box, date picker) ──────────────────

    [Fact]
    public void Sessionless_CommittedTextAndDeletionsAreKeys()
    {
        var (m, keys) = Sessionless();

        m.CommitText("a", 1);
        m.CommitText("😀", 1);
        m.DeleteSurroundingText(1, 0);
        m.DeleteSurroundingText(0, 1);

        Assert.Equal('a', keys[0].Character);
        Assert.Equal("😀", keys[1].Text);
        Assert.Null(keys[1].Character);
        Assert.Equal([KumikoKey.Character, KumikoKey.Character, KumikoKey.Backspace, KumikoKey.Delete], keys.Select(k => k.Key));
        Assert.Equal(string.Empty, m.GetTextBeforeCursor(10));
    }

    [Fact]
    public void Sessionless_ACompositionIsTypedWhenFinished()
    {
        var (m, keys) = Sessionless();

        m.SetComposingText("か", 1);
        m.SetComposingText("かな", 1);
        Assert.Empty(keys);

        m.FinishComposingText();
        Assert.Equal("かな", Assert.Single(keys).Text);
    }

    // ── Hardware keys ─────────────────────────────────────────────

    [Theory]
    [InlineData(M.KeycodeDpadUp, KumikoKey.Up)]
    [InlineData(M.KeycodeDpadDown, KumikoKey.Down)]
    [InlineData(M.KeycodeDpadLeft, KumikoKey.Left)]
    [InlineData(M.KeycodeDpadRight, KumikoKey.Right)]
    [InlineData(M.KeycodeMoveHome, KumikoKey.Home)]
    [InlineData(M.KeycodeMoveEnd, KumikoKey.End)]
    [InlineData(M.KeycodePageUp, KumikoKey.PageUp)]
    [InlineData(M.KeycodePageDown, KumikoKey.PageDown)]
    [InlineData(M.KeycodeTab, KumikoKey.Tab)]
    [InlineData(M.KeycodeEnter, KumikoKey.Enter)]
    [InlineData(M.KeycodeNumpadEnter, KumikoKey.Enter)]
    [InlineData(M.KeycodeEscape, KumikoKey.Escape)]
    [InlineData(M.KeycodeDel, KumikoKey.Backspace)]
    [InlineData(M.KeycodeForwardDel, KumikoKey.Delete)]
    [InlineData(M.KeycodeF2, KumikoKey.F2)]
    public void NavigationAndEditingKeys_Map(int keyCode, KumikoKey expected)
    {
        var m = new M();

        Assert.True(m.MapKeyDown(keyCode, 0, 0, out var key, out var text, out var modifiers));

        Assert.Equal(expected, key);
        Assert.Null(text);
        Assert.Equal(InputModifiers.None, modifiers);
    }

    [Theory]
    [InlineData(M.KeycodeA, KumikoKey.A)]
    [InlineData(M.KeycodeC, KumikoKey.C)]
    [InlineData(M.KeycodeV, KumikoKey.V)]
    [InlineData(M.KeycodeX, KumikoKey.X)]
    [InlineData(M.KeycodeY, KumikoKey.Y)]
    [InlineData(M.KeycodeZ, KumikoKey.Z)]
    public void Shortcuts_WithControlOrMeta_AreKeys_PlainTheyAreText(int keyCode, KumikoKey expected)
    {
        var m = new M();
        char letter = char.ToLowerInvariant(expected.ToString()[0]);

        Assert.True(m.MapKeyDown(keyCode, M.MetaCtrlOn, letter, out var key, out _, out var modifiers));
        Assert.Equal(expected, key);
        Assert.Equal(InputModifiers.Control, modifiers);

        Assert.True(m.MapKeyDown(keyCode, M.MetaMetaOn | M.MetaShiftOn, letter, out key, out _, out modifiers));
        Assert.Equal(expected, key);
        Assert.Equal(InputModifiers.Meta | InputModifiers.Shift, modifiers);

        Assert.True(m.MapKeyDown(keyCode, 0, letter, out key, out var text, out _));
        Assert.Equal(KumikoKey.Character, key);
        Assert.Equal(letter.ToString(), text);
    }

    [Fact]
    public void MetaState_MapsEveryModifier()
    {
        Assert.Equal(InputModifiers.Shift | InputModifiers.Alt | InputModifiers.Control | InputModifiers.Meta,
            M.MapMetaState(M.MetaShiftOn | M.MetaAltOn | M.MetaCtrlOn | M.MetaMetaOn));
        Assert.Equal(InputModifiers.None, M.MapMetaState(0));
    }

    [Fact]
    public void ShiftTab_CarriesShift()
    {
        Assert.True(new M().MapKeyDown(M.KeycodeTab, M.MetaShiftOn, 0, out var key, out _, out var modifiers));
        Assert.Equal(KumikoKey.Tab, key);
        Assert.Equal(InputModifiers.Shift, modifiers);
    }

    [Fact]
    public void TypedCharacters_AreText_AltGraphIsNotAModifier()
    {
        var m = new M();

        Assert.True(m.MapKeyDown(33, M.MetaShiftOn, 'E', out var key, out var text, out var modifiers));
        Assert.Equal((KumikoKey.Character, "E", InputModifiers.Shift), (key, text, modifiers));

        Assert.True(m.MapKeyDown(33, M.MetaAltOn, '€', out key, out text, out modifiers));
        Assert.Equal((KumikoKey.Character, "€", InputModifiers.None), (key, text, modifiers));
    }

    [Fact]
    public void Space_IsTheSpaceKeyWithText()
    {
        Assert.True(new M().MapKeyDown(M.KeycodeSpace, 0, ' ', out var key, out var text, out _));
        Assert.Equal(KumikoKey.Space, key);
        Assert.Equal(" ", text);
    }

    [Fact]
    public void KeysTheGridDoesNotUse_AreLeftAlone()
    {
        var m = new M();

        Assert.False(m.MapKeyDown(24 /* volume up */, 0, 0, out _, out _, out _));
        Assert.False(m.MapKeyDown(33 /* E */, M.MetaCtrlOn, 'e', out _, out _, out _));
    }

    [Fact]
    public void DeadKeys_ComposeWithTheNextLetter()
    {
        var m = new M();
        int acute = M.CombiningAccent | 0x0301;

        Assert.True(m.MapKeyDown(33, 0, acute, out var key, out var text, out _));
        Assert.Equal(KumikoKey.None, key);
        Assert.Null(text);

        Assert.True(m.MapKeyDown(33, 0, 'e', out key, out text, out _));
        Assert.Equal((KumikoKey.Character, "é"), (key, text));
    }

    [Fact]
    public void DeadKeys_SpaceOrASecondDeadKeyTypeTheAccent()
    {
        var m = new M();
        int grave = M.CombiningAccent | 0x0300;
        int circumflex = M.CombiningAccent | 0x0302;

        m.ApplyDeadKey(grave);
        Assert.True(m.MapKeyDown(M.KeycodeSpace, 0, ' ', out _, out var text, out _));
        Assert.Equal("`", text);

        m.ApplyDeadKey(grave);
        Assert.Equal("`", m.ApplyDeadKey(circumflex));
        Assert.Equal("â", m.ApplyDeadKey('a'));
    }

    [Fact]
    public void DeadKey_WithALetterThatHasNoPrecomposedForm_KeepsTheCombiningMark()
    {
        var m = new M();
        m.ApplyDeadKey(M.CombiningAccent | 0x0301);

        Assert.Equal("q́", m.ApplyDeadKey('q'));
    }

    [Theory]
    [InlineData(M.IdSelectAll, KumikoKey.A)]
    [InlineData(M.IdCut, KumikoKey.X)]
    [InlineData(M.IdCopy, KumikoKey.C)]
    [InlineData(M.IdPaste, KumikoKey.V)]
    [InlineData(M.IdPasteAsPlainText, KumikoKey.V)]
    public void ContextMenuActions_AreShortcuts(int id, KumikoKey expected) =>
        Assert.Equal(expected, M.MapContextMenuAction(id));

    // ── Keyboard type ─────────────────────────────────────────────

    [Fact]
    public void InputType_FollowsTheEditor()
    {
        var text = Box("x");
        var numeric = Box("1", b => b.NumericOnly = true);
        var digitsMask = Box(string.Empty, b => b.InputMask = "###-##-####");
        var lettersMask = Box(string.Empty, b => b.InputMask = "AA-###");

        Assert.Equal(M.TypeClassText, M.GetInputType(text, "x", false));
        Assert.Equal(M.TypeClassNumber | M.TypeNumberFlagSigned, M.GetInputType(numeric, 42, false));
        Assert.Equal(M.TypeClassNumber | M.TypeNumberFlagSigned | M.TypeNumberFlagDecimal, M.GetInputType(numeric, 4.2m, false));
        Assert.Equal(M.TypeClassNumber | M.TypeNumberFlagSigned | M.TypeNumberFlagDecimal, M.GetInputType(numeric, null, false));
        Assert.Equal(M.TypeClassNumber, M.GetInputType(digitsMask, null, false));
        Assert.Equal(M.TypeClassText | M.TypeTextFlagNoSuggestions, M.GetInputType(lettersMask, null, false));
        Assert.Equal(M.TypeClassText | M.TypeTextVariationFilter | M.TypeTextFlagNoSuggestions, M.GetInputType(text, null, true));
        Assert.Equal(M.TypeClassText | M.TypeTextFlagNoSuggestions, M.GetInputType(null, null, false));
    }

    [Fact]
    public void ImeOptions_NextWhenEnterKeepsEditing_NeverFullScreen()
    {
        Assert.Equal(M.ImeActionDone | M.ImeFlagNoExtractUi | M.ImeFlagNoFullscreen, M.GetImeOptions(false));
        Assert.Equal(M.ImeActionNext | M.ImeFlagNoExtractUi | M.ImeFlagNoFullscreen, M.GetImeOptions(true));
    }
}
