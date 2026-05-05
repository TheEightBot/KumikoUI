using KumikoUI.Maui;
using KumikoUI.SkiaSharp;
using Microsoft.Extensions.Logging;

namespace SampleApp.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaKumikoUI()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register custom SkiaSharp typefaces for use in the grid renderer.
		// Fonts placed in Resources/Raw/ can be loaded here and made available
		// to DataGridStyle via GridFont.Family.  This is the recommended fix for
		// garbled CJK text and icon-font glyphs on Android, where the system font
		// manager does not expose Japanese or icon-font typefaces.
		RegisterSkiaFonts();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>
	/// Loads custom font files from the app bundle and registers them with
	/// <see cref="SkiaFontRegistrar"/> so <see cref="KumikoUI.SkiaSharp.SkiaDrawingContext"/>
	/// can resolve them by family name.
	/// </summary>
	private static void RegisterSkiaFonts()
	{
		// Noto Sans JP — covers Japanese, Chinese and Korean characters.
		// Family name "NotoSansJP" can be used in DataGridStyle.HeaderFont / CellFont.
		TryRegisterFont("NotoSansJP", "NotoSansJP-Regular.ttf");

		// Material Icons — icon glyph font. Use the Unicode code points (e.g. "\uE88A")
		// as cell values and set the column font family to "MaterialIcons".
		TryRegisterFont("MaterialIcons", "MaterialIcons-Regular.ttf");
	}

	private static void TryRegisterFont(string family, string assetFileName)
	{
		try
		{
			// Blocking on async file I/O is safe here: MauiProgram.CreateMauiApp() runs
			// before the MAUI UI message-loop starts, so there is no running
			// synchronization context that could deadlock on `.GetAwaiter().GetResult()`.
			using var stream = FileSystem.OpenAppPackageFileAsync(assetFileName)
				.GetAwaiter().GetResult();
			SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
		}
		catch (Exception ex)
		{
			// Non-fatal — the grid falls back to the system default font.
			System.Diagnostics.Debug.WriteLine(
				$"[KumikoUI] Could not register font '{family}' from '{assetFileName}': {ex.Message}");
		}
	}
}
