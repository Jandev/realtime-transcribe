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

> **macOS Sequoia (15) and later — including Tahoe (26.x):** the right-click → **Open** shortcut that bypassed Gatekeeper in older macOS versions no longer works. Use one of the two methods below instead.

### Method 1 — System Settings (recommended)

1. Attempt to open `RealtimeTranscribe.app`. macOS will block it and show a warning.
2. Open **System Settings → Privacy & Security** and scroll down to the **Security** section.
3. You will see a message about the blocked app with an **Open Anyway** button. Click it.
4. Confirm in the dialog that appears (you may be asked for your administrator password).

macOS remembers this choice; subsequent launches will not prompt again.

### Method 2 — Terminal (quickest)

Run the following command to remove the quarantine attribute Apple attaches to downloaded files:

```bash
xattr -dr com.apple.quarantine /path/to/RealtimeTranscribe.app
```

After that, open the app normally.

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
