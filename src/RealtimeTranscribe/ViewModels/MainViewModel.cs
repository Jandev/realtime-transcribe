using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.Models;
using RealtimeTranscribe.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// ViewModel for <see cref="MainPage"/>.
/// Handles Record / Stop logic, realtime transcription, summarisation, and in-app font scaling.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ITranscriptionScheduler _transcriptionScheduler;
    private readonly IMarkdownProcessor _markdownProcessor;
    private readonly IFileStorageService _fileStorageService;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _schedulerCts;
    private Task? _schedulerTask;

    // Accumulates transcript segments produced by the scheduler and the final chunk.
    private readonly List<string> _transcriptSegments = new();

    public MainViewModel(
        IAudioService audioService,
        ITranscriptionService transcriptionService,
        ITranscriptionScheduler transcriptionScheduler,
        IMarkdownProcessor markdownProcessor,
        IFileStorageService fileStorageService)
    {
        _audioService = audioService;
        _transcriptionService = transcriptionService;
        _transcriptionScheduler = transcriptionScheduler;
        _markdownProcessor = markdownProcessor;
        _fileStorageService = fileStorageService;

        // React when a Bluetooth / wireless input device disconnects mid-recording.
        _audioService.RecordingInterrupted += OnRecordingInterrupted;

        // React when the user changes the selected input or output recording device.
        _audioService.DeviceSelectionChanged += OnDeviceSelectionChanged;

        // Restore persisted font size, clamping any out-of-range value to a safe default.
        _contentFontSize = TextScaleService.Restore(
            Preferences.Default.Get(TextScaleService.PreferenceKey, TextScaleService.Default));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyDiarizedTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyDiarizedTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyTranscriptCommand))]
    private string _transcript = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyDiarizedTranscriptCommand))]
    private string _diarizedTranscript = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryHtml))]
    [NotifyCanExecuteChangedFor(nameof(CopySummaryCommand))]
    private string _summary = string.Empty;

    /// <summary>
    /// Rendered HTML for the summary, derived from <see cref="Summary"/>.
    /// Contains a full HTML document with embedded CSS styling.
    /// Returns placeholder HTML when no summary is available.
    /// </summary>
    public string SummaryHtml =>
        string.IsNullOrEmpty(Summary)
            ? WrapHtml("<p style=\"color: #9a9a9a; font-style: italic;\">Summary and action items will appear here…</p>")
            : WrapHtml(_markdownProcessor.ToHtml(Summary));

    private static string WrapHtml(string bodyContent) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                    font-size: 14px;
                    line-height: 1.6;
                    margin: 0;
                    padding: 8px 12px;
                    color: #1a1a1a;
                    background: transparent;
                }
                h1, h2, h3, h4 { margin-top: 12px; margin-bottom: 4px; }
                ul, ol { padding-left: 20px; margin: 4px 0; }
                li { margin: 2px 0; }
                table { border-collapse: collapse; width: 100%; margin: 8px 0; }
                th, td { border: 1px solid #ccc; padding: 4px 8px; text-align: left; }
                th { background: #f5f5f5; font-weight: 600; }
                p { margin: 4px 0; }
                @media (prefers-color-scheme: dark) {
                    body { color: #e0e0e0; }
                    th { background: #2d2d2d; }
                    th, td { border-color: #555; }
                }
            </style>
        </head>
        <body>{{bodyContent}}</body>
        </html>
        """;

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

    [RelayCommand(CanExecute = nameof(HasDiarizedTranscript))]
    private async Task CopyDiarizedTranscriptAsync()
    {
        await Clipboard.Default.SetTextAsync(DiarizedTranscript);
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
    private bool HasDiarizedTranscript => !string.IsNullOrEmpty(DiarizedTranscript) && !IsRecording && !IsProcessing;
    private bool HasSummary => !string.IsNullOrEmpty(Summary) && !IsRecording && !IsProcessing;

    // ── Sidebar: saved transcription files ──────────────────────────────

    /// <summary>All files from the service — cached for filtering.</summary>
    private IReadOnlyList<TranscriptionFile> _allFiles = Array.Empty<TranscriptionFile>();

    /// <summary>Grouped and filtered transcription files shown in the sidebar.</summary>
    public ObservableCollection<TranscriptionFileGroup> GroupedTranscriptionFiles { get; } = new();

    [ObservableProperty]
    private TranscriptionFileItem? _selectedTranscriptionFile;

    [ObservableProperty]
    private string _filterText = string.Empty;

    partial void OnFilterTextChanged(string value) => RebuildGroupedFiles();

    partial void OnSelectedTranscriptionFileChanged(TranscriptionFileItem? value)
    {
        if (value is not null)
            _ = LoadTranscriptionFileAsync(value);
    }

    [RelayCommand]
    private async Task RefreshFilesAsync()
    {
        try
        {
            _allFiles = await _fileStorageService.ListSummariesAsync();
            RebuildGroupedFiles();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Error refreshing files: {ex}");
        }
    }

    /// <summary>Applies <see cref="FilterText"/> and groups the cached file list by date.</summary>
    private void RebuildGroupedFiles()
    {
        var items = _allFiles.Select(f => new TranscriptionFileItem(f));

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            items = items.Where(item =>
                item.FileNameStem.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                || item.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        var groups = items
            .GroupBy(item => item.DateKey)
            .OrderByDescending(g => g.Key)
            .Select(g => new TranscriptionFileGroup(g.Key, g));

        GroupedTranscriptionFiles.Clear();
        foreach (var g in groups)
            GroupedTranscriptionFiles.Add(g);
    }

    /// <summary>
    /// Commits an inline rename for the given item.  Called from the code-behind
    /// when the user presses Enter or the Entry loses focus.
    /// </summary>
    [RelayCommand]
    private async Task CommitRenameAsync(TranscriptionFileItem? item)
    {
        if (item is null || !item.IsEditing)
            return;

        var newName = item.EditName?.Trim();

        // No change or empty — cancel.
        if (string.IsNullOrEmpty(newName) || newName == item.FileNameStem)
        {
            item.CancelEdit();
            return;
        }

        try
        {
            var updated = await _fileStorageService.RenameSummaryAsync(item.FilePath, newName);
            item.ApplyRename(updated);
            // Refresh the full list to keep grouping/sorting correct.
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rename failed: {ex.Message}";
            item.CancelEdit();
        }
    }

    private async Task LoadTranscriptionFileAsync(TranscriptionFileItem file)
    {
        try
        {
            StatusMessage = $"Loading {file.DisplayName}…";
            var content = await _fileStorageService.LoadTranscriptionAsync(file.FilePath);
            Summary = content.Summary;
            Transcript = content.Transcript;
            DiarizedTranscript = content.DiarizedTranscript;
            StatusMessage = $"Loaded: {file.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading file: {ex.Message}";
        }
    }

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
            DiarizedTranscript = string.Empty;
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

    // Delay (ms) between each character when animating new transcript text into the UI.
    private const int TranscriptAnimationDelayMs = 20;

    // Timeout (seconds) for transcribing buffered audio when switching devices mid-recording.
    private const int DeviceSwitchTranscriptionTimeoutSeconds = 30;

    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        await _transcriptionScheduler.RunAsync(
            audioChunkProvider: ct => _audioService.GetCurrentChunkAsync(),
            onSegment: async segment =>
            {
                _transcriptSegments.Add(segment);

                await AppendToTranscriptAsync(segment, cancellationToken);

                await MainThread.InvokeOnMainThreadAsync(() =>
                    StatusMessage = $"🔴 Recording… (updated {DateTime.Now:HH:mm:ss})");
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Appends <paramref name="segment"/> to <see cref="Transcript"/> one character at a time
    /// to produce a visually fluent "streaming" effect.  Must be called from a background thread;
    /// all UI updates are dispatched to the main thread.  Respects <paramref name="ct"/> on every
    /// inter-character delay so cancellation stops the animation immediately.
    /// </summary>
    private async Task AppendToTranscriptAsync(string segment, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(segment))
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var prefix = string.IsNullOrEmpty(Transcript) ? string.Empty : " ";
            var textToAppend = prefix + segment;
            var sb = new StringBuilder(Transcript);

            foreach (char c in textToAppend)
            {
                if (ct.IsCancellationRequested)
                    return;

                sb.Append(c);
                Transcript = sb.ToString();

                try
                {
                    await Task.Delay(TranscriptAnimationDelayMs, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });
    }

    /// <summary>
    /// Called when the audio input device becomes unavailable during an active recording
    /// (e.g. AirPods placed back in their case). Dispatches to the UI thread and triggers
    /// a graceful stop so any captured audio is still processed.
    /// </summary>
    private void OnRecordingInterrupted(object? sender, EventArgs e)
    {
        if (!IsRecording)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                StatusMessage = "⚠️ Audio device disconnected — saving recording…";
                await StopAndProcessAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error handling device disconnection: {ex.Message}";
                IsProcessing = false;
            }
        });
    }

    /// <summary>
    /// Called when the user changes the selected input or output recording device.
    /// If recording is in progress, the current session is stopped, the buffered audio is
    /// captured and transcribed into the running transcript, and then recording restarts
    /// immediately on the newly-selected device.
    /// </summary>
    private void OnDeviceSelectionChanged(object? sender, EventArgs e)
    {
        if (!IsRecording)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                StatusMessage = "🔄 Device changed — restarting recording…";
                await StopAndRestartRecordingAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restarting recording: {ex.Message}";
                IsRecording = false;
            }
        });
    }

    /// <summary>
    /// Stops the current recording session, captures and transcribes any buffered audio,
    /// then immediately restarts recording on the newly-selected device.
    /// Unlike <see cref="StopAndProcessAsync"/>, this method does not run speaker
    /// diarisation or summarisation — those are deferred until the user stops recording.
    /// </summary>
    private async Task StopAndRestartRecordingAsync()
    {
        // Stop the realtime scheduler.
        _schedulerCts?.Cancel();

        StatusMessage = "🔄 Capturing audio before device switch…";

        try
        {
            if (_schedulerTask is not null)
            {
                try { await _schedulerTask; }
                catch (OperationCanceledException) { }
                _schedulerTask = null;
            }

            // Retrieve whatever audio was buffered before the device change.
            var wav = await _audioService.StopRecordingAsync();
            IsRecording = false;

            if (wav.Length > 0)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DeviceSwitchTranscriptionTimeoutSeconds));
                StatusMessage = "🔄 Transcribing buffered audio…";
                var segment = await _transcriptionService.TranscribeAsync(wav, cts.Token);
                if (!string.IsNullOrEmpty(segment))
                {
                    _transcriptSegments.Add(segment);
                    await AppendToTranscriptAsync(segment, cts.Token);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Error capturing audio before device switch: {ex}");
        }
        finally
        {
            _schedulerCts?.Dispose();
            _schedulerCts = null;
        }

        // Restart recording on the newly-selected device.
        await _audioService.StartRecordingAsync();
        IsRecording = true;
        StatusMessage = "🔴 Recording…";

        _schedulerCts = new CancellationTokenSource();
        _schedulerTask = RunSchedulerAsync(_schedulerCts.Token);
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

            StatusMessage = "Identifying speakers…";
            DiarizedTranscript = await _transcriptionService.DiarizeAsync(Transcript, _cts.Token);

            StatusMessage = "Summarising…";
            await _transcriptionService.SummarizeStreamingAsync(
                Transcript,
                onToken: async token =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() => Summary += token);
                },
                _cts.Token);

            if (!string.IsNullOrEmpty(Summary) || !string.IsNullOrEmpty(Transcript) || !string.IsNullOrEmpty(DiarizedTranscript))
            {
                await _fileStorageService.SaveTranscriptionAsync(Summary, Transcript, DiarizedTranscript, DateTime.Now, _cts.Token);
                await RefreshFilesAsync();
            }

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
