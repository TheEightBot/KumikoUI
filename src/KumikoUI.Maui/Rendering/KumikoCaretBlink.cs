namespace KumikoUI.Maui.Rendering;

/// <summary>
/// Timing of the drawn text caret's blink, so the MAUI host redraws only when the caret changes state (#71, as the
/// Uno host does, #65). The phase matches <c>DrawnTextBox</c>, which shows the caret for <see cref="PeriodMs"/>, hides
/// it for <see cref="PeriodMs"/>, and so on, from the moment it takes focus.
/// </summary>
internal static class KumikoCaretBlink
{
    /// <summary>How long the caret stays visible, then hidden (the same constant <c>DrawnTextBox</c> draws with).</summary>
    public const int PeriodMs = 530;

    /// <summary>
    /// How far past a transition the redraw is scheduled, so timer jitter never lands it just before the boundary
    /// (which would draw the old state and miss the blink).
    /// </summary>
    public const int SlackMs = 8;

    /// <summary>
    /// Milliseconds from now until just after the next blink transition, given <paramref name="elapsedMs"/> since the
    /// blink started. Scheduling each tick from the blink's start (rather than a fixed interval) keeps timer drift
    /// from accumulating.
    /// </summary>
    public static int DelayToNextTransition(long elapsedMs)
    {
        if (elapsedMs < 0) elapsedMs = 0;
        return PeriodMs - (int)(elapsedMs % PeriodMs) + SlackMs;
    }
}
