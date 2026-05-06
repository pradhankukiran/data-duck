using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataDuck.ViewModels;
using DataDuck.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DataDuck;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureViewModels(services);
        ConfigureSharedServices(services);
        Configure(services);

        Services = services.BuildServiceProvider();

        var mainVm = Services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureViewModels(IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<FileListViewModel>();
        services.AddSingleton<SqlEditorViewModel>();
        services.AddSingleton<ResultsViewModel>();
        services.AddSingleton<QueryHistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }

    private static void ConfigureSharedServices(IServiceCollection services)
    {
        // Cross-platform service implementations land here in later phases.
    }

    /// <summary>
    /// Override hook so each platform head (Browser, Desktop) can register its own
    /// implementations of IDuckDbService, IAiService, ILocalStore, IFileService.
    /// </summary>
    private static void Configure(IServiceCollection services)
    {
        Configurator?.Invoke(services);
    }

    public static Action<IServiceCollection>? Configurator { get; set; }
}
