# System Audio Capture (macOS 14.2+)

On macOS 14.2 (Sonoma) and later, Realtime Transcribe can capture **all system audio** — everything that plays on your Mac, including Teams, Zoom, browser tabs, and music apps — directly, without BlackHole or any other virtual audio driver.

## Table of Contents

- [How to use it](#how-to-use-it)
- [Granting the permission](#granting-the-permission)
- [Troubleshooting](#troubleshooting)
  - ["Ensure the app has the system-audio capture permission"](#ensure-the-app-has-the-system-audio-capture-permission)
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

1. **You denied the permission prompt.** See [I denied the permission — how do I re-enable it?](#i-denied-the-permission--how-do-i-re-enable-it) below.
2. **The permission prompt never appeared.** See [No permission prompt appeared](#no-permission-prompt-appeared).
3. **You are running macOS older than 14.2.** See [macOS version too old](#macos-version-too-old).

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
