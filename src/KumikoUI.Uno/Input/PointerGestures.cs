using KumikoUI.Core.Rendering;
using Windows.UI;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure pointer and color translation used by the Uno grid host, kept free of any live UI so it can be unit tested.
/// </summary>
internal static class PointerGestures
{
    /// <summary>Maximum gap between two presses that still counts as a double tap.</summary>
    internal const long DoubleTapThresholdMs = 400;

    /// <summary>Maximum distance in pixels between two presses that still counts as a double tap.</summary>
    internal const float DoubleTapDistanceThreshold = 20f;

    /// <summary>WinUI pointer timestamps are in microseconds; Core wants milliseconds.</summary>
    internal static long ToMillis(ulong timestampMicroseconds) => (long)(timestampMicroseconds / 1000);

    /// <summary>Converts a WinUI color to a Core color, keeping null as null.</summary>
    internal static KumikoColor? ToKumikoColor(Color? color)
        => color is { } c ? new KumikoColor(c.R, c.G, c.B, c.A) : null;

    /// <summary>
    /// Detects double taps on press: a press within <see cref="DoubleTapThresholdMs"/> and
    /// <see cref="DoubleTapDistanceThreshold"/> of the previous one reports a click count of 2.
    /// </summary>
    internal struct TapTracker
    {
        private long _lastTapTimeMs;
        private float _lastTapX;
        private float _lastTapY;

        /// <summary>Records a press and returns its click count (1 or 2).</summary>
        /// <param name="nowMs">Press time in milliseconds.</param>
        /// <param name="x">Press X position.</param>
        /// <param name="y">Press Y position.</param>
        internal int RegisterPress(long nowMs, float x, float y)
        {
            int clickCount = 1;
            float dx = x - _lastTapX;
            float dy = y - _lastTapY;
            if (nowMs - _lastTapTimeMs < DoubleTapThresholdMs &&
                MathF.Sqrt(dx * dx + dy * dy) < DoubleTapDistanceThreshold)
            {
                clickCount = 2;
                _lastTapTimeMs = 0; // reset so a third tap doesn't also count as double
            }
            else
            {
                _lastTapTimeMs = nowMs;
            }
            _lastTapX = x;
            _lastTapY = y;
            return clickCount;
        }
    }
}
