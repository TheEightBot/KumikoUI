namespace KumikoUI.Core.Input;

/// <summary>
/// Data for <see cref="KumikoInputController.HapticFeedbackRequested"/>: a touch or pen gesture reached a moment a
/// phone usually confirms with a short vibration.
/// </summary>
public sealed class KumikoHapticEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="kind">The moment the gesture reached.</param>
    /// <param name="deviceType">The pointer that made the gesture.</param>
    public KumikoHapticEventArgs(KumikoHapticKind kind, KumikoPointerDeviceType deviceType)
    {
        Kind = kind;
        DeviceType = deviceType;
    }

    /// <summary>The moment the gesture reached.</summary>
    public KumikoHapticKind Kind { get; }

    /// <summary>The pointer that made the gesture: <see cref="KumikoPointerDeviceType.Touch"/> or <see cref="KumikoPointerDeviceType.Pen"/>.</summary>
    public KumikoPointerDeviceType DeviceType { get; }
}
