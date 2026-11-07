using Microsoft.Extensions.Logging;

namespace SaveToBlob;

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

        // ⬇️ These four lines are what's missing in your build
        builder.Services.AddSingleton<Services.IBlobStorageSettingsService, Services.BlobStorageSettingsService>();
        builder.Services.AddSingleton<Services.IZipService, Services.ZipService>();
        builder.Services.AddSingleton<Services.IBlobUploadService, Services.BlobUploadService>();
        builder.Services.AddTransient<ViewModels.MainViewModel>();

        return builder.Build();
    }
}