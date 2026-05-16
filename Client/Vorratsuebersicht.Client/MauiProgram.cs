using Microsoft.Extensions.Logging;
using Vorratsuebersicht.Client.Services;
using Vorratsuebersicht.Client.Pages;

namespace Vorratsuebersicht.Client;

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

        builder.Services.AddSingleton<LocalDatabase>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ArticlesPage>();
        builder.Services.AddTransient<StoragePage>();
        builder.Services.AddTransient<ShoppingPage>();
        builder.Services.AddTransient<SyncPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
