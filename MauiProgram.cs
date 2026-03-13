using Microsoft.Extensions.Logging;
using WhiteNoise.Services;
using WhiteNoise.ViewModels;
using WhiteNoise.Views;

namespace WhiteNoise;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",    "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf",   "OpenSansSemiBold");
            });

        // ── Dependency Injection ─────────────────────────────────────────────
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<AudioViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
