# Installation

## Table of Contents

- [Download the app](#download-the-app)
- [macOS Gatekeeper (unsigned binary)](#macos-gatekeeper-unsigned-binary)
- [Build from source](#build-from-source)

---

## Download the app

Download the latest `.app` bundle from the [Releases](https://github.com/Jandev/realtime-transcribe/releases) page.

---

## macOS Gatekeeper (unsigned binary)

The released binaries are not code-signed. macOS Gatekeeper will block the app on first launch with a message like *"RealtimeTranscribe.app can't be opened because it is from an unidentified developer."*

**To allow the app to run:**

1. In **Finder**, right-click (or Control-click) `RealtimeTranscribe.app` and choose **Open**.
2. In the dialog that appears, click **Open** to confirm.

macOS remembers this choice; subsequent launches will not prompt again.

Alternatively, after seeing the blocked-app message, open **System Settings → Privacy & Security**, scroll to the *Security* section, and click **Open Anyway** next to the blocked app entry.

> **Terminal alternative:** Run `xattr -dr com.apple.quarantine /path/to/RealtimeTranscribe.app` to remove the quarantine flag.

---

## Build from source

### Prerequisites

- macOS 14 Sonoma or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MAUI workload:

  ```bash
  dotnet workload install maui
  ```

- Xcode (the version required by the installed Apple workloads). Select it for command-line builds:

  ```bash
  sudo xcode-select --switch /Applications/Xcode.app
  sudo xcodebuild -runFirstLaunch
  sudo xcodebuild -license accept
  ```

### Run

```bash
cd src/RealtimeTranscribe
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

### Build a .app bundle

```bash
cd src/RealtimeTranscribe
dotnet publish -f net10.0-maccatalyst -c Release
# Output: bin/Release/net10.0-maccatalyst/publish/RealtimeTranscribe.app
```
