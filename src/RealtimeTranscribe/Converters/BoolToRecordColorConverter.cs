using System.Globalization;

namespace RealtimeTranscribe.Converters;

/// <summary>
/// Returns Apple System Red when recording is active (<c>true</c>),
/// otherwise Apple System Blue — consistent with the HIG colour palette.
/// </summary>
public class BoolToRecordColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#FF3B30")   // Apple System Red
            : Color.FromArgb("#007AFF");   // Apple System Blue

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
