namespace KumikoUI.Core.Components;

/// <summary>
/// A popup that opens popups of its own inside its drawing (the filter popup's operator drop-down and calendar, #325).
/// A press outside every popup closes only the innermost open one, as if it were a popup of its own.
/// </summary>
internal interface INestedPopupHost
{
    /// <summary>Closes the nested popup that is open.</summary>
    /// <returns>False when none is open.</returns>
    bool CloseNestedPopup();
}
