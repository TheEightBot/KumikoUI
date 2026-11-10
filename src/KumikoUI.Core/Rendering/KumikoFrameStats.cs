using System.Diagnostics;
using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// The frame-time overlay's counters (#70): how long <see cref="KumikoRenderer.Render"/> takes, how many frames
/// are drawn per second and how many bytes a frame allocates, averaged over a short window. The text is rebuilt
/// only when a window ends (<see cref="UpdateInterval"/>, four times a second by default), so showing the overlay
/// allocates one short string per window and nothing per frame. The renderer owns one instance and only touches
/// it while <see cref="KumikoRenderer.ShowFrameStats"/> is true.
/// </summary>
public sealed class KumikoFrameStats
{
    private readonly KumikoPaint _textPaint = new()
    {
        Color = new KumikoColor(255, 255, 255),
        Font = new KumikoFont("Default", 12),
    };
    private readonly KumikoPaint _backgroundPaint = new() { Color = new KumikoColor(0, 0, 0, 190) };

    private long _frameStart;
    private long _allocatedAtStart;
    private long _windowStart;
    private int _windowFrames;
    private long _windowRenderTicks;
    private long _windowAllocated;
    private string _text = "";
    private float _textWidth = -1;

    /// <summary>The length of one averaging window. Default: 250 ms.</summary>
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The average time <see cref="KumikoRenderer.Render"/> took over the last window, in milliseconds.</summary>
    public double FrameTimeMs { get; private set; }

    /// <summary>The number of frames drawn per second over the last window.</summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>The average bytes one frame allocated on the rendering thread over the last window.</summary>
    public long AllocatedBytesPerFrame { get; private set; }

    /// <summary>The text the overlay shows, for example <c>0.42 ms | 60 fps | 0 B/frame</c>; empty until the first window ends.</summary>
    public string Text => _text;

    internal void BeginFrame()
    {
        _frameStart = Stopwatch.GetTimestamp();
        _allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        if (_windowStart == 0)
            _windowStart = _frameStart;
    }

    /// <summary>Ends the frame, ahead of the overlay's own drawing so that is not counted.</summary>
    internal void EndFrame()
    {
        long now = Stopwatch.GetTimestamp();
        _windowRenderTicks += now - _frameStart;
        _windowAllocated += GC.GetAllocatedBytesForCurrentThread() - _allocatedAtStart;
        _windowFrames++;

        long window = now - _windowStart;
        if (window >= UpdateInterval.TotalSeconds * Stopwatch.Frequency)
        {
            FrameTimeMs = _windowRenderTicks * 1000.0 / Stopwatch.Frequency / _windowFrames;
            FramesPerSecond = _windowFrames * (double)Stopwatch.Frequency / window;
            AllocatedBytesPerFrame = _windowAllocated / _windowFrames;
            _text = string.Create(CultureInfo.InvariantCulture,
                $"{FrameTimeMs:0.00} ms | {FramesPerSecond:0} fps | {FormatBytes(AllocatedBytesPerFrame)}/frame");
            _textWidth = -1;
            _windowStart = now;
            _windowFrames = 0;
            _windowRenderTicks = 0;
            _windowAllocated = 0;
        }
    }

    private static string FormatBytes(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:0.#} KB";

    /// <summary>Draws the readout in the viewport's top-right corner.</summary>
    internal void Draw(IDrawingContext ctx, float viewportWidth)
    {
        if (_text.Length == 0) return;
        if (_textWidth < 0)
            _textWidth = ctx.MeasureText(_text, _textPaint).Width;

        const float padding = 6f, margin = 4f, height = 22f;
        float width = _textWidth + 2 * padding;
        var box = new KumikoRect(Math.Max(0, viewportWidth - width - margin), margin, width, height);
        ctx.FillRoundRect(box, 4f, _backgroundPaint);
        ctx.DrawTextInRect(_text, box, _textPaint, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center, clip: false);
    }
}
