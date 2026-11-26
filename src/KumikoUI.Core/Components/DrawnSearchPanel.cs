using System.Globalization;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// The grid's drawn search panel (#317): a search box, the position among the matches ("3 of 12"), previous and
/// next buttons and a close button, shown as a modeless popup at the top right of the data area. Enter moves to the
/// next match, Shift+Enter to the previous one, Escape closes the panel and clears the search. The box is a
/// <see cref="DrawnTextBox"/>, so input methods, the soft keyboard and the clipboard work as in the filter popup's
/// search box. The panel has the keyboard while it is focused; a press on the grid takes it back.
/// </summary>
internal sealed class DrawnSearchPanel : DrawnComponent
{
    internal const float PanelWidth = 300f;
    internal const float PanelHeight = 38f;
    private const float Padding = 6f;
    private const float ButtonSize = 26f;
    private const float CountWidth = 70f;
    private const float Gap = 4f;

    private readonly KumikoSource _source;
    private readonly Func<CellPosition> _currentCell;
    private readonly DrawnTextBox _box;
    private Target _pressTarget;
    private Target _hoverTarget;

    // The count label and what it was composed from, so a frame that shows the same state composes nothing.
    private string _countText = string.Empty;
    private int _countVersion = -1;
    private string? _countSearch;
    private CellPosition _countCell = CellPosition.Invalid;
    private int _countTotal = int.MinValue;
    private KumikoStrings? _countStrings;

    private enum Target { None, Box, Previous, Next, Close }

    private readonly record struct PanelLayout(KumikoRect Box, KumikoRect Count, KumikoRect Previous, KumikoRect Next, KumikoRect Close);

    /// <summary>Creates the panel for <paramref name="source"/>; <paramref name="currentCell"/> reads the grid's current cell.</summary>
    public DrawnSearchPanel(KumikoSource source, Func<CellPosition> currentCell)
    {
        _source = source;
        _currentCell = currentCell;
        _box = new DrawnTextBox
        {
            Font = Font,
            Padding = 6f,
            InitialSelectionMode = Editing.EditTextSelectionMode.CursorAtEnd,
        };
        _box.ValueChanged += (_, _) => { TextEdited?.Invoke(this, EventArgs.Empty); InvalidateVisual(); };
        _box.RedrawRequested += (_, _) => InvalidateVisual();
        Bounds = new KumikoRect(0, 0, PanelWidth, PanelHeight);
    }

    // ── Styling ──────────────────────────────────────────────────

    public KumikoFont Font { get; set; } = new("Default", 12);
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    public KumikoColor BoxBorderColor { get; set; } = new(200, 200, 200);
    public KumikoColor AccentColor { get; set; } = new(0, 120, 215);
    public KumikoColor HoverColor { get; set; } = new(0, 120, 215, 30);
    public KumikoColor PlaceholderColor { get; set; } = new(160, 160, 160);
    public KumikoColor NoMatchesColor { get; set; } = new(200, 40, 40);

    /// <summary>Takes the panel's colors from the grid's style, as the filter popup does.</summary>
    public void ApplyTheme(KumikoStyle style)
    {
        IconProvider = style.IconProvider;
        BackgroundColor = style.BackgroundColor;
        TextColor = style.CellTextColor;
        BorderColor = style.HeaderBorderColor;
        BoxBorderColor = style.GridLineColor;
        AccentColor = style.AccentColor;
        HoverColor = style.AccentColor.WithAlpha(30);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;
        PlaceholderColor = new KumikoColor((byte)((txt.R + bg.R) / 2), (byte)((txt.G + bg.G) / 2), (byte)((txt.B + bg.B) / 2));
        _box.TextColor = style.CellTextColor;
        _box.CursorColor = style.CellTextColor;
        _box.BackgroundColor = style.BackgroundColor;
        _box.EditingBackgroundColor = style.BackgroundColor;
        _box.BorderColor = BoxBorderColor;
        _box.FocusedBorderColor = style.AccentColor;
        _box.SelectionHighlightColor = style.AccentColor.WithAlpha(80);
        InvalidateVisual();
    }

    // ── Events ───────────────────────────────────────────────────

    /// <summary>The text in the box changed (typed, pasted or set).</summary>
    public event EventHandler? TextEdited;

    /// <summary>Enter or the next button: move to the next match.</summary>
    public event EventHandler? NextRequested;

    /// <summary>Shift+Enter or the previous button: move to the previous match.</summary>
    public event EventHandler? PreviousRequested;

    /// <summary>Escape or the close button: close the panel.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The box was pressed and needs the platform keyboard.</summary>
    public event EventHandler? KeyboardFocusRequested;

    /// <summary>Raised when the box copies or cuts text: the host takes the text.</summary>
    public event EventHandler<ClipboardTextEventArgs>? CopyRequested
    {
        add => _box.CopyRequested += value;
        remove => _box.CopyRequested -= value;
    }

    /// <summary>Raised when the box pastes: the host supplies the text.</summary>
    public event EventHandler<ClipboardRequestEventArgs>? PasteRequested
    {
        add => _box.PasteRequested += value;
        remove => _box.PasteRequested -= value;
    }

    // ── State ────────────────────────────────────────────────────

    /// <summary>The search box's text-input surface.</summary>
    public ITextInputSession TextInput => _box;

    /// <summary>The text in the box.</summary>
    public string Text
    {
        get => _box.Text;
        set
        {
            if (_box.Text == value) return;
            _box.Text = value;
            _box.SetSelection(_box.Text.Length, _box.Text.Length);
        }
    }

    /// <summary>Pastes <paramref name="text"/> into the box.</summary>
    public void PasteText(string? text) => _box.PasteText(text);

    /// <summary>Selects all the text in the box (Ctrl/Cmd+F again).</summary>
    public void SelectAll() => _box.SelectAll();

    /// <summary>The text the panel shows for the matches: "3 of 12", "12 matches", "No matches" or "?".</summary>
    internal string CountText
    {
        get
        {
            UpdateCountText();
            return _countText;
        }
    }

    private void UpdateCountText()
    {
        var strings = _source.Strings;
        string search = _source.SearchText;
        if (search.Length == 0)
        {
            _countText = string.Empty;
            _countSearch = search;
            return;
        }

        int total = _source.SearchMatchCount;
        var cell = _currentCell();
        int version = _source.ContentVersion;
        if (total == _countTotal && version == _countVersion && ReferenceEquals(search, _countSearch) &&
            cell == _countCell && ReferenceEquals(strings, _countStrings))
            return;

        _countTotal = total;
        _countVersion = version;
        _countSearch = search;
        _countCell = cell;
        _countStrings = strings;
        if (total < 0)
        {
            _countText = strings.SearchCountPending;
            return;
        }
        if (total == 0)
        {
            _countText = strings.SearchNoMatches;
            return;
        }
        int ordinal = _source.GetSearchMatchOrdinal(cell);
        _countText = ordinal > 0
            ? string.Format(CultureInfo.CurrentCulture, strings.SearchMatchCount, ordinal, total)
            : string.Format(CultureInfo.CurrentCulture, strings.SearchMatchTotal, total);
    }

    // ── Layout and drawing ───────────────────────────────────────

    private PanelLayout Layout()
    {
        var b = Bounds;
        float y = b.Y + (b.Height - ButtonSize) / 2;
        float right = b.Right - Padding;
        var close = new KumikoRect(right - ButtonSize, y, ButtonSize, ButtonSize);
        var next = new KumikoRect(close.X - ButtonSize, y, ButtonSize, ButtonSize);
        var previous = new KumikoRect(next.X - ButtonSize, y, ButtonSize, ButtonSize);
        var count = new KumikoRect(previous.X - Gap - CountWidth, b.Y, CountWidth, b.Height);
        float boxX = b.X + Padding;
        var box = new KumikoRect(boxX, y, Math.Max(40f, count.X - Gap - boxX), ButtonSize);
        return new PanelLayout(box, count, previous, next, close);
    }

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;
        var layout = Layout();
        ctx.FillRect(Bounds, Paints.Get(color: BackgroundColor));
        ctx.DrawRect(Bounds, Paints.Get(color: BorderColor, strokeWidth: 1));

        _box.Bounds = layout.Box;
        _box.Font = Font;
        _box.BorderColor = BoxBorderColor;
        _box.FocusedBorderColor = AccentColor;
        _box.OnDraw(ctx);
        if (_box.Text.Length == 0)
        {
            ctx.DrawTextInRect(_source.Strings.SearchPlaceholder,
                new KumikoRect(layout.Box.X + 8, layout.Box.Y, layout.Box.Width - 12, layout.Box.Height),
                Paints.Get(color: PlaceholderColor, font: Font), KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }

        string count = CountText;
        if (count.Length > 0)
        {
            var color = _countTotal == 0 ? NoMatchesColor : PlaceholderColor;
            ctx.DrawTextInRect(count, layout.Count, Paints.Get(color: color, font: Font),
                KumikoTextAlignment.Right, KumikoVerticalAlignment.Center);
        }

        DrawButton(ctx, layout.Previous, KumikoIcon.SpinUp, Target.Previous);
        DrawButton(ctx, layout.Next, KumikoIcon.SpinDown, Target.Next);
        DrawButton(ctx, layout.Close, KumikoIcon.Remove, Target.Close);
    }

    private void DrawButton(IDrawingContext ctx, KumikoRect rect, KumikoIcon icon, Target target)
    {
        if (_hoverTarget == target || _pressTarget == target)
            ctx.FillRoundRect(rect, 4, Paints.Get(color: HoverColor));
        const float iconSize = 12f;
        var iconRect = new KumikoRect(rect.X + (rect.Width - iconSize) / 2, rect.Y + (rect.Height - iconSize) / 2, iconSize, iconSize);
        IconProvider.DrawIcon(ctx, icon, iconRect, Paints.Get(color: TextColor, strokeWidth: 1.5f));
    }

    private Target TargetAt(float x, float y)
    {
        var layout = Layout();
        if (layout.Box.Contains(x, y)) return Target.Box;
        if (layout.Previous.Contains(x, y)) return Target.Previous;
        if (layout.Next.Contains(x, y)) return Target.Next;
        if (layout.Close.Contains(x, y)) return Target.Close;
        return Target.None;
    }

    // ── Pointer ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        _pressTarget = TargetAt(e.X, e.Y);
        // Any press on the panel gives it the keyboard; a press on the box also places the caret.
        IsFocused = true;
        if (_pressTarget == Target.Box)
        {
            _box.OnPointerDown(e);
            KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (_pressTarget == Target.Box) _box.OnPointerMove(e);
        var hover = TargetAt(e.X, e.Y);
        if (hover != _hoverTarget)
        {
            _hoverTarget = hover;
            InvalidateVisual();
        }
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        var pressed = _pressTarget;
        _pressTarget = Target.None;
        bool same = TargetAt(e.X, e.Y) == pressed;
        switch (pressed)
        {
            case Target.Box:
                _box.OnPointerUp(e);
                break;
            case Target.Previous when same:
                PreviousRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Target.Next when same:
                NextRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Target.Close when same:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override void OnPointerCancel(KumikoPointerEventArgs e)
    {
        _pressTarget = Target.None;
        InvalidateVisual();
    }

    // ── Keyboard ─────────────────────────────────────────────────

    /// <summary>
    /// While the panel has the keyboard: Enter (Shift+Enter) moves to the next (previous) match, Escape closes the
    /// panel, Ctrl/Cmd+F selects the text, and everything else edits the text. Unfocused, it takes no key.
    /// </summary>
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (!IsFocused) return false;

        // An input method composing in the box gets Enter and Escape first
        if (_box.Composition != null && e.Key is KumikoKey.Enter or KumikoKey.Escape)
            return _box.OnKeyDown(e);

        switch (e.Key)
        {
            case KumikoKey.Enter:
                if (e.HasShift) PreviousRequested?.Invoke(this, EventArgs.Empty);
                else NextRequested?.Invoke(this, EventArgs.Empty);
                return true;
            case KumikoKey.Escape:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return true;
            case KumikoKey.F when e.HasCommand:
                _box.SelectAll();
                return true;
            case KumikoKey.Tab:
                return false; // Tab leaves the box to the grid's own Tab handling
            case KumikoKey.Space:
                // Space arrives as KumikoKey.Space (not Character) on Apple platforms
                _box.InsertText(" ");
                return true;
        }
        return _box.OnKeyDown(e) || e.Key == KumikoKey.Character;
    }

    // ── Focus ────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnGotFocus() => _box.IsFocused = true;

    /// <inheritdoc />
    protected override void OnLostFocus() => _box.IsFocused = false;
}
