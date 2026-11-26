using KumikoUI.Core.Data;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The load-more row of incremental loading (#318), and the near-end check after each frame.</summary>
public sealed partial class KumikoRenderer
{
    /// <summary>How long each phase of the busy indicator lasts; the hosts repaint at this interval while it shows.</summary>
    internal const int LoadMoreIndicatorStepMs = 200;

    // Set just before each draw from the style, so the row allocates nothing per frame
    private readonly KumikoPaint _loadMoreBackground = new();
    private readonly KumikoPaint _loadMoreText = new() { IsAntiAlias = true };
    private readonly KumikoPaint _loadMoreAccentText = new() { IsAntiAlias = true };
    private readonly KumikoPaint _loadMoreErrorText = new() { IsAntiAlias = true };
    private readonly KumikoPaint _loadMoreDot = new() { IsAntiAlias = true };
    private readonly KumikoPaint _loadMorePill = new() { IsAntiAlias = true };
    private readonly KumikoPaint _loadMorePillText = new() { IsAntiAlias = true };

    /// <summary>
    /// Whether the last frame drew the load-more row's animated busy indicator on screen. The hosts repaint every
    /// <see cref="LoadMoreIndicatorStepMs"/> while it is true, and stop as soon as it is false. With reduced motion
    /// (<see cref="KumikoStyle.EnableAnimations"/>, <see cref="KumikoInputController.AnimationsEnabled"/>) the indicator
    /// is static and this stays false.
    /// </summary>
    internal bool NeedsLoadMoreAnimationFrame { get; private set; }

    /// <summary>The monotonic clock, in milliseconds, the busy indicator's phase is derived from. Tests replace it.</summary>
    internal Func<long> LoadMoreClock { get; set; } = static () => Environment.TickCount64;

    /// <summary>
    /// Draws the load-more row after the last row (inside the full-width rows' clip), centered in the viewport so it
    /// stays readable when the grid is scrolled horizontally.
    /// </summary>
    private void DrawLoadMoreRow(IDrawingContext ctx, KumikoSource source, ScrollState scroll, KumikoStyle style,
        float dataAreaTop, float height, float handleOffset, float rightHandleWidth)
    {
        NeedsLoadMoreAnimationFrame = false;
        if (height <= 0 || source.IncrementalLoaderOrNull is not { } loader) return;
        var kind = loader.RowKind;
        if (kind == LoadMoreRowKind.None) return;

        float top = LoadMoreRowLayout.GetTop(source, style, dataAreaTop, scroll.OffsetY) + RowShift(source.RowCount);
        if (top >= scroll.ViewportHeight || top + height <= dataAreaTop) return; // off screen

        var background = style.LoadMoreRowBackgroundColor ?? style.BackgroundColor;
        var accent = style.LoadMoreIndicatorColor ?? style.AccentColor;
        var font = style.LoadMoreFont ?? style.CellFont;
        var row = new KumikoRect(0, top, scroll.ViewportWidth, height);
        _loadMoreBackground.Color = background;
        ctx.FillRect(row, _loadMoreBackground);
        ctx.DrawLine(0, top + height, scroll.ViewportWidth, top + height, _paintCache.GridLine);

        float left = handleOffset + style.CellPadding;
        float right = scroll.ViewportWidth - rightHandleWidth - style.CellPadding;
        float centerX = (left + right) / 2;
        float centerY = top + height / 2;
        var strings = source.Strings;

        switch (kind)
        {
            case LoadMoreRowKind.Loading:
                DrawLoadMoreBusy(ctx, strings.LoadMoreLoading, style, accent, font, left, right, centerX, centerY, top, height);
                break;

            case LoadMoreRowKind.LoadMore:
                if (OverlayFor(HitRegion.LoadMoreRow, -1, -1, -1) is { } overlay) ctx.FillRect(row, overlay);
                _loadMoreAccentText.Color = accent;
                _loadMoreAccentText.Font = font;
                ctx.DrawTextInRect(strings.LoadMoreButton, new KumikoRect(left, top, Math.Max(0, right - left), height),
                    _loadMoreAccentText, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center, true);
                break;

            case LoadMoreRowKind.Failed:
                DrawLoadMoreFailed(ctx, loader.ErrorText ?? strings.LoadMoreFailed, strings.LoadMoreRetry, style, accent,
                    background, font, left, right, centerX, centerY, top, height);
                break;
        }
    }

    /// <summary>Three dots (the one of the current phase opaque) and the loading text, centered.</summary>
    private void DrawLoadMoreBusy(IDrawingContext ctx, string text, KumikoStyle style, KumikoColor accent, KumikoFont font,
        float left, float right, float centerX, float centerY, float top, float height)
    {
        _loadMoreText.Color = style.LoadMoreTextColor ?? style.CellTextColor;
        _loadMoreText.Font = font;

        float dot = Math.Clamp(height / 5f, 4f, 8f);
        float gap = dot * 0.75f;
        float dotsWidth = 3 * dot + 2 * gap;
        float spacing = style.CellPadding;
        float textWidth = ctx.MeasureText(text, _loadMoreText).Width;
        float x = Math.Max(left, centerX - (dotsWidth + spacing + textWidth) / 2);

        bool animate = _inputController?.CanAnimate(style) ?? style.EnableAnimations;
        int phase = animate ? (int)(LoadMoreClock() / LoadMoreIndicatorStepMs % 3) : -1;
        for (int i = 0; i < 3; i++)
        {
            // Animated: the dot of the phase is opaque, the others faint. Reduced motion: all three alike, static.
            int alpha = phase < 0 ? 170 : i == phase ? 255 : 80;
            _loadMoreDot.Color = new KumikoColor(accent.R, accent.G, accent.B, (byte)(accent.A * alpha / 255));
            ctx.FillRoundRect(new KumikoRect(x + i * (dot + gap), centerY - dot / 2, dot, dot), dot / 2, _loadMoreDot);
        }

        float textLeft = x + dotsWidth + spacing;
        ctx.DrawTextInRect(text, new KumikoRect(textLeft, top, Math.Max(0, right - textLeft), height), _loadMoreText,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center, true);
        NeedsLoadMoreAnimationFrame = animate;
    }

    /// <summary>The error text and a retry pill, centered; the error is shortened first when they do not fit.</summary>
    private void DrawLoadMoreFailed(IDrawingContext ctx, string error, string retry, KumikoStyle style, KumikoColor accent,
        KumikoColor background, KumikoFont font, float left, float right, float centerX, float centerY, float top, float height)
    {
        _loadMoreErrorText.Color = style.LoadMoreErrorTextColor;
        _loadMoreErrorText.Font = font;
        _loadMorePillText.Color = background;
        _loadMorePillText.Font = font;
        _loadMorePill.Color = accent;

        float spacing = style.CellPadding;
        float pillWidth = ctx.MeasureText(retry, _loadMorePillText).Width + 3 * style.CellPadding;
        float pillHeight = Math.Max(0, Math.Min(height - 8, Math.Max(20, height * 0.66f)));
        float errorWidth = Math.Min(ctx.MeasureText(error, _loadMoreErrorText).Width,
            Math.Max(0, right - left - pillWidth - spacing));
        float x = Math.Max(left, centerX - (errorWidth + spacing + pillWidth) / 2);

        ctx.DrawTextInRect(error, new KumikoRect(x, top, errorWidth, height), _loadMoreErrorText,
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center, true);

        var pill = new KumikoRect(x + errorWidth + spacing, centerY - pillHeight / 2, pillWidth, pillHeight);
        ctx.FillRoundRect(pill, pillHeight / 2, _loadMorePill);
        if (OverlayFor(HitRegion.LoadMoreRow, -1, -1, -1) is { } overlay) ctx.FillRoundRect(pill, pillHeight / 2, overlay);
        ctx.DrawTextInRect(retry, pill, _loadMorePillText, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center, true);
    }

    /// <summary>After the frame: let the source's incremental loader decide whether to load (#318). Allocation-free.</summary>
    private void ObserveLoadMore(KumikoSource source, ScrollState scroll, KumikoStyle style)
    {
        if (source.IncrementalLoaderOrNull is not { } loader || scroll.ViewportHeight <= 0) return;
        // With every row frozen nothing scrolls, and the last frozen row is the last one on screen.
        int last = _lastLastRow >= 0 ? _lastLastRow : source.EffectiveFrozenRowCount - 1;
        loader.ObserveFrame(last, source.RowCount, LoadMoreRowLayout.GetRowsPerViewport(scroll, style, source),
            scroll.OffsetY, scroll.ViewportHeight);
    }
}
