using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Tests for the pure translation of keyboard-proxy text into Core key/character events.
/// </summary>
public class ProxyTextInputTests
{
    private const string Sentinel = "​";

    private static ProxyTextInput.Dispatch Char(char c) => new(GridKey.Character, c);

    // ── ExtractInserted ──────────────────────────────────────────

    [Theory]
    [InlineData(Sentinel, Sentinel + "a", "a")]          // plain append
    [InlineData(Sentinel, Sentinel + "ab", "ab")]        // fast typing / paste
    [InlineData(Sentinel, "a" + Sentinel, "a")]          // insertion before the sentinel
    [InlineData(Sentinel, "日本", "日本")]               // IME full replacement
    public void ExtractInserted_ReturnsTextWithoutSentinel(string oldText, string newText, string expected)
    {
        Assert.Equal(expected, ProxyTextInput.ExtractInserted(oldText, newText, Sentinel));
    }

    // ── Plan: typed characters ───────────────────────────────────

    [Fact]
    public void Plan_PrintableCharacters_AreSentAsGridKeyCharacter()
    {
        Assert.Equal(new[] { Char('a'), Char('1'), Char(' ') }, ProxyTextInput.Plan("a1 ", GridKey.None));
    }

    // ── Plan: macOS function-key characters (F2 → U+F705) ────────

    [Theory]
    [InlineData('')] // F2
    [InlineData('')] // Up arrow
    [InlineData('')] // Forward delete
    [InlineData('')] // end of the AppKit range
    public void Plan_AppKitFunctionKeyCharacter_IsDropped(char functionKey)
    {
        Assert.Empty(ProxyTextInput.Plan(functionKey.ToString(), GridKey.None));
    }

    [Fact]
    public void Plan_FunctionKeyMixedWithText_KeepsOnlyText()
    {
        Assert.Equal(new[] { Char('a'), Char('b') }, ProxyTextInput.Plan("ab", GridKey.None));
    }

    [Fact]
    public void Plan_OtherControlCharacters_AreDropped()
    {
        Assert.Empty(ProxyTextInput.Plan("\u0001\u001B\u007F", GridKey.None));
    }

    // ── Plan: Tab / Enter echoes ─────────────────────────────────

    [Fact]
    public void Plan_TabEchoAfterKeyDownTab_IsDroppedSoTabFiresOnce()
    {
        Assert.Empty(ProxyTextInput.Plan("\t", GridKey.Tab));
    }

    [Fact]
    public void Plan_TabWithoutKeyDown_IsSentAsTab()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(GridKey.Tab, '\t') }, ProxyTextInput.Plan("\t", GridKey.None));
    }

    [Fact]
    public void Plan_OnlyOneEchoIsDropped()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(GridKey.Tab, '\t') }, ProxyTextInput.Plan("\t\t", GridKey.Tab));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Plan_EnterEchoAfterKeyDownEnter_IsDropped(string echo)
    {
        Assert.Empty(ProxyTextInput.Plan(echo, GridKey.Enter));
    }

    [Fact]
    public void Plan_SoftKeyboardReturn_IsOneEnter()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(GridKey.Enter, '\r') }, ProxyTextInput.Plan("\r\n", GridKey.None));
    }

    [Fact]
    public void Plan_EchoOfDifferentKey_IsNotDropped()
    {
        Assert.Equal(new[] { new ProxyTextInput.Dispatch(GridKey.Tab, '\t') }, ProxyTextInput.Plan("\t", GridKey.Enter));
    }
}
