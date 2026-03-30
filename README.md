# Realtime Transcribe

A .NET MAUI macOS app that records meeting audio, transcribes it with **Azure AI Foundry Whisper**, and generates a concise summary with action items using **Azure OpenAI GPT-4o**.

Supports Dutch 🇳🇱 and English 🇬🇧 automatically (Whisper auto-detects language; the summary prompt responds in the same language).

---

## Screenshots

![Realtime Transcribe app showing a successful Dutch transcription and generated summary](docs/images/first-transcription-in-dutch-screenshot.png)

---

## Features

| Feature               | Details                                                            |
| --------------------- | ------------------------------------------------------------------ |
| 🎙 Recording          | Microphone capture (16 kHz mono WAV via Plugin.Maui.Audio)         |
| 🔊 Full-audio capture | Optional BlackHole loopback (see guide below)                      |
| 📝 Transcription      | Azure AI Foundry Whisper large-v3                                  |
| 🤖 Summary            | GPT-4o-mini; concise 3-sentence summary + bullet action items      |
| 🌍 Languages          | Dutch & English auto-detected                                      |
| ⚙️ Settings UI        | Endpoint / API key configurable in-app (persisted via Preferences) |
| 📋 Copy buttons       | One-tap copy of transcript or summary to clipboard                 |

---

## Requirements

- macOS 14 Sonoma or later
- .NET 10 SDK with the **MAUI workload** (`dotnet workload install maui`)
- An Azure subscription with:
  - Azure OpenAI resource
  - **whisper-large-v3** deployment
  - **gpt-4o-mini** (or gpt-4o) deployment

---

## Azure Setup

1. Create an **Azure OpenAI** resource in the [Azure portal](https://portal.azure.com).
2. In **Azure AI Foundry** (or the OpenAI studio), deploy:
   - Model: `whisper-large-v3` → note the deployment name (e.g. `whisper-large-v3`)
   - Model: `gpt-4o-mini` → note the deployment name (e.g. `gpt-4o-mini`)
3. Note the **Endpoint URL** (e.g. `https://<your-resource>.openai.azure.com/`) and an **API Key**.

See also: [Azure AI Foundry Whisper Quickstart](https://learn.microsoft.com/en-us/azure/foundry/openai/whisper-quickstart)

---

## Configuration

There are two ways to configure the Azure credentials:

### Option A – Settings UI (recommended for end-users)

Run the app and navigate to the **Settings** tab. Enter:

- Endpoint URL
- API Key
- Whisper deployment name
- Chat deployment name

Values are persisted across app restarts.

### Option B – appsettings.json / User Secrets (recommended for developers)

Edit `src/RealtimeTranscribe/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-api-key>",
    "WhisperDeploymentName": "whisper-large-v3",
    "ChatDeploymentName": "gpt-4o-mini"
  }
}
```

Or use .NET User Secrets (prevents secrets from being committed to git):

```bash
cd src/RealtimeTranscribe
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper-large-v3"
dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "gpt-4o-mini"
```

### Option C – DefaultAzureCredential (passwordless)

Leave the API Key blank. The app falls back to `DefaultAzureCredential`, which supports `az login`, managed identity, environment variables, and more.

---

## Build & Run

### Install the supported Xcode version for .NET 10 (once)

For stable `.NET 10` MacCatalyst builds, install the Xcode version required by the installed Apple workloads (currently `Xcode 26.2`).

1. Go to [Apple Developer Downloads](https://developer.apple.com/download/all/).
2. Sign in with your Apple ID.
3. Search for **Xcode 26.2** and download the `.xip`.
   - If `26.2` is not listed in the UI, inspect the download URLs for nearby `26.x` versions and infer the `26.2` URL by adjusting the version segment accordingly.
   - Apple sometimes keeps older artifacts available even when discovery in the web UI is inconsistent.
4. Extract it and rename it before first launch (for example `Xcode_26.2.app`).
5. Move it to `/Applications/`.
6. Select it for command-line builds:

```bash
sudo xcode-select --switch /Applications/Xcode_26.2.app
sudo xcodebuild -runFirstLaunch
sudo xcodebuild -license accept
```

### Install the MAUI workload (once)

```bash
dotnet workload install maui
```

### Run on macOS (MacCatalyst)

```bash
cd src/RealtimeTranscribe
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

### Build a .app bundle

```bash
dotnet publish -f net10.0-maccatalyst -c Release
# Output: bin/Release/net10.0-maccatalyst/publish/RealtimeTranscribe.app
```

---

## BlackHole Setup (record Teams / Zoom + microphone)

[BlackHole](https://existential.audio/blackhole/) is a free virtual audio driver for macOS that lets you route system audio (e.g. a Teams or Zoom call) into a recording device.

### Installation

```bash
brew install blackhole-2ch
```

Or download the installer from https://existential.audio/blackhole/

### Create a Multi-Output Device

1. Open **Audio MIDI Setup** (found in `/Applications/Utilities/`).
2. Click **+** at the bottom-left → **Create Multi-Output Device**.
3. Check both **BlackHole 2ch** and your built-in speakers / headphones.
4. Rename it (e.g. "Transcribe Output").
5. In **System Preferences → Sound → Output**, select **Transcribe Output**.

### Create an Aggregate Input Device

1. In **Audio MIDI Setup**, click **+** → **Create Aggregate Device**.
2. Check **BlackHole 2ch** and your built-in microphone.
3. Rename it (e.g. "Transcribe Input").
4. In **System Preferences → Sound → Input**, select **Transcribe Input**.

Now when you record in the app you will capture both your microphone and any system audio (Teams/Zoom call, YouTube, etc.).

---

## Project Structure

```
src/RealtimeTranscribe/
├── MauiProgram.cs              DI setup, configuration loading
├── App.xaml / .cs              Application entry point
├── AppShell.xaml / .cs         Tab navigation (Transcribe | Settings)
├── MainPage.xaml / .cs         Main recording UI
├── SettingsPage.xaml / .cs     Azure configuration UI
├── ViewModels/
│   ├── MainViewModel.cs        Record/Stop/Copy logic
│   └── SettingsViewModel.cs    Settings persistence logic
├── Services/
│   ├── IAudioService.cs
│   ├── AudioService.cs         Plugin.Maui.Audio wrapper
│   ├── ITranscriptionService.cs
│   └── TranscriptionService.cs Azure Whisper + GPT-4o
├── Models/
│   └── AzureOpenAISettings.cs  Strongly-typed config
├── Converters/
│   └── BoolToRecordColorConverter.cs
├── Platforms/MacCatalyst/
│   ├── AppDelegate.cs
│   ├── Program.cs
│   └── Info.plist              NSMicrophoneUsageDescription
└── Resources/
    ├── AppIcon/
    ├── Images/
    ├── Splash/
    └── Styles/
        ├── Colors.xaml
        └── Styles.xaml
```

---

## Permissions

The app requests microphone access on first launch. If permission is denied, navigate to **System Preferences → Privacy & Security → Microphone** and enable the app.

The `Info.plist` contains:

```xml
<key>NSMicrophoneUsageDescription</key>
<string>This app needs access to the microphone to record audio for transcription.</string>
```

---

## Edge Cases

| Scenario                  | Handling                                                      |
| ------------------------- | ------------------------------------------------------------- |
| No microphone permission  | User-friendly status message                                  |
| Azure auth failure        | Exception message shown in status                             |
| Empty recording           | Skips transcription/summarisation                             |
| Long recordings (>30 min) | Whisper file-size limit is 25 MB; chunk large files if needed |
| Network errors            | Exception message shown in status                             |
| Operation cancel          | Graceful cancellation via CancellationToken                   |

---

## License

MIT – see [LICENSE](LICENSE).
