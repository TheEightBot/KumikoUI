using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn single-line text input field.
/// Supports cursor, text selection, copy/paste, and scrolling.
/// </summary>
public class DrawnTextBox : DrawnComponent
{
    private string _text = string.Empty;
    private int _cursorPosition;
    private int _selectionStart = -1; // -1 = no selection
    private int _selectionEnd = -1;
    private float _scrollOffset; // Horizontal scroll for long text
    private long _cursorBlinkStart;
    private bool _numericOnly;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for text input and placeholder.</summary>
    public GridFont Font { get; set; } = new("Default", 13);
    /// <summary>Color of the entered text.</summary>
    public GridColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color when the text box is not focused.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Border color when the text box is not focused.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the text box has input focus.</summary>
    public GridColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color used to highlight the selected text range.</summary>
    public GridColor SelectionHighlightColor { get; set; } = new(0, 120, 215, 80);
    /// <summary>Color of the blinking text cursor.</summary>
    public GridColor CursorColor { get; set; } = new(0, 0, 0);
    /// <summary>Color of the placeholder text shown when the field is empty.</summary>
    public GridColor PlaceholderColor { get; set; } = new(160, 160, 160);
    /// <summary>Background fill color applied when the text box has input focus.</summary>
    public GridColor EditingBackgroundColor { get; set; } = new(232, 242, 254); // Light blue tint
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
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the text box
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
    {
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;
        SelectionHighlightColor = style.AccentColor.WithAlpha(80);
        CursorColor = txt;

        // Editing background: accent with very low alpha blended over bg
        EditingBackgroundColor = new GridColor(
            (byte)((style.AccentColor.R * 0.15) + (bg.R * 0.85)),
            (byte)((style.AccentColor.G * 0.15) + (bg.G * 0.85)),
            (byte)((style.AccentColor.B * 0.15) + (bg.B * 0.85)));

        // Placeholder: midpoint between text and background
        PlaceholderColor = new GridColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Gets or sets the text content.</summary>
    public string Text
    {
        get => _text;
        set
        {
            var old = _text;
            _text = value ?? string.Empty;
            _cursorPosition = Math.Min(_cursorPosition, _text.Length);
            ClearSelection();
            if (old != _text)
                RaiseValueChanged(old, _text);
        }
    }

    /// <summary>Placeholder text shown when empty and unfocused.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Restrict input to numeric characters only.</summary>
    public bool NumericOnly
    {
        get => _numericOnly;
        set => _numericOnly = value;
    }

    /// <summary>Maximum character length (0 = unlimited).</summary>
    public int MaxLength { get; set; }

    /// <summary>Is the text read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Optional input validation function. Return true if the input is valid.
    /// Called on each character insertion and paste. If it returns false, the input is rejected.
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

    /// <summary>Error message shown when validation fails.</summary>
    public string? ValidationMessage { get; set; }

    /// <summary>Border color used when validation state is Error.</summary>
    public GridColor ErrorBorderColor { get; set; } = new(220, 50, 50);

    /// <summary>Border color used when validation state is Warning.</summary>
    public GridColor WarningBorderColor { get; set; } = new(230, 170, 0);

    /// <summary>Current cursor position (character index).</summary>
    public int CursorPosition => _cursorPosition;

    /// <summary>Is there an active text selection?</summary>
    public bool HasSelection => _selectionStart >= 0 && _selectionEnd >= 0 && _selectionStart != _selectionEnd;

    /// <summary>Gets the selected text.</summary>
    public string SelectedText
    {
        get
        {
            if (!HasSelection) return string.Empty;
            int start = Math.Min(_selectionStart, _selectionEnd);
            int end = Math.Max(_selectionStart, _selectionEnd);
            return _text.Substring(start, end - start);
        }
    }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when text changes via user input.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    /// <summary>Raised when Enter is pressed during editing.</summary>
    public event Action? Committed;

    /// <summary>Raised when Escape is pressed during editing.</summary>
    public event Action? Cancelled;

    /// <summary>Raised when clipboard paste is requested (host must provide text).</summary>
    public event EventHandler<ClipboardRequestEventArgs>? PasteRequested;

    /// <summary>Raised when clipboard copy is requested (host should copy the text).</summary>
    public event EventHandler<ClipboardTextEventArgs>? CopyRequested;

    // ── Text measurement cache ──────────────────────────────────

    private IDrawingContext? _measureContext;

    /// <summary>Set a drawing context for text measurement. Call before drawing.</summary>
    public void SetMeasureContext(IDrawingContext ctx) => _measureContext = ctx;

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        _measureContext = ctx;

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
        ctx.FillRect(b, new GridPaint { Color = bgColor, Style = PaintStyle.Fill });

        // Border — thicker when focused
        float bw = IsFocused ? BorderWidth + 1f : BorderWidth;
        if (Validation == ValidationState.Error) bw = BorderWidth + 1.5f;
        ctx.DrawRect(b, new GridPaint
        {
            Color = borderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = bw,
            IsAntiAlias = true
        });

        // Clip to inner area
        float innerX = b.X + Padding;
        float innerY = b.Y;
        float innerW = b.Width - Padding * 2;
        float innerH = b.Height;

        ctx.Save();
        ctx.ClipRect(new GridRect(innerX, innerY, innerW, innerH));
        ctx.Translate(-_scrollOffset, 0);

        // Compute properly centered text baseline using font metrics
        var textPaint = new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font };
        var fontMetrics = ctx.GetFontMetrics(textPaint);
        float textHeight = fontMetrics.TextHeight;
        float textBaseline = b.Y + (b.Height - textHeight) / 2 - fontMetrics.Ascent;

        // Selection highlight
        if (IsFocused && HasSelection)
        {
            DrawSelectionHighlight(ctx, innerX, b.Y, b.Height);
        }

        // Text or placeholder
        if (_text.Length > 0)
        {
            ctx.DrawText(_text, innerX, textBaseline, textPaint);
        }
        else if (!IsFocused && !string.IsNullOrEmpty(Placeholder))
        {
            var phPaint = new GridPaint { Color = PlaceholderColor, Style = PaintStyle.Fill, Font = Font };
            ctx.DrawText(Placeholder, innerX, textBaseline, phPaint);
        }

        // Cursor
        if (IsFocused && !IsReadOnly)
        {
            DrawCursor(ctx, innerX, b.Y, b.Height, fontMetrics);
        }

        ctx.Restore();
    }

    private void DrawSelectionHighlight(IDrawingContext ctx, float textX, float y, float height)
    {
        int start = Math.Min(_selectionStart, _selectionEnd);
        int end = Math.Max(_selectionStart, _selectionEnd);

        float startX = textX + MeasureSubstring(0, start);
        float endX = textX + MeasureSubstring(0, end);

        var paint = new GridPaint { Color = SelectionHighlightColor, Style = PaintStyle.Fill };
        ctx.FillRect(new GridRect(startX, y + 2, endX - startX, height - 4), paint);
    }

    private void DrawCursor(IDrawingContext ctx, float textX, float y, float height, GridFontMetrics fontMetrics)
    {
        // Blink every 530ms — but always visible for first 530ms after reset
        long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _cursorBlinkStart;
        if ((elapsed / 530) % 2 != 0) return;

        float cursorX = textX + MeasureSubstring(0, _cursorPosition);

        // Align cursor to the text area using font metrics
        float textHeight = fontMetrics.TextHeight;
        float textTop = y + (height - textHeight) / 2;
        float cursorTop = textTop + 1f;
        float cursorBottom = textTop + textHeight - 1f;

        var paint = new GridPaint { Color = CursorColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f };
        ctx.DrawLine(cursorX, cursorTop, cursorX, cursorBottom, paint);
    }

    private float MeasureSubstring(int start, int end)
    {
        if (_measureContext == null || start >= end || end > _text.Length) return 0;
        string sub = _text.Substring(start, end - start);
        return _measureContext.MeasureText(sub, new GridPaint { Font = Font }).Width;
    }

    // ── Focus ───────────────────────────────────────────────────

    protected override void OnGotFocus()
    {
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (InitialSelectionMode == EditTextSelectionMode.CursorAtEnd)
        {
            ClearSelection();
            _cursorPosition = _text.Length;
        }
        else
        {
            SelectAll();
        }
    }

    protected override void OnLostFocus()
    {
        ClearSelection();

        // Apply output formatting on focus loss
        if (OutputFormatter != null && _text.Length > 0)
        {
            var formatted = OutputFormatter(_text);
            if (formatted != _text)
            {
                var old = _text;
                _text = formatted;
                _cursorPosition = Math.Min(_cursorPosition, _text.Length);
                TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
                RaiseValueChanged(old, _text);
            }
        }
    }

    // ── Pointer input ───────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsFocused || IsReadOnly) return false;

        _cursorPosition = GetCharIndexAtX(e.X);
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if ((e.Modifiers & InputModifiers.Shift) != 0)
        {
            // Extend selection
            _selectionEnd = _cursorPosition;
        }
        else
        {
            _selectionStart = _cursorPosition;
            _selectionEnd = _cursorPosition;
        }

        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(GridPointerEventArgs e)
    {
        if (!IsFocused || _selectionStart < 0) return false;

        _cursorPosition = GetCharIndexAtX(e.X);
        _selectionEnd = _cursorPosition;
        InvalidateVisual();
        return true;
    }

    private int GetCharIndexAtX(float screenX)
    {
        if (_measureContext == null) return 0;

        float localX = screenX - Bounds.X - Padding + _scrollOffset;
        if (localX <= 0) return 0;
        if (_text.Length == 0) return 0;

        // Binary search for character at X
        float totalWidth = MeasureSubstring(0, _text.Length);
        if (localX >= totalWidth) return _text.Length;

        for (int i = 0; i < _text.Length; i++)
        {
            float charEnd = MeasureSubstring(0, i + 1);
            float charMid = (i > 0 ? MeasureSubstring(0, i) : 0) + (charEnd - (i > 0 ? MeasureSubstring(0, i) : 0)) / 2;
            if (localX < charMid)
                return i;
        }
        return _text.Length;
    }

    // ── Keyboard input ──────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (!IsFocused) return false;

        bool ctrl = e.HasControl;
        bool shift = e.HasShift;

        switch (e.Key)
        {
            case GridKey.Left:
                MoveCursor(ctrl ? FindPreviousWordBoundary() : _cursorPosition - 1, shift);
                return true;

            case GridKey.Right:
                MoveCursor(ctrl ? FindNextWordBoundary() : _cursorPosition + 1, shift);
                return true;

            case GridKey.Home:
                MoveCursor(0, shift);
                return true;

            case GridKey.End:
                MoveCursor(_text.Length, shift);
                return true;

            case GridKey.Backspace:
                if (!IsReadOnly) HandleBackspace(ctrl);
                return true;

            case GridKey.Delete:
                if (!IsReadOnly) HandleDelete(ctrl);
                return true;

            case GridKey.Enter:
                Committed?.Invoke();
                return true;

            case GridKey.Escape:
                Cancelled?.Invoke();
                return true;

            case GridKey.A:
                if (ctrl) { SelectAll(); return true; }
                if (!IsReadOnly && e.Character.HasValue) InsertCharacter(e.Character.Value);
                return true;

            case GridKey.C:
                if (ctrl) { HandleCopy(); return true; }
                if (!IsReadOnly && e.Character.HasValue) InsertCharacter(e.Character.Value);
                return true;

            case GridKey.V:
                if (ctrl) { HandlePaste(); return true; }
                if (!IsReadOnly && e.Character.HasValue) InsertCharacter(e.Character.Value);
                return true;

            case GridKey.X:
                if (ctrl) { HandleCut(); return true; }
                if (!IsReadOnly && e.Character.HasValue) InsertCharacter(e.Character.Value);
                return true;

            default:
                if (!IsReadOnly && e.Character.HasValue && e.Character.Value != '\0')
                {
                    InsertCharacter(e.Character.Value);
                    return true;
                }
                return false;
        }
    }

    // ── Text manipulation ───────────────────────────────────────

    private void InsertCharacter(char c)
    {
        if (c == '\0') return;
        if (_numericOnly && !char.IsDigit(c) && c != '.' && c != '-' && c != ',') return;
        if (MaxLength > 0 && _text.Length >= MaxLength && !HasSelection) return;

        string insertText = c.ToString();
        InsertText(insertText);
    }

    /// <summary>Insert text at cursor, replacing any selection.</summary>
    public void InsertText(string text)
    {
        if (IsReadOnly) return;

        var old = _text;
        string tempText = _text;
        int tempCursor = _cursorPosition;

        if (HasSelection)
        {
            int start = Math.Min(_selectionStart, _selectionEnd);
            int end = Math.Max(_selectionStart, _selectionEnd);
            tempText = tempText.Remove(start, end - start);
            tempCursor = start;
        }

        if (MaxLength > 0)
        {
            int available = MaxLength - tempText.Length;
            if (available <= 0) return;
            text = text.Substring(0, Math.Min(text.Length, available));
        }

        string candidateText = tempText.Insert(tempCursor, text);

        // Apply input mask validation
        if (InputMask != null && !ValidateAgainstMask(candidateText))
            return;

        // Apply custom input validator
        if (InputValidator != null && !InputValidator(candidateText))
        {
            Validation = ValidationState.Error;
            InvalidateVisual();
            return;
        }

        // Accept the change
        if (HasSelection)
        {
            int start = Math.Min(_selectionStart, _selectionEnd);
            int end = Math.Max(_selectionStart, _selectionEnd);
            _text = _text.Remove(start, end - start);
            _cursorPosition = start;
        }

        _text = _text.Insert(_cursorPosition, text);
        _cursorPosition += text.Length;
        ClearSelection();
        EnsureCursorVisible();
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (InputValidator != null)
            Validation = ValidationState.Valid;

        if (old != _text)
        {
            TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
            RaiseValueChanged(old, _text);
        }

        InvalidateVisual();
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

    private void HandleBackspace(bool wholeWord)
    {
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (_cursorPosition <= 0) return;

        int deleteStart = wholeWord ? FindPreviousWordBoundary() : _cursorPosition - 1;
        int deleteCount = _cursorPosition - deleteStart;

        var old = _text;
        _text = _text.Remove(deleteStart, deleteCount);
        _cursorPosition = deleteStart;
        EnsureCursorVisible();
        TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
        RaiseValueChanged(old, _text);
        InvalidateVisual();
    }

    private void HandleDelete(bool wholeWord)
    {
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (_cursorPosition >= _text.Length) return;

        int deleteEnd = wholeWord ? FindNextWordBoundary() : _cursorPosition + 1;
        int deleteCount = deleteEnd - _cursorPosition;

        var old = _text;
        _text = _text.Remove(_cursorPosition, deleteCount);
        TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
        RaiseValueChanged(old, _text);
        InvalidateVisual();
    }

    private void DeleteSelection()
    {
        if (!HasSelection) return;
        int start = Math.Min(_selectionStart, _selectionEnd);
        int end = Math.Max(_selectionStart, _selectionEnd);

        var old = _text;
        _text = _text.Remove(start, end - start);
        _cursorPosition = start;
        ClearSelection();
        EnsureCursorVisible();
        TextChanged?.Invoke(this, new TextChangedEventArgs(old, _text));
        RaiseValueChanged(old, _text);
        InvalidateVisual();
    }

    // ── Selection ───────────────────────────────────────────────

    /// <summary>Select all text.</summary>
    public void SelectAll()
    {
        _selectionStart = 0;
        _selectionEnd = _text.Length;
        _cursorPosition = _text.Length;
        InvalidateVisual();
    }

    /// <summary>Clear the selection.</summary>
    public void ClearSelection()
    {
        _selectionStart = -1;
        _selectionEnd = -1;
    }

    private void MoveCursor(int newPosition, bool extendSelection)
    {
        newPosition = Math.Clamp(newPosition, 0, _text.Length);

        if (extendSelection)
        {
            if (_selectionStart < 0) _selectionStart = _cursorPosition;
            _selectionEnd = newPosition;
        }
        else
        {
            ClearSelection();
        }

        _cursorPosition = newPosition;
        _cursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        EnsureCursorVisible();
        InvalidateVisual();
    }

    // ── Word boundaries ─────────────────────────────────────────

    private int FindPreviousWordBoundary()
    {
        if (_cursorPosition <= 0) return 0;
        int pos = _cursorPosition - 1;
        // Skip whitespace
        while (pos > 0 && char.IsWhiteSpace(_text[pos])) pos--;
        // Skip word chars
        while (pos > 0 && !char.IsWhiteSpace(_text[pos - 1])) pos--;
        return pos;
    }

    private int FindNextWordBoundary()
    {
        if (_cursorPosition >= _text.Length) return _text.Length;
        int pos = _cursorPosition;
        // Skip word chars
        while (pos < _text.Length && !char.IsWhiteSpace(_text[pos])) pos++;
        // Skip whitespace
        while (pos < _text.Length && char.IsWhiteSpace(_text[pos])) pos++;
        return pos;
    }

    // ── Scrolling ───────────────────────────────────────────────

    private void EnsureCursorVisible()
    {
        if (_measureContext == null) return;

        float cursorX = MeasureSubstring(0, _cursorPosition);
        float visibleWidth = Bounds.Width - Padding * 2;

        if (cursorX - _scrollOffset > visibleWidth)
            _scrollOffset = cursorX - visibleWidth + Padding;
        else if (cursorX - _scrollOffset < 0)
            _scrollOffset = cursorX;

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
        CopyRequested?.Invoke(this, new ClipboardTextEventArgs(SelectedText));
        DeleteSelection();
    }

    private void HandlePaste()
    {
        var args = new ClipboardRequestEventArgs();
        PasteRequested?.Invoke(this, args);
        if (!string.IsNullOrEmpty(args.Text))
            InsertText(args.Text);
    }
}

/// <summary>Text changed event args.</summary>
public class TextChangedEventArgs : EventArgs
{
    /// <summary>The text value before the change.</summary>
    public string OldText { get; }
    /// <summary>The text value after the change.</summary>
    public string NewText { get; }
    /// <summary>Initializes a new instance of the <see cref="TextChangedEventArgs"/> class.</summary>
    /// <param name="oldText">The previous text value.</param>
    /// <param name="newText">The new text value.</param>
    public TextChangedEventArgs(string oldText, string newText) { OldText = oldText; NewText = newText; }
}

/// <summary>Clipboard request (for paste — host fills in the Text).</summary>
public class ClipboardRequestEventArgs : EventArgs
{
    /// <summary>The clipboard text provided by the host for paste operations.</summary>
    public string? Text { get; set; }
}

/// <summary>Clipboard text event (for copy/cut — host reads the Text).</summary>
public class ClipboardTextEventArgs : EventArgs
{
    /// <summary>The text to be placed on the clipboard.</summary>
    public string Text { get; }
    /// <summary>Initializes a new instance of the <see cref="ClipboardTextEventArgs"/> class.</summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    public ClipboardTextEventArgs(string text) { Text = text; }
}

/// <summary>Validation state for input controls.</summary>
public enum ValidationState
{
    /// <summary>No validation applied.</summary>
    None,
    /// <summary>Input is valid.</summary>
    Valid,
    /// <summary>Input has a warning.</summary>
    Warning,
    /// <summary>Input is invalid.</summary>
    Error
}
