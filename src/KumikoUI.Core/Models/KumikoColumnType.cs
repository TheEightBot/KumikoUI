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
    /// A time of day: <see cref="TimeOnly"/>, <see cref="TimeSpan"/> or the time part of a <see cref="DateTime"/>.
    /// Shown with <see cref="KumikoColumn.Format"/> ("t", the culture's short time, when it is not set) and
    /// edited in a <see cref="DrawnTimePicker"/> (<see cref="KumikoColumn.Use24HourTime"/>,
    /// <see cref="KumikoColumn.TimeMinuteStep"/>). Sorts earliest first on the first tap and filters by the
    /// time of day, to the minute.
    /// </summary>
    Time,
    /// <summary>
    /// A date with a time of day: <see cref="DateTime"/> or <see cref="DateTimeOffset"/>. Shown with
    /// <see cref="KumikoColumn.Format"/> ("g", the culture's short date and time, when it is not set) and edited in a
    /// <see cref="DrawnDatePicker"/> with its time row. Sorts newest first on the first tap and filters by the
    /// date and time, to the minute.
    /// </summary>
    DateTime
}
