using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Tests for the pure translation of keyboard-proxy text into Core key/character events.
/// </summary>
public class ProxyTextInputTests
{
    private static ProxyTextInput.Dispatch Char(char c) => new(KumikoKey.Character, c);

    // ── Diff ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ab", "abc", 2, 0, 2, 0, "c")]          // append
    [InlineData("ac", "abc", 1, 0, 1, 0, "b")]          // insert in the middle
    [InlineData("aa", "aaa", 1, 0, 1, 0, "a")]          // next to the same letter: the caret decides
    [InlineData("aa", "aaa", 0, 0, 0, 0, "a")]
    [InlineData("hello", "hi", 1, 4, 1, 4, "i")]        // typing over a selection
    [InlineData("hello", "に", 0, 5, 0, 5, "に")]        // a selection replaced
    [InlineData("abc", "ab", 3, 0, 2, 1, "")]           // Backspace at the end
    [InlineData("abc", "ac", 2, 0, 1, 1, "")]           // Backspace in the middle
    [InlineData("aab", "ab", 2, 0, 1, 1, "")]           // Backspace after a repeated letter
    [InlineData("aab", "ab", 0, 0, 0, 1, "")]           // Delete before a repeated letter
    [InlineData("abc", "a", 1, 2, 1, 2, "")]            // deleting a selection
    [InlineData("teh", "the ", 3, 0, 1, 2, "he ")]      // a suggestion replaces a word: common prefix and suffix
    [InlineData("abc", "abc", 3, 0, 3, 0, "")]          // no change
    public void Diff_FindsTheSingleEdit(string oldText, string newText, int selectionStart, int selectionLength,
        int start, int removed, string inserted)
    {
        Assert.Equal(new ProxyTextInput.TextChange(start, removed, inserted),
            ProxyTextInput.Diff(oldText, newText, selectionStart, selectionLength));
    }

    [Fact]
    public void Diff_NeverSplitsASurrogatePair()
    {
        // 😀 (D83D DE00) replaced by 😃 (D83D DE03): both share the high surrogate.
        Assert.Equal(new ProxyTextInput.TextChange(1, 2, "😃"), ProxyTextInput.Diff("a😀b", "a😃b", 0, 0));
    }

    [Fact]
    public void Diff_AtTheCaret_DoesNotSplitAPairEither()
    {
        // The caret is reported inside 😀 (between its halves): the edit falls back to whole code points.
        Assert.Equal(new ProxyTextInput.TextChange(1, 2, "😃"), ProxyTextInput.Diff("x😀", "x😃", 2, 0));
    }

    // ── Plan: typed characters ───────────────────────────────────

    [Fact]
    public void Plan_PrintableCharacters_AreSentAsGridKeyCharacter()
    {
        Assert.Equal(new[] { Char('a'), Char('1'), Char(' ') }, ProxyTextInput.Plan("a1 ", KumikoKey.None));
    }

    // ── Plan: macOS function-key characters (F2 → U+F705) ────────

    [Theory]
    [InlineData('')] // F2
    [InlineData('')] // Up arrow
    [InlineData('')] // Forward delete
    [InlineData('')] // end of the AppKit range
    public void Plan_AppKitFunctionKeyCharacter_IsDropped(char functionKey)
    {
        Assert.Empty(ProxyTextInput.Plan(functionKey.ToString(), KumikoKey.None));
    }

    [Fact]
    public void Plan_FunctionKeyMixedWithText_KeepsOnlyText()
    {
        Assert.Equal(new[] { Char('a'), Char('b') }, ProxyTextInput.Plan("ab", KumikoKey.None));
    }

    [Fact]
    public void Plan_OtherControlCharacters_AreDropped()
    {
        Assert.Empty(ProxyTextInput.Plan("\u0001\u001B\u007F", KumikoKey.None));
    }

    // ── Plan: Tab / Enter echoes ─────────────────────────────────

    [Fact]
    public void Plan_TabEchoAfterKeyDownTab_IsDroppedSoTabFiresOnce()
    {
        Assert.Empty(ProxyTextInput.Plan("\t", KumikoKey.Tab));
    }

    [Fact]
    public void Plan_TabWithoutKeyDown_IsSentAsTab()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(KumikoKey.Tab, '\t') }, ProxyTextInput.Plan("\t", KumikoKey.None));
    }

    [Fact]
    public void Plan_OnlyOneEchoIsDropped()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(KumikoKey.Tab, '\t') }, ProxyTextInput.Plan("\t\t", KumikoKey.Tab));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Plan_EnterEchoAfterKeyDownEnter_IsDropped(string echo)
    {
        Assert.Empty(ProxyTextInput.Plan(echo, KumikoKey.Enter));
    }

    [Fact]
    public void Plan_SoftKeyboardReturn_IsOneEnter()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(KumikoKey.Enter, '\r') }, ProxyTextInput.Plan("\r\n", KumikoKey.None));
    }

    [Fact]
    public void Plan_EchoOfDifferentKey_IsNotDropped()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(KumikoKey.Tab, '\t') }, ProxyTextInput.Plan("\t", KumikoKey.Enter));
    }
}
