#if MACCATALYST || IOS
using CoreGraphics;
using Foundation;
using KumikoUI.Core.Input;
using ObjCRuntime;
using UIKit;

namespace KumikoUI.Maui;

/// <summary>
/// Native UIView that acts as a keyboard input sink on Apple platforms.
/// Implements IUIKeyInput to receive character input and backspace,
/// and overrides PressesBegan for arrow keys, Escape, Delete, etc.
/// </summary>
internal class KeyInputResponder : UIView, IUIKeyInput
{
    public event Action<string>? TextInserted;
    public event Action? BackspaceDeleted;
    public event Action<UIPress>? KeyPressed;
    /// <summary>Fires when Tab is pressed via UIKeyCommand (bypasses iOS focus system).</summary>
    public event Action<bool>? TabKeyReceived;
    /// <summary>Fires when a navigation key is pressed via UIKeyCommand (bypasses iOS focus system).</summary>
    public event Action<GridKey, InputModifiers>? NavigationKeyReceived;

    // UIKeyCommand entries for Tab and navigation keys — takes priority over the
    // iOS focus system, which would otherwise steal these for focus navigation.
    private static readonly Selector _navKeySelector = new Selector("handleNavKey:");
    private static readonly UIKeyCommand[] _keyCommands = BuildKeyCommands();

    private static UIKeyCommand[] BuildKeyCommands()
    {
        var cmds = new List<UIKeyCommand>();

        // Tab / Shift+Tab
        cmds.Add(UIKeyCommand.Create((NSString)"\t", 0, new Selector("handleTabKey:")));
        cmds.Add(UIKeyCommand.Create((NSString)"\t", UIKeyModifierFlags.Shift, new Selector("handleShiftTabKey:")));

        // Arrow keys, Escape — each with plain + Shift variants
        var navInputs = new[]
        {
            UIKeyCommand.LeftArrow,
            UIKeyCommand.RightArrow,
            UIKeyCommand.UpArrow,
            UIKeyCommand.DownArrow,
            UIKeyCommand.Escape,
            UIKeyCommand.PageUp,
            UIKeyCommand.PageDown,
            UIKeyCommand.Home,
            UIKeyCommand.End,
        };

        foreach (var input in navInputs)
        {
            cmds.Add(UIKeyCommand.Create((NSString)input, 0, _navKeySelector));
            cmds.Add(UIKeyCommand.Create((NSString)input, UIKeyModifierFlags.Shift, _navKeySelector));
        }

        return cmds.ToArray();
    }

    public KeyInputResponder(CGRect frame) : base(frame) { }

    public override bool CanBecomeFirstResponder => true;

    public override UIKeyCommand[] KeyCommands => _keyCommands;

    [Export("handleTabKey:")]
    private void HandleTabKey(UIKeyCommand cmd) => TabKeyReceived?.Invoke(false);

    [Export("handleShiftTabKey:")]
    private void HandleShiftTabKey(UIKeyCommand cmd) => TabKeyReceived?.Invoke(true);

    [Export("handleNavKey:")]
    private void HandleNavKey(UIKeyCommand cmd)
    {
        var input = cmd.Input;
        if (input == null) return;

        var modifiers = InputModifiers.None;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= InputModifiers.Shift;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= InputModifiers.Control;
        if (cmd.ModifierFlags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= InputModifiers.Alt;

        GridKey? gridKey = input switch
        {
            var s when s == UIKeyCommand.LeftArrow => GridKey.Left,
            var s when s == UIKeyCommand.RightArrow => GridKey.Right,
            var s when s == UIKeyCommand.UpArrow => GridKey.Up,
            var s when s == UIKeyCommand.DownArrow => GridKey.Down,
            var s when s == UIKeyCommand.Escape => GridKey.Escape,
            var s when s == UIKeyCommand.PageUp => GridKey.PageUp,
            var s when s == UIKeyCommand.PageDown => GridKey.PageDown,
            var s when s == UIKeyCommand.Home => GridKey.Home,
            var s when s == UIKeyCommand.End => GridKey.End,
            _ => null
        };

        if (gridKey.HasValue)
            NavigationKeyReceived?.Invoke(gridKey.Value, modifiers);
    }

    public bool HasText => true;

    public void InsertText(string text)
    {
        TextInserted?.Invoke(text);
    }

    public void DeleteBackward()
    {
        BackspaceDeleted?.Invoke();
    }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        bool handled = false;
        foreach (UIPress press in presses)
        {
            if (press.Key != null)
            {
                var keyCode = press.Key.KeyCode;
                // Only handle non-character keys here (characters come via InsertText)
                bool isNavigationKey = keyCode switch
                {
                    UIKeyboardHidUsage.KeyboardLeftArrow => true,
                    UIKeyboardHidUsage.KeyboardRightArrow => true,
                    UIKeyboardHidUsage.KeyboardUpArrow => true,
                    UIKeyboardHidUsage.KeyboardDownArrow => true,
                    UIKeyboardHidUsage.KeyboardHome => true,
                    UIKeyboardHidUsage.KeyboardEnd => true,
                    UIKeyboardHidUsage.KeyboardPageUp => true,
                    UIKeyboardHidUsage.KeyboardPageDown => true,
                    UIKeyboardHidUsage.KeyboardEscape => true,
                    UIKeyboardHidUsage.KeyboardDeleteForward => true,
                    UIKeyboardHidUsage.KeyboardReturnOrEnter => true,
                    UIKeyboardHidUsage.KeyboardTab => true,
                    UIKeyboardHidUsage.KeyboardSpacebar => true,
                    UIKeyboardHidUsage.KeyboardF2 => true,
                    _ => false
                };

                if (isNavigationKey)
                {
                    KeyPressed?.Invoke(press);
                    handled = true;
                }
            }
        }

        if (!handled)
            base.PressesBegan(presses, evt);
    }
}
#endif
