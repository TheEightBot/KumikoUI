using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Hit regions that depend on measured text, published by <see cref="KumikoRenderer"/> as it draws them and read by
/// <see cref="KumikoHitTester"/> (#101, INPUT-12): the grouping panel's chips. The hit tester has no drawing context
/// to measure with, so without these it can only approximate. Reused every frame; allocation-free once warm.
/// </summary>
internal sealed class RenderedHitRegions
{
    private readonly List<RenderedChip> _chips = new();

    /// <summary>The chips drawn in the last frame, left to right, one per group description.</summary>
    public IReadOnlyList<RenderedChip> Chips => _chips;

    /// <summary>The height of the grouping panel the chips were drawn in.</summary>
    public float PanelHeight { get; private set; }

    /// <summary>Starts a frame's grouping panel (or clears it when none is drawn).</summary>
    public void BeginGroupPanel(float panelHeight)
    {
        _chips.Clear();
        PanelHeight = panelHeight;
    }

    /// <summary>Records a chip drawn for <paramref name="group"/>.</summary>
    public void AddChip(GroupDescription group, KumikoRect bounds) => _chips.Add(new RenderedChip(group, bounds));

    /// <summary>
    /// Whether the chips drawn last describe <paramref name="groups"/> as they are now: one chip per description, in
    /// order, in a panel of the same height. False when the groups changed since that frame; the hit tester then
    /// falls back to its estimate.
    /// </summary>
    public bool Matches(IReadOnlyList<GroupDescription> groups, float panelHeight)
    {
        if (_chips.Count != groups.Count || MathF.Abs(PanelHeight - panelHeight) > 0.01f) return false;
        for (int i = 0; i < groups.Count; i++)
        {
            if (!ReferenceEquals(_chips[i].Group, groups[i])) return false;
        }
        return true;
    }
}

/// <summary>A grouping panel chip as drawn: its group and its rectangle (viewport coordinates).</summary>
internal readonly record struct RenderedChip(GroupDescription Group, KumikoRect Bounds);
