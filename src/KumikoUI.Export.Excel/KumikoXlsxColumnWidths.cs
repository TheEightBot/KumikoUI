namespace KumikoUI.Export.Excel;

/// <summary>How <see cref="KumikoXlsxExporter"/> sizes the columns of the sheet.</summary>
public enum KumikoXlsxColumnWidths
{
    /// <summary>
    /// Each column gets the width it has in the grid (<c>KumikoColumn.Width</c>), converted from device-independent
    /// pixels to Excel's character units for the default 11-point Calibri font.
    /// </summary>
    FromGrid = 0,

    /// <summary>No widths are written: every column has the sheet's default width (about 8.43 characters).</summary>
    Fixed = 1,
}
