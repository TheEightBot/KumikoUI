using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace SampleApp.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = SoftInput.AdjustResize, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// AdjustResize shrinks the window above the soft keyboard so the cell being edited stays visible (docs/TEXT-INPUT.md#android).
public class MainActivity : MauiAppCompatActivity
{
}
