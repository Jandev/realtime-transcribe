using CommunityToolkit.Mvvm.Input;
using RealtimeTranscribe.ViewModels;

namespace RealtimeTranscribe;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.RefreshFilesCommand is IAsyncRelayCommand asyncCmd)
            _ = asyncCmd.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Transcript))
            ScrollEditorToEnd(TranscriptEditor);
        else if (e.PropertyName == nameof(MainViewModel.DiarizedTranscript))
            ScrollEditorToEnd(DiarizedTranscriptEditor);
    }

    private static void ScrollEditorToEnd(Editor editor)
    {
        var text = editor.Text;
        if (!string.IsNullOrEmpty(text))
            editor.CursorPosition = text.Length;
    }

    private async void OnSummaryWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (sender is not WebView webView)
            return;

        try
        {
            var heightStr = await webView.EvaluateJavaScriptAsync("document.documentElement.scrollHeight");
            if (double.TryParse(heightStr, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var height) && height > 0)
            {
                webView.HeightRequest = Math.Max(height, 120);
            }
        }
        catch
        {
            // Fall back to the minimum height if JavaScript evaluation fails
        }
    }
}
