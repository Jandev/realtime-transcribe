using CommunityToolkit.Mvvm.ComponentModel;
using RealtimeTranscribe.Models;

namespace RealtimeTranscribe.ViewModels;

/// <summary>
/// Observable wrapper around <see cref="TranscriptionFile"/> that adds UI-only state
/// such as inline-rename editing.
/// </summary>
public partial class TranscriptionFileItem : ObservableObject
{
    public TranscriptionFileItem(TranscriptionFile file)
    {
        DisplayName = file.DisplayName;
        FilePath = file.FilePath;
        LastModified = file.LastModified;
        FileNameStem = Path.GetFileNameWithoutExtension(file.FilePath);
        DateKey = file.LastModified.ToString("yyyyMMdd");
    }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _filePath;

    public DateTime LastModified { get; private set; }

    /// <summary>Filename without the .md extension — used as the editable rename value.</summary>
    [ObservableProperty]
    private string _fileNameStem;

    /// <summary>Date key used for grouping (yyyyMMdd).</summary>
    public string DateKey { get; private set; }

    /// <summary>Whether the user is currently renaming this item inline.</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>The text shown in the rename Entry while editing.</summary>
    [ObservableProperty]
    private string _editName = string.Empty;

    /// <summary>Enters inline-rename mode with the current filename stem pre-filled.</summary>
    public void BeginEdit()
    {
        EditName = FileNameStem;
        IsEditing = true;
    }

    /// <summary>Applies the result of a rename to this item.</summary>
    public void ApplyRename(TranscriptionFile updated)
    {
        DisplayName = updated.DisplayName;
        FilePath = updated.FilePath;
        LastModified = updated.LastModified;
        FileNameStem = Path.GetFileNameWithoutExtension(updated.FilePath);
        DateKey = updated.LastModified.ToString("yyyyMMdd");
        IsEditing = false;
    }

    /// <summary>Cancels rename mode without changes.</summary>
    public void CancelEdit()
    {
        IsEditing = false;
    }
}
