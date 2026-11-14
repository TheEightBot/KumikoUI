using KumikoUI.Maui;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Make the CJK and icon fonts available to the grid renderer by family name (KumikoFont.Family). The
		// registration is asynchronous and non-fatal: a font that cannot be read is logged and the grid falls
		// back to the system font. This is the recommended fix for garbled CJK text and icon glyphs on Android,
		// where the system font manager does not expose those typefaces.
		_ = KumikoFonts.RegisterFromAppPackageAsync(
		[
			("NotoSansJP", "NotoSansJP-Regular.ttf"),
			("MaterialIcons", "MaterialIcons-Regular.ttf"),
		]);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
