using System;
using System.Net.Http;
using Avalonia;
using DataDuck;
using DataDuck.Desktop.Services;
using DataDuck.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataDuck.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.Configurator = ConfigureDesktopServices;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static void ConfigureDesktopServices(IServiceCollection services)
    {
        services.AddSingleton<IFileService, StorageProviderFileService>();
        services.AddSingleton<ILocalStore, InMemoryLocalStore>();
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IAiService, EnvVarGroqAiService>();
        services.AddSingleton<IDuckDbService, NotSupportedDuckDbService>();
    }
}
