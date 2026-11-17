using KumikoUI.Uno.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>The host's soft keyboard rule when Core asks for keyboard focus (#275).</summary>
public class SoftKeyboardPolicyTests
{
    [Fact]
    public void FilterSearchTappedByTouch_WithTheProxyAlreadyFocused_ShowsTheKeyboard()
    {
        // No cell edit, but the search box is Core's active text input: focus did not change, so show explicitly.
        Assert.Equal(SoftKeyboardAction.Show, SoftKeyboardPolicy.Decide(false, true, true, true));
    }

    [Fact]
    public void FilterSearchOpened_WithTheProxyNotFocused_LeavesItToTheFocusChange()
    {
        Assert.Equal(SoftKeyboardAction.None, SoftKeyboardPolicy.Decide(false, true, false, true));
    }

    [Fact]
    public void MouseOrPen_NeverShowsTheKeyboard()
    {
        Assert.Equal(SoftKeyboardAction.None, SoftKeyboardPolicy.Decide(false, true, true, false));
    }

    [Fact]
    public void TextEditor_WithTheProxyFocusedByTouch_ShowsTheKeyboard()
    {
        Assert.Equal(SoftKeyboardAction.Show, SoftKeyboardPolicy.Decide(true, true, true, true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditorWithoutTextInput_HidesTheKeyboard(bool proxyHadFocus)
    {
        Assert.Equal(SoftKeyboardAction.Hide, SoftKeyboardPolicy.Decide(true, false, proxyHadFocus, true));
    }
}
