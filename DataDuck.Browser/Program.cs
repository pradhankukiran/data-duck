using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using DataDuck;
using DataDuck.Browser.Services;
using DataDuck.Services;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.Configurator = ConfigureBrowserServices;

        return BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

    private static void ConfigureBrowserServices(IServiceCollection services)
    {
        services.AddSingleton<IFileService, StorageProviderFileService>();
        services.AddSingleton<ILocalStore, BrowserLocalStore>();
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IAiService, GroqAiService>();
        services.AddSingleton<IDuckDbService, DuckDbBrowserService>();
    }
}
