using RealtimeTranscribe.ViewModels;

namespace RealtimeTranscribe;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
