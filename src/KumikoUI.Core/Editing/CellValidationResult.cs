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

    /// <summary>A passing result with no message.</summary>
    public static CellValidationResult Valid => new();
    /// <summary>Creates a failing result with <see cref="ValidationSeverity.Error"/> severity; the edit is rejected.</summary>
    /// <param name="message">Message shown to the user.</param>
    public static CellValidationResult Error(string message) =>
        new() { IsValid = false, ErrorMessage = message };
    /// <summary>Creates a passing result that carries a <see cref="ValidationSeverity.Warning"/> message; the edit is still accepted.</summary>
    /// <param name="message">Message shown to the user.</param>
    public static CellValidationResult Warning(string message) =>
        new() { IsValid = true, ErrorMessage = message, Severity = ValidationSeverity.Warning };
}
