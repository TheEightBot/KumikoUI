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
    [InlineData(VirtualKey.Up, GridKey.Up)]
    [InlineData(VirtualKey.Down, GridKey.Down)]
    [InlineData(VirtualKey.Left, GridKey.Left)]
    [InlineData(VirtualKey.Right, GridKey.Right)]
    [InlineData(VirtualKey.Home, GridKey.Home)]
    [InlineData(VirtualKey.End, GridKey.End)]
    [InlineData(VirtualKey.PageUp, GridKey.PageUp)]
    [InlineData(VirtualKey.PageDown, GridKey.PageDown)]
    [InlineData(VirtualKey.Tab, GridKey.Tab)]
    public void ToGridKey_NavigationKeys_MapToMatchingGridKey(VirtualKey key, GridKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — action keys
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VirtualKey.Enter, GridKey.Enter)]
    [InlineData(VirtualKey.Escape, GridKey.Escape)]
    [InlineData(VirtualKey.Space, GridKey.Space)]
    [InlineData(VirtualKey.Delete, GridKey.Delete)]
    [InlineData(VirtualKey.F2, GridKey.F2)]
    public void ToGridKey_ActionKeys_MapToMatchingGridKey(VirtualKey key, GridKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    /// <summary>WinUI's <see cref="VirtualKey.Back"/> is the Backspace key — it must map to
    /// <see cref="GridKey.Backspace"/>, NOT a "navigate back" gesture.</summary>
    [Fact]
    public void ToGridKey_Back_MapsToBackspace()
    {
        Assert.Equal(GridKey.Backspace, InputMapping.ToGridKey(VirtualKey.Back));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — letters with grid shortcuts (Ctrl+A/C/V/X/Z)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VirtualKey.A, GridKey.A)]
    [InlineData(VirtualKey.C, GridKey.C)]
    [InlineData(VirtualKey.V, GridKey.V)]
    [InlineData(VirtualKey.X, GridKey.X)]
    [InlineData(VirtualKey.Z, GridKey.Z)]
    public void ToGridKey_ShortcutLetters_MapToMatchingGridKey(VirtualKey key, GridKey expected)
    {
        Assert.Equal(expected, InputMapping.ToGridKey(key));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToGridKey — unmapped keys fall through to None
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Keys the grid does not handle as commands (printable text arrives via CharacterReceived,
    /// and unrelated keys are ignored) must collapse to <see cref="GridKey.None"/>.
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
        Assert.Equal(GridKey.None, InputMapping.ToGridKey(key));
    }

    /// <summary>
    /// Guard the WHOLE contract in one shot: exactly the documented set of <see cref="VirtualKey"/>
    /// values produces a non-<see cref="GridKey.None"/> result; every other VirtualKey maps to None.
    /// This catches an accidental new mapping (or a dropped one) regardless of which arm changed.
    /// </summary>
    [Fact]
    public void ToGridKey_OnlyDocumentedKeys_AreHandled()
    {
        var handled = new Dictionary<VirtualKey, GridKey>
        {
            [VirtualKey.Up] = GridKey.Up,
            [VirtualKey.Down] = GridKey.Down,
            [VirtualKey.Left] = GridKey.Left,
            [VirtualKey.Right] = GridKey.Right,
            [VirtualKey.Home] = GridKey.Home,
            [VirtualKey.End] = GridKey.End,
            [VirtualKey.PageUp] = GridKey.PageUp,
            [VirtualKey.PageDown] = GridKey.PageDown,
            [VirtualKey.Tab] = GridKey.Tab,
            [VirtualKey.Enter] = GridKey.Enter,
            [VirtualKey.Escape] = GridKey.Escape,
            [VirtualKey.Space] = GridKey.Space,
            [VirtualKey.Delete] = GridKey.Delete,
            [VirtualKey.Back] = GridKey.Backspace,
            [VirtualKey.F2] = GridKey.F2,
            [VirtualKey.A] = GridKey.A,
            [VirtualKey.C] = GridKey.C,
            [VirtualKey.V] = GridKey.V,
            [VirtualKey.X] = GridKey.X,
            [VirtualKey.Z] = GridKey.Z,
        };

        foreach (VirtualKey key in Enum.GetValues<VirtualKey>())
        {
            var expected = handled.TryGetValue(key, out var mapped) ? mapped : GridKey.None;
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
    // The Windows/Command key maps to Meta.
    [InlineData(VirtualKeyModifiers.Windows, InputModifiers.Meta)]
    public void ToInputModifiers_SingleFlag_MapsToMatchingModifier(
        VirtualKeyModifiers mods, InputModifiers expected)
    {
        Assert.Equal(expected, InputMapping.ToInputModifiers(mods));
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

        var result = InputMapping.ToInputModifiers(all);

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
