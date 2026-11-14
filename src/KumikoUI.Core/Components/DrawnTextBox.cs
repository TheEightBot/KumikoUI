using System.Globalization;
using System.Text;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn single-line text input field: caret and selection on grapheme-cluster boundaries,
/// input-method composition (<see cref="ITextInputSession"/>), undo/redo, word and line selection by
/// double and triple click, drag selection, clipboard requests and horizontal scrolling.
/// </summary>
public sealed class DrawnTextBox : DrawnComponent, ITextInputSession, IValueEditor
{
    private const int MaxUndoDepth = 200;
    private const long MultiClickTimeMs = 500;
    private const float MultiClickSlop = 4f;

    private string _text = string.Empty;
    private int _anchor; // selection anchor; equal to _caret when nothing is selected
    private int _caret;
    private int _compositionStart = -1; // -1: no composition
    private int _compositionLength;
    private TextState _beforeComposition;
    private float _scrollOffset; // Horizontal scroll for long text
    private long _cursorBlinkStart;
    private bool _numericOnly;

    // Grapheme boundaries of _text and the measured width of the text before each one (NaN: not measured yet)
    private int[]? _boundaries;
    private float[]? _prefixWidths;

    // Paints reused by every frame (a render pass allocates nothing); the colors and font are set before each use
    private readonly KumikoPaint _bgPaint = new();
    private readonly KumikoPaint _borderPaint = new() { IsAntiAlias = true };
    private readonly KumikoPaint _textPaint = new();
    private readonly KumikoPaint _placeholderPaint = new();
    private readonly KumikoPaint _selectionPaint = new();
    private readonly KumikoPaint _underlinePaint = new() { StrokeWidth = 1f };
    private readonly KumikoPaint _cursorPaint = new() { StrokeWidth = 1.5f };
    private (string Family, float Size, bool Bold, bool Italic, IDrawingContext? Context) _widthKey;

    // Undo history: states before each edit; consecutive typing or deleting coalesce into one step
    private readonly List<TextState> _undo = new();
    private readonly List<TextState> _redo = new();
    private EditKind _lastEditKind;

    // Pointer gestures
    private bool _isDragging;
    private long _lastPressTime = long.MinValue;
    private float _lastPressX, _lastPressY;
    private int _pressCount;

    // Calls made by a host adapter through ITextInputSession don't raise TextInputStateChanged
    private int _adapterCallDepth;

    private readonly record struct TextState(string Text, int Anchor, int Caret);

    private readonly record struct Snapshot(string Text, int Anchor, int Caret, int CompositionStart, int CompositionLength);

    private enum EditKind { None, Typing, DeletingBackward, DeletingForward, Other }

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for text input and placeholder.</summary>
    public KumikoFont Font { get; set; } = new("Default", 13);
    /// <summary>Color of the entered text.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color when the text box is not focused.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color when the text box is not focused.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the text box has input focus.</summary>
    public KumikoColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color used to highlight the selected text range.</summary>
    public KumikoColor SelectionHighlightColor { get; set; } = new(0, 120, 215, 80);
    /// <summary>Color of the blinking text cursor.</summary>
    public KumikoColor CursorColor { get; set; } = new(0, 0, 0);
    /// <summary>Color of the placeholder text shown when the field is empty.</summary>
    public KumikoColor PlaceholderColor { get; set; } = new(160, 160, 160);
    /// <summary>Background fill color applied when the text box has input focus.</summary>
    public KumikoColor EditingBackgroundColor { get; set; } = new(232, 242, 254); // Light blue tint
    /// <summary>Horizontal padding inside the text box.</summary>
    public float Padding { get; set; } = 4f;
    /// <summary>Thickness of the border stroke.</summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>
    /// Controls how text is selected when the text box receives focus.
    /// Default: SelectAll.
    /// </summary>
    public EditTextSelectionMode InitialSelectionMode { get; set; } = EditTextSelectionMode.SelectAll;

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Sets the font of the text, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        InvalidateVisual();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the text box
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        ApplyFont(style.CellFont);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;
        SelectionHighlightColor = style.AccentColor.WithAlpha(80);
        CursorColor = txt;

        // Editing background: accent with very low alpha blended over bg
        EditingBackgroundColor = new KumikoColor(
            (byte)((style.AccentColor.R * 0.15) + (bg.R * 0.85)),
            (byte)((style.AccentColor.G * 0.15) + (bg.G * 0.85)),
            (byte)((style.AccentColor.B * 0.15) + (bg.B * 0.85)));

        // Placeholder: midpoint between text and background
        PlaceholderColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the text content. Setting it ends any composition, clears the undo history and keeps
    /// the caret where it was (moved into the new text).
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            var snap = Capture();
            var old = _text;
            SetTextCore(value ?? string.Empty);
            _compositionStart = -1;
            _compositionLength = 0;
            _caret = _anchor = TextElements.Floor(Boundaries, Math.Min(_caret, _text.Length));
            _undo.Clear();
            _redo.Clear();
            _lastEditKind = EditKind.None;
            if (old != _text)
                RaiseValueChanged(old, _text);
            if (StateDiffers(snap))
                RaiseStateChanged();
        }
    }

    /// <summary>Placeholder text shown when empty and unfocused.</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Restrict input to numeric characters: digits, '.', ',', '-' and the current culture's decimal and
    /// group separators and negative sign. Typed and pasted text is filtered to these characters.
    /// </summary>
    public bool NumericOnly
    {
        get => _numericOnly;
        set => _numericOnly = value;
    }

    /// <summary>Maximum length in UTF-16 code units (0 = unlimited). Inserted text is cut at a grapheme boundary.</summary>
    public int MaxLength { get; set; }

    /// <summary>Is the text read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Optional input validation function. Return true if the input is valid.
    /// Called on each insertion, paste and committed composition. If it returns false, the input is rejected.
    /// </summary>
    public Func<string, bool>? InputValidator { get; set; }

    /// <summary>
    /// Optional formatting function applied when the text box loses focus.
    /// Receives the current text and returns the formatted text.
    /// </summary>
    public Func<string, string>? OutputFormatter { get; set; }

    /// <summary>
    /// Optional input mask/pattern (e.g., "###-##-####" for SSN).
    /// '#' = digit, 'A' = letter, '*' = any. Other chars are literal.
    /// </summary>
    public string? InputMask { get; set; }

    /// <summary>
    /// Validation state. Set by the host or by InputValidator.
    /// </summary>
    public ValidationState Validation { get; set; } = ValidationState.None;

    /// <summary>Border color used when validation state is Error.</summary>
    public KumikoColor ErrorBorderColor { get; set; } = new(220, 50, 50);

    /// <summary>Border color used when validation state is Warning.</summary>
    public KumikoColor WarningBorderColor { get; set; } = new(230, 170, 0);

    /// <summary>Current caret position (UTF-16 index, always on a grapheme boundary).</summary>
    public int CursorPosition => _caret;

    /// <summary>The selection; empty at the caret when nothing is selected.</summary>
    public KumikoTextRange Selection => KumikoTextRange.FromBounds(_anchor, _caret);

    /// <summary>The composition (marked text) range, or null when no composition is active.</summary>
    public KumikoTextRange? Composition =>
        _compositionStart >= 0 ? new KumikoTextRange(_compositionStart, _compositionLength) : null;

    /// <summary>Is there an active text selection?</summary>
    public bool HasSelection => _anchor != _caret;

    /// <summary>Gets the selected text.</summary>
    public string SelectedText => HasSelection ? _text.Substring(Selection.Start, Selection.Length) : string.Empty;

    /// <summary>Whether <see cref="Undo"/> has a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether <see cref="Redo"/> has a step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when text changes via user input.</summary>
    public event EventHandler<KumikoTextChangedEventArgs>? TextChanged;

    /// <summary>
    /// Raised when Enter is pressed during editing (not while an input method is composing). The key itself
    /// is left unhandled, so a host (the grid's edit session) can commit the edit.
    /// </summary>
    public event EventHandler? Committed;

    /// <summary>
    /// Raised when Escape is pressed during editing (not while an input method is composing). The key itself
    /// is left unhandled, so a host (the grid's edit session) can cancel the edit.
    /// </summary>
    public event EventHandler? Cancelled;

    /// <summary>Raised when clipboard paste is requested (host must provide text).</summary>
    public event EventHandler<ClipboardRequestEventArgs>? PasteRequested;

    /// <summary>Raised when clipboard copy is requested (host should copy the text).</summary>
    public event EventHandler<ClipboardTextEventArgs>? CopyRequested;

    /// <inheritdoc />
    public event EventHandler? TextInputStateChanged;

    // ── Text measurement ────────────────────────────────────────

    private IDrawingContext? _measureContext;

    /// <summary>Set a drawing context for text measurement. Call before drawing.</summary>
    public void SetMeasureContext(IDrawingContext ctx) => _measureContext = ctx;

    private int[] Boundaries => _boundaries ??= TextElements.GetBoundaries(_text);

    private void SetTextCore(string text)
    {
        if (ReferenceEquals(text, _text) || text == _text) { _text = text; return; }
        _text = text;
        _boundaries = null;
        _prefixWidths = null;
    }

    /// <summary>The width of the text before boundary number <paramref name="k"/>, measured once per text.</summary>
    private float PrefixWidth(int k)
    {
        var b = Boundaries;
        if (_measureContext == null || k <= 0) return 0;
        var key = (Font.Family, Font.Size, Font.IsBold, Font.IsItalic, _measureContext);
        if (_prefixWidths == null || _prefixWidths.Length != b.Length || _widthKey != key)
        {
            _prefixWidths = new float[b.Length];
            Array.Fill(_prefixWidths, float.NaN);
            _widthKey = key;
        }
        if (float.IsNaN(_prefixWidths[k]))
            _prefixWidths[k] = _measureContext.MeasureText(_text.Substring(0, b[k]), new KumikoPaint { Font = Font }).Width;
        return _prefixWidths[k];
    }

    /// <summary>The x offset (from the text start) of an index.</summary>
    private float XAt(int index)
    {
        if (_measureContext == null || index <= 0) return 0;
        int k = Array.BinarySearch(Boundaries, Math.Min(index, _text.Length));
        if (k >= 0) return PrefixWidth(k);
        return _measureContext.MeasureText(_text.Substring(0, Math.Min(index, _text.Length)), new KumikoPaint { Font = Font }).Width;
    }

    // ── Geometry for text-input adapters ────────────────────────

    /// <summary>
    /// The area a range of the text occupies, in the coordinates of <see cref="DrawnComponent.Bounds"/>: from the
    /// left edge of its first character to the right edge of its last, as tall as the line of text. An empty range
    /// gives the caret's area, with no width. The positions follow the horizontal scroll and stay inside the text
    /// area. A host's text-input adapter places the input method's candidate window with it (UITextInput
    /// <c>firstRectForRange:</c> and <c>caretRectForPosition:</c>).
    /// </summary>
    /// <param name="range">The range, in UTF-16 code units; clamped to the text.</param>
    /// <returns>The area of the range; before the box is first measured, a caret at the start of the text area.</returns>
    public KumikoRect GetTextRangeBounds(KumikoTextRange range)
    {
        var b = Bounds;
        float innerLeft = b.X + Padding;
        float innerRight = Math.Max(innerLeft, b.Right - Padding);
        int start = Math.Clamp(range.Start, 0, _text.Length);
        int end = Math.Clamp(range.End, start, _text.Length);
        float left = Math.Clamp(innerLeft + XAt(start) - _scrollOffset, innerLeft, innerRight);
        float right = Math.Clamp(innerLeft + XAt(end) - _scrollOffset, left, innerRight);

        float top = b.Y, height = b.Height;
        if (_measureContext != null)
        {
            _textPaint.Font = Font;
            float textHeight = _measureContext.GetFontMetrics(_textPaint).TextHeight;
            if (textHeight > 0 && textHeight < b.Height)
            {
                top = b.Y + (b.Height - textHeight) / 2;
                height = textHeight;
            }
        }
        return new KumikoRect(left, top, right - left, height);
    }

    /// <summary>
    /// The text index nearest to an x position in the coordinates of <see cref="DrawnComponent.Bounds"/>, following
    /// the horizontal scroll. The index is on a grapheme-cluster boundary; 0 before the box is first measured.
    /// </summary>
    /// <param name="x">The x position.</param>
    /// <returns>An index from 0 to the text's length, in UTF-16 code units.</returns>
    public int GetTextIndexAt(float x) => GetIndexAtX(x);

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        _measureContext = ctx;
        EnsureCursorVisible();

        var b = Bounds;
        var borderColor = Validation switch
        {
            ValidationState.Error => ErrorBorderColor,
            ValidationState.Warning => WarningBorderColor,
            _ => IsFocused ? FocusedBorderColor : BorderColor
        };

        // Focused editing background — light blue tint to clearly indicate edit mode
        var bgColor = IsFocused ? EditingBackgroundColor : BackgroundColor;

        // Background
        _bgPaint.Color = bgColor;
        ctx.FillRect(b, _bgPaint);

        // Border — thicker when focused
        float bw = IsFocused ? BorderWidth + 1f : BorderWidth;
        if (Validation == ValidationState.Error) bw = BorderWidth + 1.5f;
        _borderPaint.Color = borderColor;
        _borderPaint.StrokeWidth = bw;
        ctx.DrawRect(b, _borderPaint);

        // Clip to inner area
        float innerX = b.X + Padding;
        float innerY = b.Y;
        float innerW = b.Width - Padding * 2;
        float innerH = b.Height;

        ctx.Save();
        ctx.ClipRect(new KumikoRect(innerX, innerY, innerW, innerH));
        ctx.Translate(-_scrollOffset, 0);

        // Compute properly centered text baseline using font metrics
        var textPaint = _textPaint;
        textPaint.Color = TextColor;
        textPaint.Font = Font;
        var fontMetrics = ctx.GetFontMetrics(textPaint);
        float textHeight = fontMetrics.TextHeight;
        float textBaseline = b.Y + (b.Height - textHeight) / 2 - fontMetrics.Ascent;

        // Selection highlight
        if (IsFocused && HasSelection)
        {
            float startX = innerX + XAt(Selection.Start);
            float endX = innerX + XAt(Selection.End);
            _selectionPaint.Color = SelectionHighlightColor;
            ctx.FillRect(new KumikoRect(startX, b.Y + 2, endX - startX, b.Height - 4), _selectionPaint);
        }

        // Text or placeholder
        if (_text.Length > 0)
        {
            ctx.DrawText(_text, innerX, textBaseline, textPaint);
        }
        else if (!IsFocused && !string.IsNullOrEmpty(Placeholder))
        {
            var phPaint = _placeholderPaint;
            phPaint.Color = PlaceholderColor;
            phPaint.Font = Font;
            ctx.DrawText(Placeholder, innerX, textBaseline, phPaint);
        }

        // Composition (marked text) underline
        if (_compositionStart >= 0 && _compositionLength > 0)
        {
            float startX = innerX + XAt(_compositionStart);
            float endX = innerX + XAt(_compositionStart + _compositionLength);
            float underlineY = textBaseline + Math.Max(1f, fontMetrics.Descent / 2);
            _underlinePaint.Color = TextColor;
            ctx.DrawLine(startX, underlineY, endX, underlineY, _underlinePaint);
        }

        // Cursor
        if (IsFocused && !IsReadOnly)
        {
            DrawCursor(ctx, innerX, b.Y, b.Height, fontMetrics);
        }

        ctx.Restore();
    }

    private void DrawCursor(IDrawingContext ctx, float textX, float y, float height, KumikoFontMetrics fontMetrics)
    {
        // Blink every 530ms — but always visible for first 530ms after reset
        long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _cursorBlinkStart;
        if ((elapsed / 530) % 2 != 0) return;

        float cursorX = textX + XAt(_caret);

        // Align cursor to the text area using font metrics
        float textHeight = fontMetrics.TextHeight;
        float textTop = y + (height - textHeight) / 2;
        float cursorTop = textTop + 1f;
        float cursorBottom = textTop + textHeight - 1f;

        _cursorPaint.Color = CursorColor;
        ctx.DrawLine(cursorX, cursorTop, cursorX, cursorBottom, _cursorPaint);
    }

    // ── Focus ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnGotFocus()
    {
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (InitialSelectionMode == EditTextSelectionMode.CursorAtEnd)
            SetSelectionCore(_text.Length, _text.Length);
        else
            SelectAll();
    }

    /// <inheritdoc />
    protected override void OnLostFocus()
    {
        CommitComposition();
        _isDragging = false;
        ClearSelection();

        // Apply output formatting on focus loss
        if (OutputFormatter != null && _text.Length > 0)
        {
            var formatted = OutputFormatter(_text);
            if (formatted != _text)
            {
                var snap = Capture();
                RecordUndo(new TextState(_text, _anchor, _caret), EditKind.Other, forceNewStep: true);
                SetTextCore(formatted);
                _caret = _anchor = Math.Min(_caret, _text.Length);
                _caret = _anchor = TextElements.Floor(Boundaries, _caret);
                Finish(snap);
            }
        }
    }

    // ── Pointer input ───────────────────────────────────────────

    /// <summary>
    /// Press: place the caret (Shift extends the selection) and start a drag selection; a double click
    /// selects the word, a triple click selects all.
    /// </summary>
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsFocused || IsReadOnly) return false;

        CommitComposition();
        int clicks = CountClicks(e);
        int index = GetIndexAtX(e.X);
        var snap = Capture();
        _lastEditKind = EditKind.None;

        if (clicks >= 3)
        {
            _anchor = 0;
            _caret = _text.Length;
            _isDragging = false;
        }
        else if (clicks == 2)
        {
            var (start, end) = WordAt(index);
            _anchor = start;
            _caret = end;
            _isDragging = false;
        }
        else
        {
            if ((e.Modifiers & InputModifiers.Shift) == 0)
                _anchor = index;
            _caret = index;
            _isDragging = true;
        }

        Finish(snap);
        return true;
    }

    /// <summary>Drag: extend the selection to the pointer, scrolling when it is past either edge.</summary>
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!IsFocused || !_isDragging) return false;

        var snap = Capture();
        _caret = GetIndexAtX(e.X);
        Finish(snap);
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (!_isDragging) return false;
        _isDragging = false;
        return true;
    }

    private int CountClicks(KumikoPointerEventArgs e)
    {
        long now = e.TimestampMs > 0 ? e.TimestampMs : Environment.TickCount64;
        bool repeat = now - _lastPressTime <= MultiClickTimeMs &&
                      Math.Abs(e.X - _lastPressX) <= MultiClickSlop &&
                      Math.Abs(e.Y - _lastPressY) <= MultiClickSlop;
        _pressCount = repeat ? Math.Min(_pressCount + 1, 3) : 1;
        _lastPressTime = now;
        _lastPressX = e.X;
        _lastPressY = e.Y;
        return Math.Max(_pressCount, Math.Min(e.ClickCount, 3));
    }

    /// <summary>The grapheme boundary nearest to a viewport x (binary search over measured prefixes).</summary>
    private int GetIndexAtX(float screenX)
    {
        if (_measureContext == null || _text.Length == 0) return 0;

        float localX = screenX - Bounds.X - Padding + _scrollOffset;
        if (localX <= 0) return 0;

        var b = Boundaries;
        int last = b.Length - 1;
        if (localX >= PrefixWidth(last)) return _text.Length;

        // Last boundary whose prefix ends at or before localX
        int lo = 0, hi = last;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (PrefixWidth(mid) <= localX) lo = mid;
            else hi = mid - 1;
        }

        float left = PrefixWidth(lo);
        float right = PrefixWidth(lo + 1);
        return localX - left < right - localX ? b[lo] : b[lo + 1];
    }

    // ── Keyboard input ──────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (!IsFocused) return false;

        bool command = e.HasCommand;
        bool shift = e.HasShift;

        // Enter and Escape finish or abandon a composition before they mean commit or cancel
        if (_compositionStart >= 0)
        {
            if (e.Key == KumikoKey.Enter) { CommitComposition(); return true; }
            if (e.Key == KumikoKey.Escape) { CancelComposition(); return true; }
        }

        switch (e.Key)
        {
            case KumikoKey.Left:
                MoveCursor(command ? PreviousWordBoundary(_caret)
                    : HasSelection && !shift ? Selection.Start
                    : TextElements.Previous(Boundaries, _caret), shift);
                return true;

            case KumikoKey.Right:
                MoveCursor(command ? NextWordBoundary(_caret)
                    : HasSelection && !shift ? Selection.End
                    : TextElements.Next(Boundaries, _caret), shift);
                return true;

            case KumikoKey.Home:
                MoveCursor(0, shift);
                return true;

            case KumikoKey.End:
                MoveCursor(_text.Length, shift);
                return true;

            case KumikoKey.Backspace:
                if (command) DeleteWordBackward();
                else DeleteBackward();
                return true;

            case KumikoKey.Delete:
                if (command) DeleteWordForward();
                else DeleteForward();
                return true;

            // A single-line box has no use for Enter and Escape: they are reported and left to the host
            case KumikoKey.Enter:
                Committed?.Invoke(this, EventArgs.Empty);
                return false;

            case KumikoKey.Escape:
                Cancelled?.Invoke(this, EventArgs.Empty);
                return false;

            case KumikoKey.A when command:
                SelectAll();
                return true;

            case KumikoKey.C when command:
                HandleCopy();
                return true;

            case KumikoKey.V when command:
                HandlePaste();
                return true;

            case KumikoKey.X when command:
                HandleCut();
                return true;

            case KumikoKey.Z when command:
                if (shift) Redo(); else Undo();
                return true;

            case KumikoKey.Y when command:
                Redo();
                return true;

            case KumikoKey.A or KumikoKey.C or KumikoKey.V or KumikoKey.X or KumikoKey.Z or KumikoKey.Y:
                TypeText(e);
                return true;

            default:
                return TypeText(e);
        }
    }

    private bool TypeText(KumikoKeyEventArgs e)
    {
        var text = e.EnteredText;
        if (text == null) return false;
        if (!IsReadOnly) InsertText(text);
        return true;
    }

    // ── Editing ─────────────────────────────────────────────────

    /// <summary>
    /// Insert text at the caret, replacing the composition if one is active, otherwise the selection.
    /// Line breaks become spaces, control characters are dropped, and <see cref="NumericOnly"/>,
    /// <see cref="InputMask"/>, <see cref="InputValidator"/> and <see cref="MaxLength"/> apply; rejected
    /// input leaves the text unchanged.
    /// </summary>
    public void InsertText(string text)
    {
        if (IsReadOnly || text == null) return;
        var snap = Capture();

        if (_compositionStart >= 0)
        {
            RestoreBeforeComposition();
            TryReplaceSelection(text, EditKind.Other, forceNewStep: true);
            _lastEditKind = EditKind.None;
        }
        else
        {
            if (TryReplaceSelection(text, EditKind.Typing, forceNewStep: false) && text.Length > 0 && char.IsWhiteSpace(text[^1]))
                _lastEditKind = EditKind.None; // a word ends: the next typing is a new undo step
        }

        Finish(snap);
    }

    /// <inheritdoc />
    public void SetComposition(string text, int cursorInComposition)
    {
        if (IsReadOnly) return;
        text = SanitizeLine(text ?? string.Empty);
        if (text.Length == 0)
        {
            CancelComposition();
            return;
        }

        var snap = Capture();
        int start, length;
        if (_compositionStart >= 0)
        {
            start = _compositionStart;
            length = _compositionLength;
        }
        else
        {
            _beforeComposition = new TextState(_text, _anchor, _caret);
            start = Selection.Start;
            length = Selection.Length;
        }

        SetTextCore(_text.Remove(start, length).Insert(start, text));
        _compositionStart = start;
        _compositionLength = text.Length;
        _caret = _anchor = start + Math.Clamp(cursorInComposition, 0, text.Length);
        Finish(snap);
    }

    /// <inheritdoc />
    public void CommitComposition()
    {
        if (_compositionStart < 0) return;
        var snap = Capture();
        string composed = _text.Substring(_compositionStart, _compositionLength);
        RestoreBeforeComposition();
        if (!IsReadOnly)
            TryReplaceSelection(composed, EditKind.Other, forceNewStep: true);
        _lastEditKind = EditKind.None;
        Finish(snap);
    }

    /// <inheritdoc />
    public void CancelComposition()
    {
        if (_compositionStart < 0) return;
        var snap = Capture();
        RestoreBeforeComposition();
        Finish(snap);
    }

    /// <inheritdoc />
    public void DeleteBackward()
    {
        if (IsReadOnly) return;
        if (_compositionStart >= 0) { DeleteInComposition(backward: true); return; }
        var snap = Capture();
        if (HasSelection) DeleteRange(Selection.Start, Selection.Length, EditKind.Other);
        else if (_caret > 0)
        {
            int previous = TextElements.Previous(Boundaries, _caret);
            DeleteRange(previous, _caret - previous, EditKind.DeletingBackward);
        }
        Finish(snap);
    }

    /// <inheritdoc />
    public void DeleteForward()
    {
        if (IsReadOnly) return;
        if (_compositionStart >= 0) { DeleteInComposition(backward: false); return; }
        var snap = Capture();
        if (HasSelection) DeleteRange(Selection.Start, Selection.Length, EditKind.Other);
        else if (_caret < _text.Length)
        {
            int next = TextElements.Next(Boundaries, _caret);
            DeleteRange(_caret, next - _caret, EditKind.DeletingForward);
        }
        Finish(snap);
    }

    private void DeleteWordBackward()
    {
        if (IsReadOnly) return;
        CommitComposition();
        var snap = Capture();
        if (HasSelection) DeleteRange(Selection.Start, Selection.Length, EditKind.Other);
        else if (_caret > 0)
        {
            int start = PreviousWordBoundary(_caret);
            DeleteRange(start, _caret - start, EditKind.Other);
        }
        Finish(snap);
    }

    private void DeleteWordForward()
    {
        if (IsReadOnly) return;
        CommitComposition();
        var snap = Capture();
        if (HasSelection) DeleteRange(Selection.Start, Selection.Length, EditKind.Other);
        else if (_caret < _text.Length)
        {
            int end = NextWordBoundary(_caret);
            DeleteRange(_caret, end - _caret, EditKind.Other);
        }
        Finish(snap);
    }

    /// <summary>Delete one grapheme of the composition next to the caret; an emptied composition is canceled.</summary>
    private void DeleteInComposition(bool backward)
    {
        string composed = _text.Substring(_compositionStart, _compositionLength);
        int caret = Math.Clamp(_caret - _compositionStart, 0, composed.Length);
        var b = TextElements.GetBoundaries(composed);
        int from = backward ? TextElements.Previous(b, caret) : caret;
        int to = backward ? caret : TextElements.Next(b, caret);
        if (from == to) return;
        string remaining = composed.Remove(from, to - from);
        if (remaining.Length == 0) CancelComposition();
        else SetComposition(remaining, from);
    }

    /// <summary>Undo the last edit (a run of typing or deleting is one step).</summary>
    public void Undo()
    {
        CancelComposition();
        if (_undo.Count == 0) return;
        var snap = Capture();
        _redo.Add(new TextState(_text, _anchor, _caret));
        var state = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        RestoreState(state);
        Finish(snap);
    }

    /// <summary>Redo the last undone edit.</summary>
    public void Redo()
    {
        CancelComposition();
        if (_redo.Count == 0) return;
        var snap = Capture();
        _undo.Add(new TextState(_text, _anchor, _caret));
        var state = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        RestoreState(state);
        Finish(snap);
    }

    /// <summary>
    /// Replace the selection with <paramref name="requested"/> as this box accepts it (single-line, numeric
    /// filter, maximum length), if the input rules accept the result. Input that filters down to nothing
    /// changes nothing (an explicitly empty string deletes the selection). No events.
    /// </summary>
    private bool TryReplaceSelection(string requested, EditKind kind, bool forceNewStep)
    {
        int start = Selection.Start, length = Selection.Length;
        string remaining = _text.Remove(start, length);
        string text = FitMaxLength(remaining.Length, Sanitize(requested));
        if (text.Length == 0 && (length == 0 || requested.Length > 0)) return false;

        string candidate = remaining.Insert(start, text);
        if (!Accepts(candidate)) return false;

        RecordUndo(new TextState(_text, _anchor, _caret), kind, forceNewStep || length > 0);
        SetTextCore(candidate);
        _caret = _anchor = start + text.Length;
        return true;
    }

    private void DeleteRange(int start, int length, EditKind kind)
    {
        if (length <= 0) return;
        RecordUndo(new TextState(_text, _anchor, _caret), kind, forceNewStep: false);
        SetTextCore(_text.Remove(start, length));
        _caret = _anchor = start;
    }

    private bool Accepts(string candidate)
    {
        if (InputMask != null && !ValidateAgainstMask(candidate))
            return false;

        if (InputValidator != null)
        {
            if (!InputValidator(candidate))
            {
                Validation = ValidationState.Error;
                InvalidateVisual();
                return false;
            }
            Validation = ValidationState.Valid;
        }
        return true;
    }

    /// <summary>The part of <paramref name="text"/> that fits <see cref="MaxLength"/>, cut at a grapheme boundary.</summary>
    private string FitMaxLength(int remainingLength, string text)
    {
        if (MaxLength <= 0) return text;
        int available = MaxLength - remainingLength;
        if (available <= 0) return string.Empty;
        if (text.Length <= available) return text;
        return text.Substring(0, TextElements.Floor(TextElements.GetBoundaries(text), available));
    }

    /// <summary>Single-line text: line breaks and tabs become spaces, other control characters are dropped.</summary>
    private static string SanitizeLine(string text)
    {
        if (text.Length == 0) return text;
        StringBuilder? sb = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!char.IsControl(c))
            {
                sb?.Append(c);
                continue;
            }

            sb ??= new StringBuilder(text, 0, i, text.Length);
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            if (c is '\r' or '\n' or '\t') sb.Append(' ');
        }
        return sb?.ToString() ?? text;
    }

    /// <summary>Text as this box accepts it: single-line, and filtered to numeric characters when <see cref="NumericOnly"/>.</summary>
    private string Sanitize(string text)
    {
        text = SanitizeLine(text);
        if (!_numericOnly || text.Length == 0) return text;

        var format = CultureInfo.CurrentCulture.NumberFormat;
        bool IsNumeric(char c) =>
            char.IsDigit(c) || c is '.' or '-' or ',' ||
            format.NumberDecimalSeparator.Contains(c) ||
            format.NumberGroupSeparator.Contains(c) ||
            format.NegativeSign.Contains(c);

        return string.Concat(text.Where(IsNumeric));
    }

    /// <summary>Validate text against the InputMask pattern.</summary>
    private bool ValidateAgainstMask(string text)
    {
        if (InputMask == null) return true;

        // Allow partial input (text can be shorter than mask)
        for (int i = 0; i < text.Length; i++)
        {
            if (i >= InputMask.Length) return false;

            char maskChar = InputMask[i];
            char textChar = text[i];

            switch (maskChar)
            {
                case '#': // Digit only
                    if (!char.IsDigit(textChar)) return false;
                    break;
                case 'A': // Letter only
                    if (!char.IsLetter(textChar)) return false;
                    break;
                case '*': // Any character
                    break;
                default: // Literal match
                    if (textChar != maskChar) return false;
                    break;
            }
        }

        return true;
    }

    private void RecordUndo(TextState state, EditKind kind, bool forceNewStep)
    {
        bool coalesce = !forceNewStep && _undo.Count > 0 && kind == _lastEditKind &&
                        kind is EditKind.Typing or EditKind.DeletingBackward or EditKind.DeletingForward;
        if (!coalesce)
        {
            _undo.Add(state);
            if (_undo.Count > MaxUndoDepth) _undo.RemoveAt(0);
        }
        _redo.Clear();
        _lastEditKind = kind;
    }

    private void RestoreState(TextState state)
    {
        SetTextCore(state.Text);
        _anchor = Math.Min(state.Anchor, _text.Length);
        _caret = Math.Min(state.Caret, _text.Length);
        _lastEditKind = EditKind.None;
    }

    /// <summary>Put back the text and selection from before the composition and end it. No events.</summary>
    private void RestoreBeforeComposition()
    {
        if (_compositionStart < 0) return;
        _compositionStart = -1;
        _compositionLength = 0;
        SetTextCore(_beforeComposition.Text);
        _anchor = Math.Min(_beforeComposition.Anchor, _text.Length);
        _caret = Math.Min(_beforeComposition.Caret, _text.Length);
    }

    // ── Selection ───────────────────────────────────────────────

    /// <summary>Select all text.</summary>
    public void SelectAll()
    {
        CommitComposition();
        var snap = Capture();
        _anchor = 0;
        _caret = _text.Length;
        _lastEditKind = EditKind.None;
        Finish(snap);
    }

    /// <summary>Clear the selection, leaving the caret where it is.</summary>
    public void ClearSelection()
    {
        var snap = Capture();
        _anchor = _caret;
        Finish(snap);
    }

    /// <inheritdoc />
    public void SetSelection(int anchor, int active)
    {
        var range = KumikoTextRange.FromBounds(anchor, active);
        if (_compositionStart >= 0 &&
            (range.Start < _compositionStart || range.End > _compositionStart + _compositionLength))
            CommitComposition(); // a selection outside the marked text ends the composition

        SetSelectionCore(anchor, active);
    }

    private void SetSelectionCore(int anchor, int active)
    {
        var snap = Capture();
        var b = Boundaries;
        _anchor = TextElements.Floor(b, Math.Clamp(anchor, 0, _text.Length));
        _caret = TextElements.Floor(b, Math.Clamp(active, 0, _text.Length));
        _lastEditKind = EditKind.None;
        Finish(snap);
    }

    private void MoveCursor(int newPosition, bool extendSelection)
    {
        CommitComposition();
        var snap = Capture();
        newPosition = TextElements.Floor(Boundaries, Math.Clamp(newPosition, 0, _text.Length));
        if (!extendSelection) _anchor = newPosition;
        _caret = newPosition;
        _lastEditKind = EditKind.None;
        Finish(snap);
    }

    // ── Words ───────────────────────────────────────────────────

    private enum CharClass { Word, Space, Other }

    private CharClass ClassAt(int index)
    {
        char c = _text[index];
        if (char.IsWhiteSpace(c)) return CharClass.Space;
        if (char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) ||
            CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            return CharClass.Word;
        return CharClass.Other;
    }

    /// <summary>The run of graphemes of one class (word, whitespace, punctuation) around an index.</summary>
    private (int Start, int End) WordAt(int index)
    {
        if (_text.Length == 0) return (0, 0);
        var b = Boundaries;
        int start = index >= _text.Length ? TextElements.Previous(b, _text.Length) : TextElements.Floor(b, index);
        var cls = ClassAt(start);
        int end = TextElements.Next(b, start);
        while (start > 0)
        {
            int previous = TextElements.Previous(b, start);
            if (ClassAt(previous) != cls) break;
            start = previous;
        }
        while (end < _text.Length && ClassAt(end) == cls)
            end = TextElements.Next(b, end);
        return (start, end);
    }

    private int PreviousWordBoundary(int index)
    {
        var b = Boundaries;
        int pos = index;
        while (pos > 0 && ClassAt(TextElements.Previous(b, pos)) == CharClass.Space)
            pos = TextElements.Previous(b, pos);
        if (pos == 0) return 0;
        var cls = ClassAt(TextElements.Previous(b, pos));
        while (pos > 0 && ClassAt(TextElements.Previous(b, pos)) == cls)
            pos = TextElements.Previous(b, pos);
        return pos;
    }

    private int NextWordBoundary(int index)
    {
        var b = Boundaries;
        int pos = index;
        if (pos < _text.Length && ClassAt(pos) != CharClass.Space)
        {
            var cls = ClassAt(pos);
            while (pos < _text.Length && ClassAt(pos) == cls)
                pos = TextElements.Next(b, pos);
        }
        while (pos < _text.Length && ClassAt(pos) == CharClass.Space)
            pos = TextElements.Next(b, pos);
        return pos;
    }

    // ── Scrolling ───────────────────────────────────────────────

    private void EnsureCursorVisible()
    {
        if (_measureContext == null) return;

        float cursorX = XAt(_caret);
        float visibleWidth = Math.Max(0, Bounds.Width - Padding * 2);
        float textWidth = XAt(_text.Length);

        if (cursorX - _scrollOffset > visibleWidth - 1)
            _scrollOffset = cursorX - visibleWidth + 2; // room for the caret
        else if (cursorX - _scrollOffset < 0)
            _scrollOffset = cursorX;

        // Don't leave empty space after the text once it is shorter
        if (_scrollOffset > 0 && textWidth - _scrollOffset < visibleWidth - 2)
            _scrollOffset = Math.Max(0, textWidth - visibleWidth + 2);

        if (_scrollOffset < 0) _scrollOffset = 0;
    }

    // ── Clipboard ───────────────────────────────────────────────

    private void HandleCopy()
    {
        if (!HasSelection) return;
        CopyRequested?.Invoke(this, new ClipboardTextEventArgs(SelectedText));
    }

    private void HandleCut()
    {
        if (!HasSelection) return;
        // Nothing is deleted unless a host took the text: cutting must never destroy it
        var copy = new ClipboardTextEventArgs(SelectedText);
        CopyRequested?.Invoke(this, copy);
        if (!copy.Handled || IsReadOnly) return;
        var snap = Capture();
        DeleteRange(Selection.Start, Selection.Length, EditKind.Other);
        Finish(snap);
    }

    private void HandlePaste()
    {
        if (IsReadOnly) return;
        var args = new ClipboardRequestEventArgs();
        PasteRequested?.Invoke(this, args);
        if (args.IsPending) return;
        PasteText(args.Text);
    }

    /// <summary>
    /// Replaces the selection with <paramref name="text"/> as one undo step, applying the box's input rules
    /// (single line, numeric only, maximum length). For a host that reads the clipboard asynchronously (see
    /// <see cref="ClipboardRequestEventArgs.IsPending"/>). Does nothing when the box is read-only or the text
    /// is empty.
    /// </summary>
    /// <param name="text">The text to paste.</param>
    public void PasteText(string? text)
    {
        if (IsReadOnly || string.IsNullOrEmpty(text)) return;

        CommitComposition();
        var snap = Capture();
        TryReplaceSelection(text, EditKind.Other, forceNewStep: true);
        Finish(snap);
    }

    // ── Change notification ─────────────────────────────────────

    private Snapshot Capture() => new(_text, _anchor, _caret, _compositionStart, _compositionLength);

    private bool StateDiffers(Snapshot snap) =>
        snap.Anchor != _anchor || snap.Caret != _caret || snap.CompositionStart != _compositionStart ||
        snap.CompositionLength != _compositionLength || !ReferenceEquals(snap.Text, _text) && snap.Text != _text;

    /// <summary>After a change: scroll the caret into view, restart its blink, raise the events, redraw.</summary>
    private void Finish(Snapshot snap)
    {
        if (!StateDiffers(snap))
        {
            InvalidateVisual(); // validation state may have changed
            return;
        }

        EnsureCursorVisible();
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (snap.Text != _text)
        {
            TextChanged?.Invoke(this, new KumikoTextChangedEventArgs(snap.Text, _text));
            RaiseValueChanged(snap.Text, _text);
        }

        RaiseStateChanged();
        InvalidateVisual();
    }

    private void RaiseStateChanged()
    {
        if (_adapterCallDepth == 0)
            TextInputStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── ITextInputSession (adapter calls don't raise TextInputStateChanged) ──

    void ITextInputSession.InsertText(string text) { _adapterCallDepth++; try { InsertText(text); } finally { _adapterCallDepth--; } }

    void ITextInputSession.SetComposition(string text, int cursorInComposition) { _adapterCallDepth++; try { SetComposition(text, cursorInComposition); } finally { _adapterCallDepth--; } }

    void ITextInputSession.CommitComposition() { _adapterCallDepth++; try { CommitComposition(); } finally { _adapterCallDepth--; } }

    void ITextInputSession.CancelComposition() { _adapterCallDepth++; try { CancelComposition(); } finally { _adapterCallDepth--; } }

    void ITextInputSession.DeleteBackward() { _adapterCallDepth++; try { DeleteBackward(); } finally { _adapterCallDepth--; } }

    void ITextInputSession.DeleteForward() { _adapterCallDepth++; try { DeleteForward(); } finally { _adapterCallDepth--; } }

    void ITextInputSession.SetSelection(int anchor, int active) { _adapterCallDepth++; try { SetSelection(anchor, active); } finally { _adapterCallDepth--; } }

    // ── IValueEditor ────────────────────────────────────────────

    object? IValueEditor.Value => _text;

    bool IValueEditor.HasValue => true;

    bool IValueEditor.IsDirty => HasChangedSinceTracking;
}
