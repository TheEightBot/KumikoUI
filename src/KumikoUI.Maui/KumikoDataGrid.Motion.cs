namespace KumikoUI.Maui;

/// <summary>
/// Transitions (#101): the grid steps Core's transitions (a group sliding open or shut, a dragged column header
/// lifting and settling, the current row's highlight fading in) from its frame loop, and plays none when the platform
/// asks for reduced motion. <see cref="Core.Models.KumikoStyle.EnableAnimations"/> turns them off per style.
/// </summary>
/// <remarks>
/// The reduced-motion setting is read when the grid is attached to a handler: iOS and Mac Catalyst
/// <c>UIAccessibility.IsReduceMotionEnabled</c>, Android's animator duration scale set to off (Developer options or
/// the "Remove animations" accessibility setting), Windows <c>UISettings.AnimationsEnabled</c>.
/// </remarks>
public partial class KumikoDataGrid
{
    /// <summary>Lets Core play transitions unless the platform asks for reduced motion.</summary>
    private void SyncReducedMotion() => _inputController.AnimationsEnabled = !PlatformReducesMotion();

    private static bool PlatformReducesMotion()
    {
        try
        {
#if IOS || MACCATALYST
            return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
            var resolver = Microsoft.Maui.ApplicationModel.Platform.AppContext?.ContentResolver;
            return resolver != null &&
                   Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#elif WINDOWS
            return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
            return false;
#endif
        }
        catch (Exception)
        {
            return false; // a platform that cannot tell plays them
        }
    }
}
