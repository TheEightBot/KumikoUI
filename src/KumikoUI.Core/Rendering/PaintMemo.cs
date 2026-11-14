namespace KumikoUI.Core.Rendering;

/// <summary>
/// A small memo of <see cref="KumikoPaint"/> instances for a drawn component: asking for the same values again
/// returns the same instance, so a component that draws every frame creates its paints once (and again only when a
/// color, font or style it draws with changes), instead of one object per draw call per frame.
/// </summary>
/// <remarks>
/// The paints are shared and never modified after they are handed out, so a drawing context or a recording may keep
/// one. A bounded ring: when more than <see cref="Capacity"/> distinct paints are asked for, the oldest is dropped.
/// </remarks>
internal sealed class PaintMemo
{
    /// <summary>Most distinct paints kept.</summary>
    public const int Capacity = 48;

    private readonly KumikoPaint?[] _paints = new KumikoPaint?[Capacity];
    private int _count;
    private int _next;

    /// <summary>Returns the paint with these values (the defaults of <see cref="KumikoPaint"/> for what is left out), creating it the first time.</summary>
    public KumikoPaint Get(KumikoColor? color = null, float strokeWidth = 1f, KumikoFont? font = null,
        bool isAntiAlias = true)
    {
        var c = color ?? KumikoColor.Black;
        for (int i = 0; i < _count; i++)
        {
            var p = _paints[i]!;
            if (p.Color.Equals(c) && p.StrokeWidth == strokeWidth && p.IsAntiAlias == isAntiAlias
                && Nullable.Equals(p.Font, font))
                return p;
        }

        var created = new KumikoPaint { Color = c, StrokeWidth = strokeWidth, Font = font, IsAntiAlias = isAntiAlias };
        if (_count < Capacity)
            _paints[_count++] = created;
        else
            _paints[_next++ % Capacity] = created;
        return created;
    }
}
