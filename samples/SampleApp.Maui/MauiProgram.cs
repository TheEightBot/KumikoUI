using KumikoUI.Maui;
using Microsoft.Extensions.Logging;

namespace SampleApp.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseKumikoUI()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

#if KUMIKO_DEVFLOW
		// .NET MAUI DevFlow agent plus the sample's KumikoUI extension (DevFlow/KumikoDevFlowExtension.cs), Debug
		// builds only (SampleApp.Maui.csproj): eng/devflow drives and inspects the sample through them (#262).
		builder.AddKumikoDevFlow();
#endif

		return builder.Build();
	}
}
