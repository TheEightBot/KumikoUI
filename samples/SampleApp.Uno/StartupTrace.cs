using System.Diagnostics;

namespace SampleApp.Uno;

/// <summary>
/// Startup markers for cold-start measurements (#278). Each <see cref="Mark"/> writes one logcat line with the tag
/// <c>KumikoStartup</c> and the milliseconds since the process started, so <c>adb logcat -s KumikoStartup:I</c> shows
/// where the time goes. Only the Android head writes anything.
/// </summary>
internal static class StartupTrace
{
    /// <summary>Logs <paramref name="name"/> with the time since the process started.</summary>
    /// <param name="name">The marker name, such as <c>OnLaunched start</c>.</param>
    [Conditional("__ANDROID__")]
    public static void Mark(string name)
    {
#if __ANDROID__
        var sinceProcessStart = Android.OS.SystemClock.UptimeMillis() - Android.OS.Process.StartUptimeMillis;
        Android.Util.Log.Info("KumikoStartup", $"{name} +{sinceProcessStart} ms");
#endif
    }
}
