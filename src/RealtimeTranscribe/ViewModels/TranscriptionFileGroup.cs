using System.Collections.ObjectModel;
using System.Globalization;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// Groups <see cref="TranscriptionFileItem"/>s by date for use with
/// <c>CollectionView.IsGrouped</c>.
/// </summary>
public class TranscriptionFileGroup : ObservableCollection<TranscriptionFileItem>
{
    /// <summary>Raw date key in <c>yyyyMMdd</c> format (used for sorting).</summary>
    public string DateKey { get; }

    /// <summary>Human-readable date header shown in the sidebar (e.g. "Mar 15, 2024").</summary>
    public string DateHeader { get; }

    public TranscriptionFileGroup(string dateKey, IEnumerable<TranscriptionFileItem> items)
        : base(items)
    {
        DateKey = dateKey;
        DateHeader = FormatDateHeader(dateKey);
    }

    private static string FormatDateHeader(string dateKey)
    {
        if (DateTime.TryParseExact(dateKey, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("MMM d, yyyy");
        }

        return dateKey;
    }
}
