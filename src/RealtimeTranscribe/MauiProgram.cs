using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using RealtimeTranscribe.ViewModels;

namespace RealtimeTranscribe;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => { });

        // ------------------------------------------------------------------
        // Configuration – appsettings.json embedded as a MAUI raw asset
        // ------------------------------------------------------------------
        using var configStream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(configStream)
            .Build();

        // Allow user-secrets override at development time
        builder.Configuration.AddConfiguration(configuration);

        // Bind strongly-typed settings
        var azureSettings = new AzureOpenAISettings();
        builder.Configuration.GetSection("AzureOpenAI").Bind(azureSettings);

        // Override with persisted Preferences (set via Settings UI)
        // Keys must match those used in SettingsViewModel
        azureSettings.Endpoint = Preferences.Default.Get("Endpoint", azureSettings.Endpoint);
        azureSettings.ApiKey = Preferences.Default.Get("ApiKey", azureSettings.ApiKey);
        azureSettings.WhisperDeploymentName = Preferences.Default.Get("WhisperDeployment", azureSettings.WhisperDeploymentName);
        azureSettings.ChatDeploymentName = Preferences.Default.Get("ChatDeployment", azureSettings.ChatDeploymentName);

        // Register as singleton so SettingsViewModel can mutate it at runtime
        builder.Services.AddSingleton(azureSettings);

        // ------------------------------------------------------------------
        // Services
        // ------------------------------------------------------------------
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
        builder.Services.AddSingleton<ITranscriptionService, TranscriptionService>();
        builder.Services.AddSingleton<IMarkdownProcessor, MarkdownProcessor>();

        // ------------------------------------------------------------------
        // ViewModels
        // ------------------------------------------------------------------
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ------------------------------------------------------------------
        // Pages
        // ------------------------------------------------------------------
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
