using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using static KumikoUI.Uno.Input.InputMapping;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// What the hidden proxy TextBox does with a command key after Core has seen it
/// (<see cref="InputMapping.GetProxyKeyOutcome"/>): the grid must never be a keyboard trap.
/// </summary>
public class ProxyKeyOutcomeTests
{
    public static TheoryData<KumikoKey> CommandKeys() => new()
    {
        KumikoKey.Tab, KumikoKey.Enter, KumikoKey.Escape, KumikoKey.F2, KumikoKey.Space,
        KumikoKey.Up, KumikoKey.Down, KumikoKey.Left, KumikoKey.Right, KumikoKey.Home, KumikoKey.End,
        KumikoKey.PageUp, KumikoKey.PageDown, KumikoKey.Backspace, KumikoKey.Delete,
        KumikoKey.A, KumikoKey.C, KumikoKey.V, KumikoKey.X, KumikoKey.Z, KumikoKey.Y,
    };

    [Theory]
    [MemberData(nameof(CommandKeys))]
    public void AKeyCoreHandled_IsMarkedHandled(KumikoKey key)
    {
        Assert.Equal(ProxyKeyOutcome.Handled, GetProxyKeyOutcome(key, handledByCore: true));
    }

    [Fact]
    public void AnUnhandledTab_MovesFocusOutOfTheGrid()
    {
        Assert.Equal(ProxyKeyOutcome.MoveFocusOut, GetProxyKeyOutcome(KumikoKey.Tab, handledByCore: false));
    }

    [Theory]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.Escape)]
    [InlineData(KumikoKey.F2)]
    public void AnUnhandledKeyTheTextBoxIgnores_ReachesThePage(KumikoKey key)
    {
        Assert.Equal(ProxyKeyOutcome.PassToPage, GetProxyKeyOutcome(key, handledByCore: false));
    }

    [Theory]
    [InlineData(KumikoKey.Up)]
    [InlineData(KumikoKey.Left)]
    [InlineData(KumikoKey.Home)]
    [InlineData(KumikoKey.End)]
    [InlineData(KumikoKey.Backspace)]
    [InlineData(KumikoKey.Delete)]
    [InlineData(KumikoKey.Space)]
    [InlineData(KumikoKey.V)]
    public void AnUnhandledCaretOrTextKey_StaysHandled_SoTheProxyTextIsUntouched(KumikoKey key)
    {
        Assert.Equal(ProxyKeyOutcome.Handled, GetProxyKeyOutcome(key, handledByCore: false));
    }
}
