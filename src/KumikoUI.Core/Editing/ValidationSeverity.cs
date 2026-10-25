using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Validation severity level.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Validation error that prevents commit.</summary>
    Error,
    /// <summary>Validation warning (commit still allowed).</summary>
    Warning,
    /// <summary>Informational validation message.</summary>
    Info
}
