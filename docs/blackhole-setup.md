# BlackHole & macOS Audio Setup

[BlackHole](https://existential.audio/blackhole/) is a free virtual audio driver for macOS that routes system audio (e.g. from Teams or Zoom) into a recording device alongside your microphone.

## Table of Contents

- [Install BlackHole](#install-blackhole)
- [Create a Multi-Output Device](#create-a-multi-output-device)
- [Create an Aggregate Input Device](#create-an-aggregate-input-device)
- [Configure macOS Sound settings](#configure-macos-sound-settings)
- [Select the recording device in the app](#select-the-recording-device-in-the-app)

---

## Install BlackHole

```bash
brew install blackhole-2ch
```

Or download the installer from https://existential.audio/blackhole/

---

## Create a Multi-Output Device

A Multi-Output Device sends audio to multiple outputs at once — your speakers *and* BlackHole — so you can still hear the call while it is being captured.

1. Open **Audio MIDI Setup** (`/Applications/Utilities/Audio MIDI Setup.app`).
2. Click **+** at the bottom-left → **Create Multi-Output Device**.
3. In the device list on the right, check both **BlackHole 2ch** and your speakers / headphones.
4. Double-click the device name and rename it (e.g. `Transcribe Output`).

![Audio MIDI Setup showing Transcribe Output multi-output device with BlackHole 2ch checked](https://github.com/user-attachments/assets/c9e1ce1e-ae06-4683-84c8-2b6d1d68f0cd)

---

## Create an Aggregate Input Device

An Aggregate Input Device combines multiple inputs into one — BlackHole (which receives system audio) *and* your microphone.

1. In **Audio MIDI Setup**, click **+** → **Create Aggregate Device**.
2. Check **BlackHole 2ch** and your built-in microphone (and any other microphone you want to use).
3. Set the **Clock Source** to **BlackHole 2ch**.
4. Double-click the device name and rename it (e.g. `Transcribe Input`).

![Audio MIDI Setup showing Transcribe Input aggregate device with BlackHole 2ch and microphones checked](https://github.com/user-attachments/assets/2bbabaa2-06c9-422e-bf99-b900fea79a56)

---

## Configure macOS Sound settings

### Output

Set macOS to play audio through the Multi-Output Device so system audio is routed through BlackHole.

Open **System Settings → Sound** and on the **Output** tab select **Transcribe Output**.

![System Settings Sound Output tab with Transcribe Output selected](https://github.com/user-attachments/assets/c9adfaf7-e8e2-4ed5-905f-2c003e95d417)

### Input

On the **Input** tab select **Transcribe Input** so the app can capture both the microphone and system audio.

![System Settings Sound Input tab with Transcribe Input selected](https://github.com/user-attachments/assets/c5c93626-6e5a-474d-9ff0-d1c57c0bb130)

---

## Select the recording device in the app

The recording device can be selected inside the app on the **Devices** tab. Choose **Transcribe Input** (the aggregate device) to capture both your microphone and system audio simultaneously.

> **Tip:** If you only want to record your own microphone (no system audio), select the built-in microphone directly instead of the aggregate device.

---

## Bluetooth devices

Avoid including Bluetooth devices inside an aggregate device — macOS removes them from the aggregate when they disconnect, which disrupts recording. Use Bluetooth devices as a standalone system input or route them through a multi-output device to BlackHole instead.
