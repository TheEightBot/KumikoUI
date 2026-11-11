namespace KumikoUI.Core.Models;

/// <summary>Where a row or column lands in the viewport when the grid is scrolled to it programmatically.</summary>
public enum KumikoScrollPosition
{
    /// <summary>Scroll the least that makes the target fully visible; nothing moves when it already is.</summary>
    Nearest,

    /// <summary>Put the target at the start of the scrolling area: below the pinned rows and any sticky group headers for a row, after the frozen columns for a column.</summary>
    Start,

    /// <summary>Center the target in the scrolling area.</summary>
    Center,

    /// <summary>Put the target at the end of the scrolling area: the bottom of the rows, the right edge of the columns.</summary>
    End,
}
