using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Validation state for input controls.</summary>
public enum ValidationState
{
    /// <summary>No validation applied.</summary>
    None,
    /// <summary>Input is valid.</summary>
    Valid,
    /// <summary>Input has a warning.</summary>
    Warning,
    /// <summary>Input is invalid.</summary>
    Error
}
