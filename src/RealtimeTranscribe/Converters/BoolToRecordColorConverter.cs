using System.Globalization;

namespace RealtimeTranscribe.Converters;

/// <summary>
/// Returns a "recording active" red colour when the value is <c>true</c>,
/// otherwise the primary brand colour.
/// </summary>
public class BoolToRecordColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Colors.Red : Color.FromArgb("#512BD4");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
