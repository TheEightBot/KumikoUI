#if MACCATALYST || IOS
using CoreGraphics;
using Foundation;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using ObjCRuntime;
using UIKit;

namespace KumikoUI.Maui;

/// <summary>
/// The grid's first responder on Apple platforms: a hidden <see cref="UIView"/> that implements <c>UITextInput</c>,
/// so the keyboard and input methods (Japanese, Chinese and Korean candidates, dead keys, dictation, the emoji
/// keyboard) edit the open cell editor's <see cref="ITextInputSession"/> through <see cref="AppleTextInputBridge"/>
/// (#40). Hardware navigation keys arrive as <see cref="UIKeyCommand"/>s and through <see cref="PressesBegan"/>, and go
/// to the grid as key events, except while an input method is composing.
/// </summary>
internal sealed class KeyInputResponder : UIView, IUITextInput
{
    private readonly AppleTextInputBridge _bridge;
    private readonly Action<KumikoKey, InputModifiers> _sendKey;
    private readonly Func<UIView?> _gridCoordinateSpace;
    private UITextInputStringTokenizer? _tokenizer;

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

        // Command+A/C/X/V/Z (and Shift+Z for redo): select all, copy, cut, paste, undo, redo. UIKit does not
        // deliver a letter pressed with Command through InsertText or PressesBegan, so each is a key command.
        foreach (var letter in new[] { "a", "c", "x", "v", "z" })
            cmds.Add(UIKeyCommand.Create((NSString)letter, UIKeyModifierFlags.Command, _navKeySelector));
        cmds.Add(UIKeyCommand.Create((NSString)"z", UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, _navKeySelector));

        // The grid's commands come before the text system's own handling of these keys (which would move a caret
        // in an empty document, or select all of it): while an input method composes, CanPerform declines them.
        foreach (var cmd in cmds)
            cmd.WantsPriorityOverSystemBehavior = true;

        return cmds.ToArray();
    }

    /// <summary>Creates the responder.</summary>
    /// <param name="bridge">Applies UIKit's text-input calls to the grid.</param>
    /// <param name="sendKey">Sends a non-text key to the grid's input controller.</param>
    /// <param name="gridCoordinateSpace">The view the grid draws in (the canvas), for converting rectangles.</param>
    public KeyInputResponder(AppleTextInputBridge bridge, Action<KumikoKey, InputModifiers> sendKey, Func<UIView?> gridCoordinateSpace)
        : base(CGRect.Empty)
    {
        _bridge = bridge;
        _sendKey = sendKey;
        _gridCoordinateSpace = gridCoordinateSpace;
        _bridge.Invalidated += OnBridgeInvalidated;
    }

    /// <inheritdoc/>
    public override bool CanBecomeFirstResponder => true;

    /// <inheritdoc/>
    public override UIKeyCommand[] KeyCommands => _keyCommands;

    /// <summary>
    /// Asked before Tab (argument <c>true</c> for Shift+Tab) is claimed. Returning false leaves the key to
    /// UIKit — keyboard focus navigation — so the grid is not a keyboard trap at its last (first) tab stop.
    /// Null claims every Tab.
    /// </summary>
    public Func<bool, bool>? CanHandleTab { get; set; }

    /// <summary>Stops following the bridge; call before dropping the responder.</summary>
    public void Detach()
    {
        _bridge.Invalidated -= OnBridgeInvalidated;
        _bridge.Detach();
        CanHandleTab = null;
    }

    private bool ClaimsTab(bool shift) => CanHandleTab?.Invoke(shift) ?? true;

    // ── Key commands ─────────────────────────────────────────────

    // UIKit asks before dispatching a key command: declining one lets the press continue to the text system (an
    // input method composing) or to UIKit (Tab past the last tab stop moves focus out of the grid).
    private bool CanPerformCore(Selector action, NSObject? withSender)
    {
        if (action.Name is "handleTabKey:" or "handleShiftTabKey:" or "handleNavKey:" && _bridge.HasMarkedText)
            return false;
        if (action.Name == "handleTabKey:") return ClaimsTab(false);
        if (action.Name == "handleShiftTabKey:") return ClaimsTab(true);
        return base.CanPerform(action, withSender);
    }

    [Export("handleTabKey:")]
    private void HandleTabKey(UIKeyCommand cmd) => _sendKey(KumikoKey.Tab, InputModifiers.None);

    [Export("handleShiftTabKey:")]
    private void HandleShiftTabKey(UIKeyCommand cmd) => _sendKey(KumikoKey.Tab, InputModifiers.Shift);

    [Export("handleNavKey:")]
    private void HandleNavKey(UIKeyCommand cmd)
    {
        var input = cmd.Input;
        if (input == null) return;

        var modifiers = MapModifiers(cmd.ModifierFlags);

        KumikoKey? gridKey = input switch
        {
            var s when s == UIKeyCommand.LeftArrow => KumikoKey.Left,
            var s when s == UIKeyCommand.RightArrow => KumikoKey.Right,
            var s when s == UIKeyCommand.UpArrow => KumikoKey.Up,
            var s when s == UIKeyCommand.DownArrow => KumikoKey.Down,
            var s when s == UIKeyCommand.Escape => KumikoKey.Escape,
            var s when s == UIKeyCommand.PageUp => KumikoKey.PageUp,
            var s when s == UIKeyCommand.PageDown => KumikoKey.PageDown,
            var s when s == UIKeyCommand.Home => KumikoKey.Home,
            var s when s == UIKeyCommand.End => KumikoKey.End,
            _ => input.ToString() switch
            {
                "a" => KumikoKey.A,
                "c" => KumikoKey.C,
                "x" => KumikoKey.X,
                "v" => KumikoKey.V,
                "z" => KumikoKey.Z,
                _ => null
            }
        };

        if (gridKey.HasValue)
            _sendKey(gridKey.Value, modifiers);
    }

    private static InputModifiers MapModifiers(UIKeyModifierFlags flags)
    {
        var modifiers = InputModifiers.None;
        if (flags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= InputModifiers.Shift;
        if (flags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= InputModifiers.Control;
        if (flags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= InputModifiers.Alt;
        return modifiers;
    }

    // The Edit menu on Mac Catalyst (and the edit menu of a pointer or touch gesture) reaches the responder
    // chain as the standard edit actions: they are the same shortcuts as Command+A/C/X/V.
    /// <inheritdoc/>
    public override bool CanPerform(Selector action, NSObject? withSender)
    {
        if (action.Name is "copy:" or "cut:" or "paste:" or "selectAll:") return !_bridge.HasMarkedText;
        return CanPerformCore(action, withSender);
    }

    /// <inheritdoc/>
    public override void Copy(NSObject? sender) => _sendKey(KumikoKey.C, InputModifiers.Control);

    /// <inheritdoc/>
    public override void Cut(NSObject? sender) => _sendKey(KumikoKey.X, InputModifiers.Control);

    /// <inheritdoc/>
    public override void Paste(NSObject? sender) => _sendKey(KumikoKey.V, InputModifiers.Control);

    /// <inheritdoc/>
    public override void SelectAll(NSObject? sender) => _sendKey(KumikoKey.A, InputModifiers.Control);

    // ── Hardware keys that are not key commands ──────────────────

    /// <inheritdoc/>
    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        bool handled = false;
        foreach (UIPress press in presses)
        {
            if (press.Key is not { } key) continue;

            // Only keys that are not text are taken here; characters come through InsertText, Backspace through
            // DeleteBackward. While an input method composes, it gets every key.
            KumikoKey? gridKey = key.KeyCode switch
            {
                UIKeyboardHidUsage.KeyboardLeftArrow => KumikoKey.Left,
                UIKeyboardHidUsage.KeyboardRightArrow => KumikoKey.Right,
                UIKeyboardHidUsage.KeyboardUpArrow => KumikoKey.Up,
                UIKeyboardHidUsage.KeyboardDownArrow => KumikoKey.Down,
                UIKeyboardHidUsage.KeyboardHome => KumikoKey.Home,
                UIKeyboardHidUsage.KeyboardEnd => KumikoKey.End,
                UIKeyboardHidUsage.KeyboardPageUp => KumikoKey.PageUp,
                UIKeyboardHidUsage.KeyboardPageDown => KumikoKey.PageDown,
                UIKeyboardHidUsage.KeyboardEscape => KumikoKey.Escape,
                UIKeyboardHidUsage.KeyboardDeleteForward => KumikoKey.Delete,
                UIKeyboardHidUsage.KeyboardReturnOrEnter => KumikoKey.Enter,
                UIKeyboardHidUsage.KeypadEnter => KumikoKey.Enter,
                UIKeyboardHidUsage.KeyboardTab when ClaimsTab(key.ModifierFlags.HasFlag(UIKeyModifierFlags.Shift)) => KumikoKey.Tab,
                UIKeyboardHidUsage.KeyboardSpacebar => KumikoKey.Space,
                UIKeyboardHidUsage.KeyboardF2 => KumikoKey.F2,
                _ => null
            };

            if (gridKey is { } k && !_bridge.ShouldLeaveKeyToTextSystem(k))
            {
                _sendKey(k, MapModifiers(key.ModifierFlags));
                handled = true;
            }
        }

        if (!handled)
            base.PressesBegan(presses, evt);
    }

    // ── UIKeyInput ───────────────────────────────────────────────

    /// <inheritdoc/>
    public bool HasText
    {
        [Export("hasText")]
        get => _bridge.HasText;
    }

    /// <inheritdoc/>
    [Export("insertText:")]
    public void InsertText(string text) => _bridge.InsertText(text);

    /// <inheritdoc/>
    [Export("deleteBackward")]
    public void DeleteBackward() => _bridge.DeleteBackward();

    // ── UITextInputTraits ────────────────────────────────────────

    /// <summary>A numeric editor gets the numbers keyboard.</summary>
    [Export("keyboardType")]
    public UIKeyboardType KeyboardType =>
        _bridge.IsNumeric ? UIKeyboardType.NumbersAndPunctuation : UIKeyboardType.Default;

    /// <summary>No autocorrection: cell values are data, not prose.</summary>
    [Export("autocorrectionType")]
    public UITextAutocorrectionType AutocorrectionType => UITextAutocorrectionType.No;

    /// <summary>No spell checking.</summary>
    [Export("spellCheckingType")]
    public UITextSpellCheckingType SpellCheckingType => UITextSpellCheckingType.No;

    /// <summary>Quotes are typed as they are.</summary>
    [Export("smartQuotesType")]
    public UITextSmartQuotesType SmartQuotesType => UITextSmartQuotesType.No;

    /// <summary>Dashes are typed as they are.</summary>
    [Export("smartDashesType")]
    public UITextSmartDashesType SmartDashesType => UITextSmartDashesType.No;

    /// <summary>No spaces are added or removed around pasted words.</summary>
    [Export("smartInsertDeleteType")]
    public UITextSmartInsertDeleteType SmartInsertDeleteType => UITextSmartInsertDeleteType.No;

    /// <summary>No inline predictions: their gray text would be drawn by nothing.</summary>
    [Export("inlinePredictionType")]
    public UITextInlinePredictionType InlinePredictionType => UITextInlinePredictionType.No;

    // ── UITextInput: text and selection ──────────────────────────

    /// <inheritdoc/>
    [Export("textInRange:")]
    public string TextInRange(UITextRange range) =>
        range is IndexedRange r ? _bridge.TextInRange(r.StartIndex, r.EndIndex) : string.Empty;

    /// <inheritdoc/>
    [Export("replaceRange:withText:")]
    public void ReplaceText(UITextRange range, string text)
    {
        if (range is IndexedRange r) _bridge.ReplaceRange(r.StartIndex, r.EndIndex, text);
    }

    /// <inheritdoc/>
    public UITextRange? SelectedTextRange
    {
        [Export("selectedTextRange")]
        get
        {
            var selection = _bridge.SelectedRange;
            return new IndexedRange(selection.Start, selection.End);
        }
        [Export("setSelectedTextRange:")]
        set
        {
            if (value is IndexedRange r) _bridge.SetSelectedRange(r.StartIndex, r.EndIndex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Nil (despite the binding's non-nullable type) when no input method is composing.</remarks>
    public UITextRange MarkedTextRange
    {
        [Export("markedTextRange")]
        get => _bridge.MarkedRange is { } marked ? new IndexedRange(marked.Start, marked.End) : null!;
    }

    /// <inheritdoc/>
    public NSDictionary? MarkedTextStyle
    {
        [Export("markedTextStyle")]
        get;
        [Export("setMarkedTextStyle:")]
        set;
    }

    /// <inheritdoc/>
    [Export("setMarkedText:selectedRange:")]
    public void SetMarkedText(string? markedText, NSRange selectedRange) =>
        _bridge.SetMarkedText(markedText, selectedRange.Location, selectedRange.Length);

    /// <inheritdoc/>
    [Export("unmarkText")]
    public void UnmarkText() => _bridge.UnmarkText();

    // ── UITextInput: positions ───────────────────────────────────

    /// <inheritdoc/>
    public UITextPosition BeginningOfDocument
    {
        [Export("beginningOfDocument")]
        get => new IndexedPosition(0);
    }

    /// <inheritdoc/>
    public UITextPosition EndOfDocument
    {
        [Export("endOfDocument")]
        get => new IndexedPosition(_bridge.Text.Length);
    }

    /// <inheritdoc/>
    [Export("textRangeFromPosition:toPosition:")]
    public UITextRange GetTextRange(UITextPosition fromPosition, UITextPosition toPosition) =>
        fromPosition is IndexedPosition from && toPosition is IndexedPosition to
            ? new IndexedRange(Math.Min(from.Index, to.Index), Math.Max(from.Index, to.Index))
            : null!;

    /// <inheritdoc/>
    [Export("positionFromPosition:offset:")]
    public UITextPosition GetPosition(UITextPosition fromPosition, nint offset) =>
        fromPosition is IndexedPosition from && _bridge.PositionFromPosition(from.Index, offset) is { } index
            ? new IndexedPosition(index)
            : null!; // nil: outside the text

    /// <inheritdoc/>
    [Export("positionFromPosition:inDirection:offset:")]
    public UITextPosition GetPosition(UITextPosition fromPosition, UITextLayoutDirection inDirection, nint offset) =>
        fromPosition is IndexedPosition from && _bridge.PositionInDirection(from.Index, Map(inDirection), offset) is { } index
            ? new IndexedPosition(index)
            : null!; // nil: outside the text

    /// <inheritdoc/>
    [Export("comparePosition:toPosition:")]
    public NSComparisonResult ComparePosition(UITextPosition first, UITextPosition second)
    {
        int a = (first as IndexedPosition)?.Index ?? 0;
        int b = (second as IndexedPosition)?.Index ?? 0;
        return a < b ? NSComparisonResult.Ascending : a > b ? NSComparisonResult.Descending : NSComparisonResult.Same;
    }

    /// <inheritdoc/>
    [Export("offsetFromPosition:toPosition:")]
    public nint GetOffsetFromPosition(UITextPosition fromPosition, UITextPosition toPosition) =>
        ((toPosition as IndexedPosition)?.Index ?? 0) - ((fromPosition as IndexedPosition)?.Index ?? 0);

    /// <inheritdoc/>
    [Export("positionWithinRange:farthestInDirection:")]
    public UITextPosition GetPositionWithinRange(UITextRange range, UITextLayoutDirection direction) =>
        range is IndexedRange r
            ? new IndexedPosition(direction is UITextLayoutDirection.Left or UITextLayoutDirection.Up ? r.StartIndex : r.EndIndex)
            : null!;

    /// <inheritdoc/>
    [Export("characterRangeByExtendingPosition:inDirection:")]
    public UITextRange GetCharacterRange(UITextPosition byExtendingPosition, UITextLayoutDirection direction)
    {
        if (byExtendingPosition is not IndexedPosition position) return null!;
        var range = _bridge.CharacterRangeByExtending(position.Index, Map(direction));
        return new IndexedRange(range.Start, range.End);
    }

    private static AppleTextInputBridge.Direction Map(UITextLayoutDirection direction) => direction switch
    {
        UITextLayoutDirection.Left => AppleTextInputBridge.Direction.Left,
        UITextLayoutDirection.Up => AppleTextInputBridge.Direction.Up,
        UITextLayoutDirection.Down => AppleTextInputBridge.Direction.Down,
        _ => AppleTextInputBridge.Direction.Right,
    };

    // ── UITextInput: writing direction (the grid draws left to right) ──

    /// <inheritdoc/>
    [Export("baseWritingDirectionForPosition:inDirection:")]
    public NSWritingDirection GetBaseWritingDirection(UITextPosition forPosition, UITextStorageDirection direction) =>
        NSWritingDirection.LeftToRight;

    /// <inheritdoc/>
    [Export("setBaseWritingDirection:forRange:")]
    public void SetBaseWritingDirectionforRange(NSWritingDirection writingDirection, UITextRange range)
    {
    }

    // ── UITextInput: geometry ────────────────────────────────────

    /// <inheritdoc/>
    [Export("firstRectForRange:")]
    public CGRect GetFirstRectForRange(UITextRange range) =>
        range is IndexedRange r ? FromGrid(_bridge.FirstRect(r.StartIndex, r.EndIndex)) : CGRect.Empty;

    /// <inheritdoc/>
    [Export("caretRectForPosition:")]
    public CGRect GetCaretRectForPosition(UITextPosition? position)
    {
        var rect = FromGrid(_bridge.CaretRect((position as IndexedPosition)?.Index ?? 0));
        return new CGRect(rect.X, rect.Y, Math.Max(rect.Width, 2), rect.Height);
    }

    /// <inheritdoc/>
    [Export("selectionRectsForRange:")]
    public UITextSelectionRect[] GetSelectionRects(UITextRange range) => Array.Empty<UITextSelectionRect>();

    /// <inheritdoc/>
    [Export("closestPositionToPoint:")]
    public UITextPosition GetClosestPositionToPoint(CGPoint point) =>
        new IndexedPosition(_bridge.ClosestPosition((float)ToGrid(point).X));

    /// <inheritdoc/>
    [Export("closestPositionToPoint:withinRange:")]
    public UITextPosition GetClosestPositionToPoint(CGPoint point, UITextRange withinRange)
    {
        int index = _bridge.ClosestPosition((float)ToGrid(point).X);
        if (withinRange is IndexedRange r) index = Math.Clamp(index, r.StartIndex, r.EndIndex);
        return new IndexedPosition(index);
    }

    /// <inheritdoc/>
    [Export("characterRangeAtPoint:")]
    public UITextRange GetCharacterRangeAtPoint(CGPoint point)
    {
        int index = _bridge.ClosestPosition((float)ToGrid(point).X);
        var range = _bridge.CharacterRangeByExtending(index, AppleTextInputBridge.Direction.Right);
        return new IndexedRange(range.Start, range.End);
    }

    // The bridge works in the grid's (the canvas's) device-independent coordinates; UIKit asks in this view's.
    private CGRect FromGrid(KumikoRect rect)
    {
        var r = new CGRect(rect.X, rect.Y, rect.Width, rect.Height);
        return _gridCoordinateSpace() is { } grid ? grid.ConvertRectToView(r, this) : r;
    }

    private CGPoint ToGrid(CGPoint point) =>
        _gridCoordinateSpace() is { } grid ? ConvertPointToView(point, grid) : point;

    // ── UITextInput: delegate and tokenizer ──────────────────────

    /// <inheritdoc/>
    public IUITextInputDelegate? InputDelegate
    {
        [Export("inputDelegate", ArgumentSemantic.Weak)]
        get;
        [Export("setInputDelegate:", ArgumentSemantic.Weak)]
        set;
    }

    /// <inheritdoc/>
    public IUITextInputTokenizer Tokenizer
    {
        [Export("tokenizer")]
        get => _tokenizer ??= new UITextInputStringTokenizer(this);
    }

    // Core changed the text, selection or composition (or an edit began or ended): UIKit's view is stale. The
    // keyboard type follows the editor, so a new session reloads the keyboard.
    private void OnBridgeInvalidated(bool sessionChanged)
    {
        var inputDelegate = InputDelegate;
        inputDelegate?.SelectionWillChange(this);
        inputDelegate?.TextWillChange(this);
        inputDelegate?.TextDidChange(this);
        inputDelegate?.SelectionDidChange(this);
        if (sessionChanged && IsFirstResponder)
            ReloadInputViews();
    }

    /// <summary>A text position: a UTF-16 index into the text.</summary>
    private sealed class IndexedPosition : UITextPosition
    {
        public IndexedPosition(int index) => Index = index;

        public int Index { get; }
    }

    /// <summary>A text range: UTF-16 indices, start before end.</summary>
    private sealed class IndexedRange : UITextRange
    {
        public IndexedRange(int start, int end)
        {
            StartIndex = start;
            EndIndex = end;
        }

        public int StartIndex { get; }

        public int EndIndex { get; }

        public override UITextPosition Start => new IndexedPosition(StartIndex);

        public override UITextPosition End => new IndexedPosition(EndIndex);

        public override bool IsEmpty => StartIndex == EndIndex;
    }
}
#endif
