namespace KumikoUI.Core.Models;

/// <summary>
/// When the grid draws a scroll bar (<see cref="KumikoStyle.VerticalScrollBarVisibility"/>,
/// <see cref="KumikoStyle.HorizontalScrollBarVisibility"/>). A bar is drawn only while the content overflows the
/// viewport in its direction, whatever the setting.
/// </summary>
public enum KumikoScrollBarVisibility
{
    /// <summary>
    /// An overlay that appears while the grid scrolls and while a mouse or pen rests over the bar's edge, then fades
    /// out. The default. It needs a connected <see cref="Input.KumikoInputController"/> whose frame loop runs (both
    /// hosts have one); a renderer drawn without a controller draws no <see cref="Auto"/> bar.
    /// </summary>
    Auto,

    /// <summary>The bar is drawn on every frame.</summary>
    Always,

    /// <summary>No bar is drawn, and the bar's edge is not a pointer target.</summary>
    Never,
}
