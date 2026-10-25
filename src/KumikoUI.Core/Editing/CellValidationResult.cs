using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Validation result for a cell edit.
/// </summary>
public class CellValidationResult
{
    /// <summary>Whether the cell value is valid.</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>Error or warning message (null when valid).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Severity of the validation result.</summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    public static CellValidationResult Valid => new();
    public static CellValidationResult Error(string message) =>
        new() { IsValid = false, ErrorMessage = message };
    public static CellValidationResult Warning(string message) =>
        new() { IsValid = true, ErrorMessage = message, Severity = ValidationSeverity.Warning };
}
