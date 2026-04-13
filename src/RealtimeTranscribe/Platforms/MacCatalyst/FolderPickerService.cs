using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace RealtimeTranscribe.Services;

/// <summary>
/// MacCatalyst implementation of <see cref="IFolderPickerService"/>.
/// Presents <see cref="UIDocumentPickerViewController"/> for folder selection and
/// persists a security-scoped bookmark so the sandboxed app can regain read/write
/// access to the chosen folder after an app restart without requiring the user to
/// re-pick.
/// </summary>
public sealed class FolderPickerService : IFolderPickerService
{
    // Versioned key so a stale bookmark from an older format never causes a crash on restore.
    private const string BookmarkPrefsKey = "OutputFolderBookmark_v1";

    /// <inheritdoc/>
    public Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // UIKit work must be on the main thread.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var picker = new UIDocumentPickerViewController(new[] { UTType.Folder })
            {
                AllowsMultipleSelection = false
            };

            // Keep a strong reference to the delegate so the GC does not collect it
            // before the picker is dismissed.
            var del = new PickerDelegate(tcs);
            picker.Delegate = del;

            Platform.GetCurrentUIViewController()!.PresentViewController(picker, true, null);
        });

        return tcs.Task;
    }

    /// <inheritdoc/>
    public string? TryRestoreAccess()
    {
        var b64 = Preferences.Default.Get(BookmarkPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(b64))
            return null;

        try
        {
            var data = NSData.FromArray(Convert.FromBase64String(b64));

            var url = NSUrl.FromBookmarkData(
                data,
                NSUrlBookmarkResolutionOptions.WithSecurityScope,
                relativeUrl: null,
                bookmarkDataIsStale: out bool isStale,
                error: out NSError? error);

            if (url == null || error != null)
                return null;

            // Re-establish security-scoped access for this session.
            url.StartAccessingSecurityScopedResource();

            // Refresh a stale bookmark while we have access.
            if (isStale)
                PersistBookmark(url);

            return url.Path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a security-scoped bookmark from <paramref name="url"/> and stores it in
    /// <see cref="Preferences"/> so it can be resolved by <see cref="TryRestoreAccess"/>
    /// on the next launch.  Silently ignores failures — the user will simply need to
    /// re-pick the folder if the bookmark cannot be created.
    /// </summary>
    internal static void PersistBookmark(NSUrl url)
    {
        try
        {
            var data = url.CreateBookmarkData(
                NSUrlBookmarkCreationOptions.WithSecurityScope,
                resourceValues: null,
                relativeUrl: null,
                error: out NSError? error);

            if (data != null && error == null)
                Preferences.Default.Set(BookmarkPrefsKey, Convert.ToBase64String(data.ToArray()));
        }
        catch { }
    }

    // ── Inner delegate ──────────────────────────────────────────────────────

    private sealed class PickerDelegate : UIDocumentPickerDelegate
    {
        private readonly TaskCompletionSource<string?> _tcs;

        public PickerDelegate(TaskCompletionSource<string?> tcs) => _tcs = tcs;

        public override void DidPickDocumentsAtUrls(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            var url = urls.FirstOrDefault();
            if (url != null)
            {
                // The URL returned by UIDocumentPickerViewController already has
                // temporary security-scoped access; persist a bookmark from it so
                // the app can regain access after the next launch.
                PersistBookmark(url);
                _tcs.TrySetResult(url.Path);
            }
            else
            {
                _tcs.TrySetResult(null);
            }
        }

        public override void WasCancelled(UIDocumentPickerViewController controller)
            => _tcs.TrySetResult(null);
    }
}
