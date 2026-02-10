using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services;
using YoutubeAutomation.Services.Interfaces;
using YoutubeAutomation.ViewModels;

namespace YoutubeAutomation;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        // Initialize logger first
        AppLogger.Initialize();

        // Global exception handlers to prevent silent crashes
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.LogUnhandled(e.Exception, "DispatcherUnhandledException");
        e.Handled = true; // Prevent app from crashing

        MessageBox.Show(
            $"เกิดข้อผิดพลาดที่ไม่คาดคิด:\n\n{e.Exception.Message}\n\n" +
            $"Log file: {AppLogger.LogFilePath}",
            "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            AppLogger.LogUnhandled(ex, $"AppDomain (IsTerminating={e.IsTerminating})");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.LogUnhandled(e.Exception, "UnobservedTaskException");
        e.SetObserved(); // Prevent app from crashing
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Load settings
        var settings = ProjectSettings.Load();

        services.AddSingleton(settings);

        // Register services
        services.AddSingleton<IOpenRouterService, OpenRouterService>();
        services.AddSingleton<IGoogleTtsService, GoogleTtsService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IStableDiffusionService, StableDiffusionService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MultiImageViewModel>();

        // Register Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<MultiImageWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services = _serviceProvider;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }
}
