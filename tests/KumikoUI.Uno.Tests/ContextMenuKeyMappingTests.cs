using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Windows.System;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The context-menu key (#319): the Menu (Application) key and Shift+F10 reach Core as
/// <see cref="KumikoKey.ContextMenu"/>; F10 alone does not, and the other keys map as before.
/// </summary>
public class ContextMenuKeyMappingTests
{
    [Theory]
    [InlineData(InputModifiers.None)]
    [InlineData(InputModifiers.Shift)]
    public void TheMenuKey_IsTheContextMenuKey(InputModifiers modifiers)
    {
        Assert.Equal(KumikoKey.ContextMenu, InputMapping.ToGridKey(VirtualKey.Application, modifiers));
    }

    [Fact]
    public void ShiftF10_IsTheContextMenuKey_F10AloneIsNothing()
    {
        Assert.Equal(KumikoKey.ContextMenu, InputMapping.ToGridKey(VirtualKey.F10, InputModifiers.Shift));
        Assert.Equal(KumikoKey.None, InputMapping.ToGridKey(VirtualKey.F10, InputModifiers.None));
    }

    [Theory]
    [InlineData(VirtualKey.Enter, KumikoKey.Enter)]
    [InlineData(VirtualKey.F2, KumikoKey.F2)]
    [InlineData(VirtualKey.A, KumikoKey.A)]
    [InlineData(VirtualKey.Q, KumikoKey.None)]
    public void OtherKeys_MapAsWithoutModifiers(VirtualKey key, KumikoKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key, InputModifiers.Shift));
    }
}
