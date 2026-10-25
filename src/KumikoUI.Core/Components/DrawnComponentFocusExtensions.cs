using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Extension to allow setting focus internally without going through DrawnFocusManager.</summary>
internal static class DrawnComponentFocusExtensions
{
    internal static void SetFocusDirect(this DrawnComponent component, bool focused)
    {
        component.IsFocused = focused;
    }
}
