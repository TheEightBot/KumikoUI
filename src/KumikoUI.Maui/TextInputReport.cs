namespace KumikoUI.Maui;

/// <summary>What a platform input method must be told after Core changed the text surface it edits.</summary>
internal enum TextInputReport
{
    /// <summary>Nothing it does not already know.</summary>
    None,

    /// <summary>The selection or composition moved (Android: <c>InputMethodManager.updateSelection</c>).</summary>
    Selection,

    /// <summary>The text changed, so the input method reads it again (Android: <c>restartInput</c>).</summary>
    Restart,
}
