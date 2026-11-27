using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines column types for data binding and rendering dispatch.
/// </summary>
public enum KumikoColumnType
{
    /// <summary>Plain text column.</summary>
    Text,
    /// <summary>Numeric column with numeric formatting.</summary>
    Numeric,
    /// <summary>Boolean column rendered as a checkbox.</summary>
    Boolean,
    /// <summary>Date/time column with date formatting.</summary>
    Date,
    /// <summary>Custom template column.</summary>
    Template,
    /// <summary>Image column.</summary>
    Image,
    /// <summary>Drop-down combo box column.</summary>
    ComboBox,
    /// <summary>Scroll-picker column.</summary>
    Picker,
    /// <summary>
    /// Read-only numeric column drawn as a progress bar. Configure it with
    /// <see cref="KumikoColumn.ProgressMinimum"/>, <see cref="KumikoColumn.ProgressMaximum"/>,
    /// <see cref="KumikoColumn.ProgressShowText"/>, <see cref="KumikoColumn.ProgressFillColor"/> and
    /// <see cref="KumikoColumn.Format"/>. Sorts and filters as numbers.
    /// </summary>
    ProgressBar,
    /// <summary>
    /// A column of row check boxes bound to the grid's <see cref="KumikoSelectionModel"/>, with a tri-state check-all
    /// box in its header. Use <see cref="KumikoSelectionColumn"/>, which applies the column's defaults. It has no
    /// cell text, is never edited, sorted or filtered, and is left out of copy.
    /// </summary>
    Selection
}
