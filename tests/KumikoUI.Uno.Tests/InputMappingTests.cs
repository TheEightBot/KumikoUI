using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Windows.System;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Unit tests for the pure, side-effect-free enum translation in
/// <see cref="InputMapping"/>. Only <see cref="InputMapping.ToGridKey"/> and
/// <see cref="InputMapping.ToInputModifiers"/> are covered — both touch WinUI/Uno enums only
/// (<see cref="VirtualKey"/>, <see cref="VirtualKeyModifiers"/>) and need no live Uno runtime.
/// <see cref="InputMapping.GetLiveModifiers"/> is deliberately excluded (it reads thread keyboard
/// state via <c>InputKeyboardSource</c>, which requires the runtime).
///
/// The key/modifier table mirrors docs/uno/04-input-keyboard-focus.md §3.
/// </summary>
public class InputMappingTests
{
    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — navigation keys
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VirtualKey.Up, KumikoKey.Up)]
    [InlineData(VirtualKey.Down, KumikoKey.Down)]
    [InlineData(VirtualKey.Left, KumikoKey.Left)]
    [InlineData(VirtualKey.Right, KumikoKey.Right)]
    [InlineData(VirtualKey.Home, KumikoKey.Home)]
    [InlineData(VirtualKey.End, KumikoKey.End)]
    [InlineData(VirtualKey.PageUp, KumikoKey.PageUp)]
    [InlineData(VirtualKey.PageDown, KumikoKey.PageDown)]
    [InlineData(VirtualKey.Tab, KumikoKey.Tab)]
    public void ToGridKey_NavigationKeys_MapToMatchingGridKey(VirtualKey key, KumikoKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — action keys
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VirtualKey.Enter, KumikoKey.Enter)]
    [InlineData(VirtualKey.Escape, KumikoKey.Escape)]
    [InlineData(VirtualKey.Space, KumikoKey.Space)]
    [InlineData(VirtualKey.Delete, KumikoKey.Delete)]
    [InlineData(VirtualKey.F2, KumikoKey.F2)]
    public void ToGridKey_ActionKeys_MapToMatchingGridKey(VirtualKey key, KumikoKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    /// <summary>WinUI's <see cref="VirtualKey.Back"/> is the Backspace key — it must map to
    /// <see cref="KumikoKey.Backspace"/>, NOT a "navigate back" gesture.</summary>
    [Fact]
    public void ToGridKey_Back_MapsToBackspace()
    {
        Assert.Equal(KumikoKey.Backspace, InputMapping.ToGridKey(VirtualKey.Back));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — letters with grid shortcuts (Ctrl+A/C/V/X/Z)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VirtualKey.A, KumikoKey.A)]
    [InlineData(VirtualKey.C, KumikoKey.C)]
    [InlineData(VirtualKey.V, KumikoKey.V)]
    [InlineData(VirtualKey.X, KumikoKey.X)]
    [InlineData(VirtualKey.Z, KumikoKey.Z)]
    public void ToGridKey_ShortcutLetters_MapToMatchingGridKey(VirtualKey key, KumikoKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — unmapped keys fall through to None
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Keys the grid does not handle as commands (printable text arrives via CharacterReceived,
    /// and unrelated keys are ignored) must collapse to <see cref="KumikoKey.None"/>.
    /// </summary>
    [Theory]
    [InlineData(VirtualKey.B)]          // a letter with no grid shortcut
    [InlineData(VirtualKey.Q)]
    [InlineData(VirtualKey.Number0)]    // digits are not commands
    [InlineData(VirtualKey.Number9)]
    [InlineData(VirtualKey.F1)]         // other function keys
    [InlineData(VirtualKey.F5)]
    [InlineData(VirtualKey.Shift)]      // bare modifier keys are not commands
    [InlineData(VirtualKey.Control)]
    [InlineData(VirtualKey.Menu)]
    [InlineData(VirtualKey.CapitalLock)]
    [InlineData(VirtualKey.Insert)]
    [InlineData(VirtualKey.None)]
    public void ToGridKey_UnmappedKeys_ReturnNone(VirtualKey key)
    {
        Assert.Equal(KumikoKey.None, InputMapping.ToGridKey(key));
    }

    /// <summary>
    /// Guard the WHOLE contract in one shot: exactly the documented set of <see cref="VirtualKey"/>
    /// values produces a non-<see cref="KumikoKey.None"/> result; every other VirtualKey maps to None.
    /// This catches an accidental new mapping (or a dropped one) regardless of which arm changed.
    /// </summary>
    [Fact]
    public void ToGridKey_OnlyDocumentedKeys_AreHandled()
    {
        var handled = new Dictionary<VirtualKey, KumikoKey>
        {
            [VirtualKey.Up] = KumikoKey.Up,
            [VirtualKey.Down] = KumikoKey.Down,
            [VirtualKey.Left] = KumikoKey.Left,
            [VirtualKey.Right] = KumikoKey.Right,
            [VirtualKey.Home] = KumikoKey.Home,
            [VirtualKey.End] = KumikoKey.End,
            [VirtualKey.PageUp] = KumikoKey.PageUp,
            [VirtualKey.PageDown] = KumikoKey.PageDown,
            [VirtualKey.Tab] = KumikoKey.Tab,
            [VirtualKey.Enter] = KumikoKey.Enter,
            [VirtualKey.Escape] = KumikoKey.Escape,
            [VirtualKey.Space] = KumikoKey.Space,
            [VirtualKey.Delete] = KumikoKey.Delete,
            [VirtualKey.Back] = KumikoKey.Backspace,
            [VirtualKey.F2] = KumikoKey.F2,
            [VirtualKey.A] = KumikoKey.A,
            [VirtualKey.C] = KumikoKey.C,
            [VirtualKey.V] = KumikoKey.V,
            [VirtualKey.X] = KumikoKey.X,
            [VirtualKey.Z] = KumikoKey.Z,
        };

        foreach (VirtualKey key in Enum.GetValues<VirtualKey>())
        {
            var expected = handled.TryGetValue(key, out var mapped) ? mapped : KumikoKey.None;
            Assert.Equal(expected, InputMapping.ToGridKey(key));
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToInputModifiers — single flags
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToInputModifiers_None_ReturnsNone()
    {
        Assert.Equal(InputModifiers.None, InputMapping.ToInputModifiers(VirtualKeyModifiers.None));
    }

    [Theory]
    [InlineData(VirtualKeyModifiers.Shift, InputModifiers.Shift)]
    [InlineData(VirtualKeyModifiers.Control, InputModifiers.Control)]
    // Menu == Alt in WinUI nomenclature.
    [InlineData(VirtualKeyModifiers.Menu, InputModifiers.Alt)]
    // The Windows key maps to Meta (Windows/Linux semantics).
    [InlineData(VirtualKeyModifiers.Windows, InputModifiers.Meta)]
    public void ToInputModifiers_SingleFlag_MapsToMatchingModifier(
        VirtualKeyModifiers mods, InputModifiers expected)
    {
        Assert.Equal(expected, InputMapping.ToInputModifiers(mods, commandActsAsControl: false));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToInputModifiers — Apple / browser Command key
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>On Apple platforms and in the browser, Command is Core's Control (shortcuts, toggle-click).</summary>
    [Fact]
    public void ToInputModifiers_CommandOnApple_SetsControlAndMeta()
    {
        var result = InputMapping.ToInputModifiers(VirtualKeyModifiers.Windows, commandActsAsControl: true);

        Assert.Equal(InputModifiers.Control | InputModifiers.Meta, result);
    }

    [Fact]
    public void ToInputModifiers_CommandShiftOnApple_SetsControlShiftAndMeta()
    {
        var result = InputMapping.ToInputModifiers(
            VirtualKeyModifiers.Windows | VirtualKeyModifiers.Shift, commandActsAsControl: true);

        Assert.Equal(InputModifiers.Control | InputModifiers.Shift | InputModifiers.Meta, result);
    }

    [Fact]
    public void ToInputModifiers_ControlOnApple_StaysControl()
    {
        var result = InputMapping.ToInputModifiers(VirtualKeyModifiers.Control, commandActsAsControl: true);

        Assert.Equal(InputModifiers.Control, result);
    }

    /// <summary>The public overload applies the current platform's rule (keyboard and pointer paths share it).</summary>
    [Fact]
    public void ToInputModifiers_DefaultOverload_UsesPlatformRule()
    {
        var expected = InputMapping.CommandActsAsControl
            ? InputModifiers.Control | InputModifiers.Meta
            : InputModifiers.Meta;

        Assert.Equal(expected, InputMapping.ToInputModifiers(VirtualKeyModifiers.Windows));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToInputModifiers — combined flags
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToInputModifiers_ShiftAndControl_CombinesBothFlags()
    {
        var result = InputMapping.ToInputModifiers(
            VirtualKeyModifiers.Shift | VirtualKeyModifiers.Control);

        Assert.Equal(InputModifiers.Shift | InputModifiers.Control, result);
        Assert.True(result.HasFlag(InputModifiers.Shift));
        Assert.True(result.HasFlag(InputModifiers.Control));
        Assert.False(result.HasFlag(InputModifiers.Alt));
        Assert.False(result.HasFlag(InputModifiers.Meta));
    }

    [Fact]
    public void ToInputModifiers_AllFlags_CombinesEveryModifier()
    {
        var all = VirtualKeyModifiers.Shift | VirtualKeyModifiers.Control
                | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Windows;

        var result = InputMapping.ToInputModifiers(all, commandActsAsControl: false);

        Assert.Equal(
            InputModifiers.Shift | InputModifiers.Control | InputModifiers.Alt | InputModifiers.Meta,
            result);
    }

    /// <summary>Ctrl+Alt (a common AltGr-style combo) must yield exactly Control|Alt.</summary>
    [Fact]
    public void ToInputModifiers_ControlAndMenu_CombinesControlAndAlt()
    {
        var result = InputMapping.ToInputModifiers(
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu);

        Assert.Equal(InputModifiers.Control | InputModifiers.Alt, result);
    }
}
