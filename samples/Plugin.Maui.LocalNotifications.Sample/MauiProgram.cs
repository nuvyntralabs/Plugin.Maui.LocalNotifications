using Microsoft.Extensions.Logging;
using Plugin.Maui.LocalNotifications;

namespace Plugin.Maui.LocalNotifications.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();
        builder.UseMauiApp<App>()
            .UseLocalNotifications(o => { o.AndroidDefaultChannelId = "general"; o.RequestPermissionOnRegister = false; });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
