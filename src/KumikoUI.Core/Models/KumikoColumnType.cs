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
    Picker
}
