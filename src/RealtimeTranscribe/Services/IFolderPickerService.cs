namespace RealtimeTranscribe.Services;

/// <summary>
/// Presents the OS folder picker and persists security-scoped bookmark data so the
/// sandboxed app can regain access to the chosen folder after an app restart.
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Presents the native folder picker.  Returns the chosen path, or <c>null</c> when
    /// the user cancels.  On MacCatalyst, also stores a security-scoped bookmark so that
    /// <see cref="TryRestoreAccess"/> can re-establish access on future launches.
    /// </summary>
    Task<string?> PickFolderAsync();

    /// <summary>
    /// Attempts to restore security-scoped access to the previously-chosen folder by
    /// resolving a persisted bookmark.  Returns the folder path on success, or
    /// <c>null</c> when no bookmark is stored or bookmark resolution fails (the user will
    /// need to re-pick the folder in that case).
    /// </summary>
    string? TryRestoreAccess();
}
