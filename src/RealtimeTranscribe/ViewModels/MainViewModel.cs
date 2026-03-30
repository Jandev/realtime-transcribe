using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.Services;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// ViewModel for <see cref="MainPage"/>.
/// Handles Record / Stop logic, realtime transcription and summarisation.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ITranscriptionScheduler _transcriptionScheduler;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _schedulerCts;
    private Task? _schedulerTask;

    // Accumulates transcript segments produced by the scheduler and the final chunk.
    private readonly List<string> _transcriptSegments = new();

    public MainViewModel(
        IAudioService audioService,
        ITranscriptionService transcriptionService,
        ITranscriptionScheduler transcriptionScheduler)
    {
        _audioService = audioService;
        _transcriptionService = transcriptionService;
        _transcriptionScheduler = transcriptionScheduler;
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

            _transcriptSegments.Clear();
            Transcript = string.Empty;
            Summary = string.Empty;

            await _audioService.StartRecordingAsync();
            IsRecording = true;
            StatusMessage = "🔴 Recording…";

            // Start the realtime transcription scheduler in the background.
            _schedulerCts = new CancellationTokenSource();
            _schedulerTask = RunSchedulerAsync(_schedulerCts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error starting recording: {ex.Message}";
        }
    }

    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        await _transcriptionScheduler.RunAsync(
            audioChunkProvider: ct => _audioService.GetCurrentChunkAsync(),
            onSegment: async segment =>
            {
                _transcriptSegments.Add(segment);
                var combined = string.Join(" ", _transcriptSegments);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Transcript = combined;
                    StatusMessage = $"🔴 Recording… (updated {DateTime.Now:HH:mm:ss})";
                });
            },
            cancellationToken: cancellationToken);
    }

    private async Task StopAndProcessAsync()
    {
        // Stop the realtime scheduler immediately.
        _schedulerCts?.Cancel();

        IsRecording = false;
        IsProcessing = true;
        StatusMessage = "Stopping recording…";

        _cts = new CancellationTokenSource();

        try
        {
            // Wait for the scheduler loop to exit cleanly.
            if (_schedulerTask is not null)
            {
                try { await _schedulerTask; }
                catch (OperationCanceledException) { }
                _schedulerTask = null;
            }

            // Capture whatever audio remains after the last scheduled chunk.
            var wav = await _audioService.StopRecordingAsync();

            if (wav.Length == 0 && _transcriptSegments.Count == 0)
            {
                StatusMessage = "No audio captured.";
                return;
            }

            // Transcribe the final audio segment (if any).
            if (wav.Length > 0)
            {
                StatusMessage = "Transcribing final segment…";
                var finalSegment = await _transcriptionService.TranscribeAsync(wav, _cts.Token);
                if (!string.IsNullOrEmpty(finalSegment))
                    _transcriptSegments.Add(finalSegment);
            }

            Transcript = string.Join(" ", _transcriptSegments);

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
            _schedulerCts?.Dispose();
            _schedulerCts = null;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
