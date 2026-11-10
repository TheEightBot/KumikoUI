using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// An <see cref="IDrawingContext"/> that draws and records nothing (and allocates nothing), so a test can measure
/// what the managed render loop itself allocates.
/// </summary>
internal sealed class NoOpDrawingContext : IDrawingContext
{
    public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
    public void FillRect(KumikoRect rect, KumikoPaint paint) { }
    public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
    public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { }
    public void DrawText(string text, float x, float y, KumikoPaint paint) { }
    public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true) { }
    public KumikoSize MeasureText(string text, KumikoPaint paint) => new(text.Length * 7f, 14f);
    public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => new(-11, 3, 2);
    public void ClipRect(KumikoRect rect) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, KumikoRect destRect) { }
}
