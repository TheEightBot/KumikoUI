using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The column menu button in the column headers (#319).</summary>
public sealed partial class KumikoRenderer
{
    private bool _showColumnMenuButton;

    /// <summary>
    /// Whether every column header draws a column menu button (three dots, <see cref="KumikoIcon.ColumnMenu"/>), left
    /// of the filter icon, and keeps room for it beside the header text. A tap on it opens the column menu when the
    /// connected <see cref="KumikoInputController"/>'s <see cref="KumikoInputController.ShowColumnMenuButton"/> and
    /// <see cref="KumikoInputController.IsColumnMenuEnabled"/> are on too; the MAUI and Uno grids set all three from
    /// their own properties. Default false.
    /// </summary>
    public bool ShowColumnMenuButton
    {
        get => _showColumnMenuButton;
        set
        {
            _showColumnMenuButton = value;
            _layout.HeaderMenuButtonSpace = value ? KumikoLayoutEngine.MenuButtonGlyphSpace : 0f;
        }
    }

    // Header width the column menu button takes when the headers show it: button (12) + gap (4).
    private float MenuButtonGlyphSpace(KumikoColumn col) =>
        _showColumnMenuButton ? KumikoLayoutEngine.MenuButtonGlyphSpace : 0f;

    /// <summary>Draws the column menu button of each header in <paramref name="visibleCols"/>; allocation-free.</summary>
    private void DrawColumnMenuButtons(
        IDrawingContext ctx,
        List<(KumikoColumn Column, float X, int OriginalIndex)> visibleCols,
        ScrollState scroll,
        KumikoStyle style)
    {
        if (!_showColumnMenuButton) return;
        foreach (var (col, colX, columnIndex) in visibleCols)
        {
            if (col.ColumnType == KumikoColumnType.Selection) continue; // its header is the check-all box
            float screenX = ColumnScreenX(colX, scroll, col.Pane);
            // Left of the filter icon when one is drawn; the hit tester uses the same geometry
            var rect = InteractionGeometry.MenuButtonRect(screenX, col, style, AllowFiltering && col.AllowFiltering);
            if (OverlayFor(HitRegion.HeaderMenuButton, -1, columnIndex, -1) is { } overlay)
                ctx.FillRoundRect(rect.Inflate(4, 4), 4, overlay);
            style.IconProvider.DrawIcon(ctx, KumikoIcon.ColumnMenu, rect, _paintCache.FilterIcon);
        }
    }
}
