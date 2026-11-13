using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>One <see cref="IKumikoIconProvider.DrawIcon"/> call a <see cref="RecordingIconProvider"/> saw.</summary>
/// <param name="Icon">The icon asked for.</param>
/// <param name="Bounds">The slot, in the coordinates the caller drew in (before any translation).</param>
/// <param name="Color">The paint's color.</param>
/// <param name="StrokeWidth">The paint's stroke width.</param>
internal sealed record IconCall(KumikoIcon Icon, KumikoRect Bounds, KumikoColor Color, float StrokeWidth);

/// <summary>
/// An icon provider that draws nothing but records every call, so a test can tell which glyph a slot asked for, in
/// which rectangle and color — and, with a <see cref="RecordingDrawingContext"/>, that nothing else drew a glyph.
/// </summary>
internal sealed class RecordingIconProvider : IKumikoIconProvider
{
    /// <summary>Every call, in call order.</summary>
    public List<IconCall> Calls { get; } = new();

    /// <summary>The icons asked for, in call order.</summary>
    public IEnumerable<KumikoIcon> Icons => Calls.Select(c => c.Icon);

    public void DrawIcon(IDrawingContext ctx, KumikoIcon icon, KumikoRect bounds, KumikoPaint paint) =>
        Calls.Add(new IconCall(icon, bounds, paint.Color, paint.StrokeWidth));
}
