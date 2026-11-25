namespace KumikoUI.Uno.Input;

/// <summary>What the host does with the soft keyboard after it focused the proxy text box.</summary>
internal enum SoftKeyboardAction
{
    /// <summary>Leave the keyboard alone (focusing the proxy shows it when it was not focused).</summary>
    None,

    /// <summary>Show the keyboard explicitly: the proxy already had focus, so focusing it did nothing.</summary>
    Show,

    /// <summary>Hide the keyboard: the open editor takes no typed text.</summary>
    Hide,
}

/// <summary>
/// The rule for the soft keyboard when Core asks for keyboard focus (an edit began, or the filter popup opened or its
/// search box was tapped). Kept free of UI types so the focus and show sequence can be tested.
/// </summary>
internal static class SoftKeyboardPolicy
{
    /// <summary>Decides what to do after the proxy was focused.</summary>
    /// <param name="isEditing">A cell edit is open.</param>
    /// <param name="hasActiveTextInput">Core has a text input session (a text editor or the filter search box).</param>
    /// <param name="proxyHadFocus">The proxy already had focus before it was focused again.</param>
    /// <param name="lastPointerWasTouch">The last press was a touch (a mouse or hardware keyboard needs no soft keyboard).</param>
    internal static SoftKeyboardAction Decide(bool isEditing, bool hasActiveTextInput, bool proxyHadFocus, bool lastPointerWasTouch)
    {
        if (isEditing && !hasActiveTextInput) return SoftKeyboardAction.Hide;
        if (proxyHadFocus && lastPointerWasTouch) return SoftKeyboardAction.Show;
        return SoftKeyboardAction.None;
    }
}
