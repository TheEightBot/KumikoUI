namespace KumikoUI.Uno;

/// <summary>
/// Transitions (#101): the grid steps Core's transitions (a group sliding open or shut, a dragged column header
/// lifting and settling, the current row's highlight fading in) from its frame loop, and plays none when the platform
/// asks for reduced motion. <see cref="Core.Models.KumikoStyle.EnableAnimations"/> turns them off per style.
/// </summary>
/// <remarks>
/// The reduced-motion setting is read on each load from <c>UISettings.AnimationsEnabled</c> (the Windows "Animation
/// effects" setting; Uno reports its platform's setting where it implements it, and true elsewhere).
/// </remarks>
public partial class KumikoDataGrid
{
    /// <summary>Lets Core play transitions unless the platform asks for reduced motion.</summary>
    private void SyncReducedMotion() => _inputController.AnimationsEnabled = !PlatformReducesMotion();

    private static bool PlatformReducesMotion()
    {
        try
        {
            return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch (Exception)
        {
            return false; // a head that cannot tell plays them
        }
    }
}
