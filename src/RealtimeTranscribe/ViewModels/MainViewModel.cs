using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.Services;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// ViewModel for <see cref="MainPage"/>.
/// Handles Record / Stop logic, transcription, summarisation, and in-app font scaling.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly ITranscriptionService _transcriptionService;

    private CancellationTokenSource? _cts;

    public MainViewModel(IAudioService audioService, ITranscriptionService transcriptionService)
    {
        _audioService = audioService;
        _transcriptionService = transcriptionService;

        // Restore persisted font size, clamping any out-of-range value to a safe default.
        _contentFontSize = TextScaleService.Restore(
            Preferences.Default.Get(TextScaleService.PreferenceKey, TextScaleService.Default));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    private string _transcript = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    private string _summary = string.Empty;

    /// <summary>Font size used for transcript and summary content areas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeadingFontSize))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseFontSizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseFontSizeCommand))]
    private double _contentFontSize;

    /// <summary>Font size used for section-heading labels (2 units larger than content).</summary>
    public double HeadingFontSize => _contentFontSize + 2.0;

    public string RecordButtonText => IsRecording ? "⏹  Stop Recording" : IsProcessing ? "⏳  Processing…" : "🎙  Start Recording";

    public bool CanRecord => !IsProcessing;

    private bool CanCancel => IsProcessing;

    [RelayCommand(CanExecute = nameof(CanRecord))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopAndProcessAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling…";
    }

    [RelayCommand(CanExecute = nameof(HasTranscript))]
    private async Task CopyTranscriptAsync()
    {
        await Clipboard.Default.SetTextAsync(Transcript);
    }

    [RelayCommand(CanExecute = nameof(HasSummary))]
    private async Task CopySummaryAsync()
    {
        await Clipboard.Default.SetTextAsync(Summary);
    }

    private bool CanIncreaseFontSize => ContentFontSize < TextScaleService.Maximum;
    private bool CanDecreaseFontSize => ContentFontSize > TextScaleService.Minimum;

    [RelayCommand(CanExecute = nameof(CanIncreaseFontSize))]
    private void IncreaseFontSize()
    {
        ContentFontSize = TextScaleService.Increment(ContentFontSize);
        Preferences.Default.Set(TextScaleService.PreferenceKey, ContentFontSize);
    }

    [RelayCommand(CanExecute = nameof(CanDecreaseFontSize))]
    private void DecreaseFontSize()
    {
        ContentFontSize = TextScaleService.Decrement(ContentFontSize);
        Preferences.Default.Set(TextScaleService.PreferenceKey, ContentFontSize);
    }

    private bool HasTranscript => !string.IsNullOrEmpty(Transcript) && !IsRecording && !IsProcessing;
    private bool HasSummary => !string.IsNullOrEmpty(Summary) && !IsRecording && !IsProcessing;

    private async Task StartRecordingAsync()
    {
        try
        {
            var micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                StatusMessage = "Microphone permission denied.";
                return;
            }

            Transcript = string.Empty;
            Summary = string.Empty;

            await _audioService.StartRecordingAsync();
            IsRecording = true;
            StatusMessage = "🔴 Recording…";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error starting recording: {ex.Message}";
        }
    }

    private async Task StopAndProcessAsync()
    {
        IsRecording = false;
        IsProcessing = true;
        StatusMessage = "Stopping recording…";

        _cts = new CancellationTokenSource();

        try
        {
            var wav = await _audioService.StopRecordingAsync();

            if (wav.Length == 0)
            {
                StatusMessage = "No audio captured.";
                return;
            }

            StatusMessage = "Transcribing…";
            Transcript = await _transcriptionService.TranscribeAsync(wav, _cts.Token);

            StatusMessage = "Summarising…";
            Summary = await _transcriptionService.SummarizeAsync(Transcript, _cts.Token);

            StatusMessage = "Done ✓";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
