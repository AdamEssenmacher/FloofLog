using FloofLog.Pages;
using FloofLog.Services;
using FloofLog.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FloofLog;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IPetLogService, PetLogService>();
        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddSingleton<ManagePetsViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ManagePetsPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
