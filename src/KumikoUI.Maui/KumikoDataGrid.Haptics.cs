using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// Optional haptic feedback (#101): a short vibration when a finger picks up or drops a row or column header and on
/// a long press, from Core's <see cref="KumikoInputController.HapticFeedbackRequested"/>. Off by default
/// (<see cref="EnableHaptics"/>).
/// </summary>
/// <remarks>
/// iOS uses <c>UIImpactFeedbackGenerator</c> (medium for a pick-up or a long press, light for a drop). Android asks the
/// grid's view for <c>PerformHapticFeedback</c>, which follows the system's touch feedback setting and needs no
/// permission. Mac Catalyst and Windows have no haptics to give.
/// </remarks>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="EnableHaptics"/> bindable property.</summary>
    public static readonly BindableProperty EnableHapticsProperty = BindableProperty.Create(
        nameof(EnableHaptics), typeof(bool), typeof(KumikoDataGrid), false);

    /// <summary>
    /// Gets or sets whether the grid gives a short vibration when a finger picks up or drops a row or a column header,
    /// and on a long press that opens a context menu or an editor. iOS and Android only. Default: false.
    /// </summary>
    public bool EnableHaptics
    {
        get => (bool)GetValue(EnableHapticsProperty);
        set => SetValue(EnableHapticsProperty, value);
    }

    private void OnHapticFeedbackRequested(object? sender, KumikoHapticEventArgs e)
    {
        if (!EnableHaptics) return;
        try
        {
#if IOS
            var style = e.Kind == KumikoHapticKind.Drop ? UIKit.UIImpactFeedbackStyle.Light : UIKit.UIImpactFeedbackStyle.Medium;
            using var generator = new UIKit.UIImpactFeedbackGenerator(style);
            generator.ImpactOccurred();
#elif ANDROID
            if (_canvasView.Handler?.PlatformView is Android.Views.View view)
            {
                view.PerformHapticFeedback(e.Kind == KumikoHapticKind.Drop
                    ? Android.Views.FeedbackConstants.VirtualKey
                    : Android.Views.FeedbackConstants.LongPress);
            }
#endif
        }
        catch (Exception)
        {
            // A device without haptics: nothing to give
        }
    }
}
