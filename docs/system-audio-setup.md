# System Audio Capture (macOS 14.2+)

On macOS 14.2 (Sonoma) and later, Realtime Transcribe can capture **all system audio** — everything that plays on your Mac, including Teams, Zoom, browser tabs, and music apps — directly, without BlackHole or any other virtual audio driver.

## Table of Contents

- [How to use it](#how-to-use-it)
- [Granting the permission](#granting-the-permission)
- [Troubleshooting](#troubleshooting)
  - ["Ensure the app has the system-audio capture permission"](#ensure-the-app-has-the-system-audio-capture-permission)
  - [App Sandbox blocks the Process Tap API](#app-sandbox-blocks-the-process-tap-api)
  - [No permission prompt appeared](#no-permission-prompt-appeared)
  - [I denied the permission — how do I re-enable it?](#i-denied-the-permission--how-do-i-re-enable-it)
  - [macOS version too old](#macos-version-too-old)

---

## How to use it

1. Open the app and go to the **Devices** tab.
2. Select **"System Audio (all apps)"** at the top of the input device list.
3. Start recording — the app will capture everything you hear, regardless of which output device (speakers, AirPods, etc.) is in use.

> **Tip:** This option captures *only* system audio (other apps' audio output). It does **not** include your microphone. If you need both, use a BlackHole aggregate device instead — see [BlackHole & macOS Audio Setup](blackhole-setup.md).

---

## Granting the permission

The **first time** you select "System Audio (all apps)" and start recording, macOS will show a permission prompt:

> *"Realtime Transcribe" would like to capture the audio of the system and other apps.*

Click **Allow** to grant the permission. macOS remembers this choice; you will not be prompted again.

---

## Troubleshooting

### "Ensure the app has the system-audio capture permission"

This error means the CoreAudio Process Tap could not be created. The most common causes:

1. **You denied the permission prompt** — see [I denied the permission — how do I re-enable it?](#i-denied-the-permission--how-do-i-re-enable-it) below.
2. **The permission prompt never appeared** — see [No permission prompt appeared](#no-permission-prompt-appeared).
3. **The app is sandboxed** — see [App Sandbox blocks the Process Tap API](#app-sandbox-blocks-the-process-tap-api). This is the most common cause when the toggles in System Settings are already on but recording still fails.
4. **You are running macOS older than 14.2** — see [macOS version too old](#macos-version-too-old).

### App Sandbox blocks the Process Tap API

If you have already enabled **Realtime Transcribe** in **both** of these panes —

- *Privacy & Security → Screen & System Audio Recording*, **and**
- *Privacy & Security → System Audio Recording Only*

— and you still get the "Ensure the app has the system-audio capture permission" error, the app you are running is **sandboxed**. The CoreAudio Process Tap API (`AudioHardwareCreateProcessTap`) does not work inside the macOS App Sandbox: granting the user-facing TCC permissions is necessary but not sufficient. This is a hard OS-level restriction; Apple's own Process Tap reference sample (AudioCap) is non-sandboxed for the same reason.

The error message in the latest build will additionally show the underlying `OSStatus` as a four-character code. A code such as `'what'` (`kAudioHardwareIllegalOperationError`) is the typical symptom of a sandbox block.

**How to fix it:**

- **Use the official release.** The pre-built `.app` bundle published on the [Releases page](https://github.com/Jandev/realtime-transcribe/releases) is **not** sandboxed and supports system-audio capture out of the box.
- **If you built from source yourself** with sandboxing enabled, rebuild after making sure `src/RealtimeTranscribe/Platforms/MacCatalyst/Entitlements.plist` does **not** contain a `com.apple.security.app-sandbox` key set to `true`. The version in the repository already has it removed.

> **Trade-off:** Removing the sandbox means the app cannot be distributed through the Mac App Store. Since Realtime Transcribe is distributed as an unsigned `.app` bundle from GitHub Releases, this is not a problem in practice.

### No permission prompt appeared

If macOS did not show a permission dialog when you first selected "System Audio (all apps)":

1. **Quit and relaunch the app**, then try again. macOS sometimes defers the prompt until the next launch.
2. **Check System Settings manually**: open **System Settings → Privacy & Security → Screen & System Audio Recording** (or **Screen Recording** on some macOS versions). If "Realtime Transcribe" is listed, make sure the toggle is **on**.
3. If the app is not listed at all, try resetting the TCC database for the app (see below).

### I denied the permission — how do I re-enable it?

1. Open **System Settings → Privacy & Security**.
2. Scroll down to (or search for) **Screen & System Audio Recording** (on macOS 15+) or **Screen Recording** (on macOS 14.x).
3. Find **Realtime Transcribe** in the list and toggle it **on**.
4. macOS may ask you to quit and reopen the app for the change to take effect — do so.

> **Note:** On macOS Sequoia (15) and later, this permission category was renamed from "Screen Recording" to "Screen & System Audio Recording". The Process Tap API's permission lives here, even though the app does not record your screen.

### macOS version too old

The CoreAudio Process Tap API requires **macOS 14.2 (Sonoma)** or later. If you are on an older version, the "System Audio (all apps)" option will not work.

**Alternative:** Use the [BlackHole virtual loopback driver](blackhole-setup.md) to capture system audio on older macOS versions.
