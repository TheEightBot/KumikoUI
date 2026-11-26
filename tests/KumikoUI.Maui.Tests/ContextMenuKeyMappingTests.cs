using KumikoUI.Core.Input;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The context-menu key (#319) on the MAUI keyboard paths that run on any OS: the Menu (Application) key and
/// Shift+F10 reach Core as <see cref="KumikoKey.ContextMenu"/>; F10 alone does not.
/// </summary>
public class ContextMenuKeyMappingTests
{
    [Theory]
    [InlineData(InputModifiers.None)]
    [InlineData(InputModifiers.Shift)]
    public void Windows_TheMenuKey_IsTheContextMenuKey(InputModifiers modifiers)
    {
        Assert.Equal(KumikoKey.ContextMenu, WindowsKeyInput.ToKumikoKey(WindowsKeyInput.Apps, modifiers));
    }

    [Fact]
    public void Windows_ShiftF10_IsTheContextMenuKey_F10AloneIsNothing()
    {
        Assert.Equal(KumikoKey.ContextMenu, WindowsKeyInput.ToKumikoKey(WindowsKeyInput.F10, InputModifiers.Shift));
        Assert.Equal(KumikoKey.None, WindowsKeyInput.ToKumikoKey(WindowsKeyInput.F10, InputModifiers.None));
    }

    [Fact]
    public void Windows_AContextMenuKeyCoreLeftAlone_ReachesThePage()
    {
        Assert.True(WindowsKeyInput.ShouldMarkHandled(KumikoKey.ContextMenu, InputModifiers.None, handledByCore: true));
        Assert.False(WindowsKeyInput.ShouldMarkHandled(KumikoKey.ContextMenu, InputModifiers.None, handledByCore: false));
    }

    [Fact]
    public void Android_TheMenuKey_AndShiftF10_AreTheContextMenuKey()
    {
        var mapper = new AndroidTextInputMapper();

        Assert.True(mapper.MapKeyDown(AndroidTextInputMapper.KeycodeMenu, 0, 0, out var menu, out _, out _));
        Assert.Equal(KumikoKey.ContextMenu, menu);

        Assert.True(mapper.MapKeyDown(AndroidTextInputMapper.KeycodeF10, AndroidTextInputMapper.MetaShiftOn, 0,
            out var shiftF10, out var text, out var modifiers));
        Assert.Equal(KumikoKey.ContextMenu, shiftF10);
        Assert.Null(text);
        Assert.Equal(InputModifiers.Shift, modifiers);

        Assert.Null(AndroidTextInputMapper.MapKeyCode(AndroidTextInputMapper.KeycodeF10, command: false));
    }
}
