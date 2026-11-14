using KumikoUI.Core.Input;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The Windows keyboard rules (#42): WinUI virtual keys to Core keys, which key events are marked handled, and
/// how <c>CharacterReceived</c> becomes text.
/// </summary>
public class WindowsKeyInputTests
{
    // ── Key mapping ──────────────────────────────────────────────

    [Theory]
    [InlineData(WindowsKeyInput.Up, KumikoKey.Up)]
    [InlineData(WindowsKeyInput.Down, KumikoKey.Down)]
    [InlineData(WindowsKeyInput.Left, KumikoKey.Left)]
    [InlineData(WindowsKeyInput.Right, KumikoKey.Right)]
    [InlineData(WindowsKeyInput.Home, KumikoKey.Home)]
    [InlineData(WindowsKeyInput.End, KumikoKey.End)]
    [InlineData(WindowsKeyInput.PageUp, KumikoKey.PageUp)]
    [InlineData(WindowsKeyInput.PageDown, KumikoKey.PageDown)]
    [InlineData(WindowsKeyInput.Tab, KumikoKey.Tab)]
    [InlineData(WindowsKeyInput.Enter, KumikoKey.Enter)]
    [InlineData(WindowsKeyInput.Escape, KumikoKey.Escape)]
    [InlineData(WindowsKeyInput.Space, KumikoKey.Space)]
    [InlineData(WindowsKeyInput.Delete, KumikoKey.Delete)]
    [InlineData(WindowsKeyInput.Back, KumikoKey.Backspace)]
    [InlineData(WindowsKeyInput.F2, KumikoKey.F2)]
    public void ToKumikoKey_MapsEveryKeyOfTheReadmeTable(int virtualKey, KumikoKey expected)
    {
        Assert.Equal(expected, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.None));
        Assert.Equal(expected, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Shift));
    }

    [Fact]
    public void VirtualKeyValues_MatchWindows()
    {
        // Windows.System.VirtualKey (winuser.h VK_*) values
        Assert.Equal(0x08, WindowsKeyInput.Back);
        Assert.Equal(0x09, WindowsKeyInput.Tab);
        Assert.Equal(0x0D, WindowsKeyInput.Enter);
        Assert.Equal(0x1B, WindowsKeyInput.Escape);
        Assert.Equal(0x20, WindowsKeyInput.Space);
        Assert.Equal(0x21, WindowsKeyInput.PageUp);
        Assert.Equal(0x22, WindowsKeyInput.PageDown);
        Assert.Equal(0x23, WindowsKeyInput.End);
        Assert.Equal(0x24, WindowsKeyInput.Home);
        Assert.Equal(0x25, WindowsKeyInput.Left);
        Assert.Equal(0x26, WindowsKeyInput.Up);
        Assert.Equal(0x27, WindowsKeyInput.Right);
        Assert.Equal(0x28, WindowsKeyInput.Down);
        Assert.Equal(0x2E, WindowsKeyInput.Delete);
        Assert.Equal('A', WindowsKeyInput.A);
        Assert.Equal('C', WindowsKeyInput.C);
        Assert.Equal('V', WindowsKeyInput.V);
        Assert.Equal('X', WindowsKeyInput.X);
        Assert.Equal('Y', WindowsKeyInput.Y);
        Assert.Equal('Z', WindowsKeyInput.Z);
        Assert.Equal(0x71, WindowsKeyInput.F2);
    }

    [Theory]
    [InlineData(WindowsKeyInput.A, KumikoKey.A)]
    [InlineData(WindowsKeyInput.C, KumikoKey.C)]
    [InlineData(WindowsKeyInput.V, KumikoKey.V)]
    [InlineData(WindowsKeyInput.X, KumikoKey.X)]
    [InlineData(WindowsKeyInput.Y, KumikoKey.Y)]
    [InlineData(WindowsKeyInput.Z, KumikoKey.Z)]
    public void ToKumikoKey_ShortcutLetters_AreKeysOnlyWithControl(int virtualKey, KumikoKey expected)
    {
        Assert.Equal(expected, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Control));
        Assert.Equal(expected, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Control | InputModifiers.Shift));
        // Typed letters are text
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.None));
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Shift));
        // AltGr (Ctrl+Alt) types characters such as Polish "ą" and "ć"
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Control | InputModifiers.Alt));
    }

    [Theory]
    [InlineData('B')]
    [InlineData('Q')]
    [InlineData(0xE5)] // VK_PROCESSKEY: a key an input method is composing with
    [InlineData(0x70)] // F1
    public void ToKumikoKey_OtherKeys_AreNone(int virtualKey)
    {
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.None));
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(virtualKey, InputModifiers.Control));
    }

    // ── Handled ──────────────────────────────────────────────────

    [Theory]
    [InlineData(KumikoKey.Tab)]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.Escape)]
    [InlineData(KumikoKey.Up)]
    [InlineData(KumikoKey.C)]
    public void ShouldMarkHandled_KeyCoreUsed_IsHandled(KumikoKey key)
    {
        Assert.True(WindowsKeyInput.ShouldMarkHandled(key, InputModifiers.None, handledByCore: true));
    }

    [Theory]
    [InlineData(InputModifiers.None)]
    [InlineData(InputModifiers.Shift)]
    public void ShouldMarkHandled_TabCoreLeftAlone_IsTheTabExit(InputModifiers modifiers)
    {
        Assert.False(WindowsKeyInput.ShouldMarkHandled(KumikoKey.Tab, modifiers, handledByCore: false));
    }

    [Theory]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.Escape)]
    [InlineData(KumikoKey.F2)]
    [InlineData(KumikoKey.C)]
    [InlineData(KumikoKey.V)]
    public void ShouldMarkHandled_PageKeysCoreLeftAlone_ReachThePage(KumikoKey key)
    {
        Assert.False(WindowsKeyInput.ShouldMarkHandled(key, InputModifiers.None, handledByCore: false));
    }

    [Theory]
    [InlineData(KumikoKey.Up)]
    [InlineData(KumikoKey.Down)]
    [InlineData(KumikoKey.Left)]
    [InlineData(KumikoKey.Right)]
    [InlineData(KumikoKey.Home)]
    [InlineData(KumikoKey.End)]
    [InlineData(KumikoKey.PageUp)]
    [InlineData(KumikoKey.PageDown)]
    [InlineData(KumikoKey.Backspace)]
    [InlineData(KumikoKey.Delete)]
    [InlineData(KumikoKey.Space)]
    public void ShouldMarkHandled_NavigationKeys_NeverMoveFocusOrScrollThePage(KumikoKey key)
    {
        Assert.True(WindowsKeyInput.ShouldMarkHandled(key, InputModifiers.None, handledByCore: false));
        Assert.True(WindowsKeyInput.ShouldMarkHandled(key, InputModifiers.Shift, handledByCore: false));
        // Alt combinations stay with the app's accelerators (Alt+Left as Back)
        Assert.False(WindowsKeyInput.ShouldMarkHandled(key, InputModifiers.Alt, handledByCore: false));
    }

    // ── Characters ───────────────────────────────────────────────

    [Fact]
    public void Character_Printable_IsText()
    {
        var input = new WindowsCharacterInput();

        Assert.Equal("a", input.Character('a'));
        Assert.Equal("é", input.Character('é'));
        Assert.Equal(" ", input.Character(' '));
    }

    [Theory]
    [InlineData('\r')]
    [InlineData('\n')]
    [InlineData('\t')]
    [InlineData('\b')]
    [InlineData('\u001B')] // Escape
    [InlineData('\u0001')] // Ctrl+A
    [InlineData('\u0016')] // Ctrl+V
    public void Character_Control_IsDroppedBecauseItArrivesAsAKey(char c)
    {
        Assert.Null(new WindowsCharacterInput().Character(c));
    }

    [Fact]
    public void Character_SurrogatePair_IsJoined()
    {
        var input = new WindowsCharacterInput();

        Assert.Null(input.Character('\uD83D'));
        Assert.Equal("😀", input.Character('\uDE00'));
    }

    [Fact]
    public void Character_LoneSurrogates_AreDropped()
    {
        var input = new WindowsCharacterInput();

        Assert.Null(input.Character('\uDE00'));
        Assert.Null(input.Character('\uD83D'));
        // A high surrogate followed by something else is dropped; the other character is kept
        Assert.Equal("b", input.Character('b'));
        Assert.Null(input.Character('\uDE00'));
    }

    [Fact]
    public void Character_SpaceOfASpaceKeyCoreUsed_IsDroppedOnce()
    {
        var input = new WindowsCharacterInput();

        input.KeyDown(KumikoKey.Space, handledByCore: true);
        Assert.Null(input.Character(' '));
        Assert.Equal(" ", input.Character(' '));
    }

    [Fact]
    public void Character_SpaceOfASpaceKeyCoreLeftAlone_IsText()
    {
        var input = new WindowsCharacterInput();

        input.KeyDown(KumikoKey.Space, handledByCore: false);
        Assert.Equal(" ", input.Character(' '));
    }

    [Fact]
    public void Character_AfterAnotherKey_SpaceEchoIsForgotten()
    {
        var input = new WindowsCharacterInput();

        input.KeyDown(KumikoKey.Space, handledByCore: true);
        input.KeyDown(KumikoKey.None, handledByCore: false);
        Assert.Equal(" ", input.Character(' '));
    }

    [Fact]
    public void Character_WhileTheTextServicesDeliverText_IsDropped()
    {
        var input = new WindowsCharacterInput();
        input.TextServicesFocusChanged();

        // Before the text services delivered anything, characters are passed on (they may be unavailable)
        input.KeyDown(KumikoKey.None, handledByCore: false);
        Assert.Equal("a", input.Character('a'));

        // The same keystroke's character after a text update is not entered twice
        input.KeyDown(KumikoKey.None, handledByCore: false);
        input.TextServicesUpdated();
        Assert.Null(input.Character('b'));

        // From then on the text services are the source of text
        input.KeyDown(KumikoKey.None, handledByCore: false);
        Assert.Null(input.Character('c'));

        // Until their focus changes
        input.TextServicesFocusChanged();
        Assert.Equal("d", input.Character('d'));
    }
}
