using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// ViewModel for <see cref="SettingsPage"/>.
/// Allows the user to configure the Azure OpenAI endpoint and API key at runtime,
/// as well as the folder where summary files are saved to disk.
/// Values are persisted using <see cref="Preferences"/> so they survive app restarts.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AzureOpenAISettings _settings;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IFileStorageService _fileStorageService;

    public SettingsViewModel(AzureOpenAISettings settings, ITranscriptionService transcriptionService, IFileStorageService fileStorageService)
    {
        _settings = settings;
        _transcriptionService = transcriptionService;
        _fileStorageService = fileStorageService;

        // Load persisted preferences; fall back to values from appsettings.json
        _endpoint = Preferences.Default.Get(nameof(Endpoint), _settings.Endpoint);
        _apiKey = Preferences.Default.Get(nameof(ApiKey), _settings.ApiKey);
        _whisperDeployment = Preferences.Default.Get(nameof(WhisperDeployment), _settings.WhisperDeploymentName);
        _chatDeployment = Preferences.Default.Get(nameof(ChatDeployment), _settings.ChatDeploymentName);
        _outputFolder = Preferences.Default.Get(nameof(OutputFolder), string.Empty);
    }

    [ObservableProperty]
    private string _endpoint;

    [ObservableProperty]
    private string _apiKey;

    [ObservableProperty]
    private string _whisperDeployment;

    [ObservableProperty]
    private string _chatDeployment;

    [ObservableProperty]
    private string _outputFolder;

    [ObservableProperty]
    private string _saveStatus = string.Empty;

    [RelayCommand]
    private async Task BrowseOutputFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (result.IsSuccessful)
            OutputFolder = result.Folder.Path;
    }

    [RelayCommand]
    private void Save()
    {
        Preferences.Default.Set(nameof(Endpoint), Endpoint);
        Preferences.Default.Set(nameof(ApiKey), ApiKey);
        Preferences.Default.Set(nameof(WhisperDeployment), WhisperDeployment);
        Preferences.Default.Set(nameof(ChatDeployment), ChatDeployment);
        Preferences.Default.Set(nameof(OutputFolder), OutputFolder);

        // Update the shared settings object so the TranscriptionService picks up the new values
        _settings.Endpoint = Endpoint;
        _settings.ApiKey = ApiKey;
        _settings.WhisperDeploymentName = WhisperDeployment;
        _settings.ChatDeploymentName = ChatDeployment;

        // Update the file storage service so it uses the new folder immediately
        _fileStorageService.OutputFolder = OutputFolder;

        SaveStatus = "Settings saved ✓";
    }
}
