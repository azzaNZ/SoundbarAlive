# SoundBarKeepAlive

**Keep your soundbar or speakers awake — even when your PC is silent or muted.**

`SoundBarKeepAlive` is a lightweight Windows tray application that plays an inaudible silent tone every 10 minutes to prevent soundbars, monitors, or powered speakers from going into standby or "sleep mode" due to inactivity.  

It is especially useful for:
- HDMI/ARC soundbars or monitors that power off after a few minutes of silence.
- Audio devices that take a few seconds to "wake up", causing you to miss system sounds.
- Maintaining a persistent audio signal without annoying noise.

---

## Features

- **Silent Audio Playback**  
  Plays a completely inaudible silent WAV file (8 seconds) every 10 minutes.

- **Smart Volume Control**  
  - If your system volume is **not muted and >0%**, the app does **nothing** (doesn't interfere).  
  - If muted or 0%, it **temporarily sets volume to 50%, un-mutes, plays the tone, and restores the original state** afterward.

- **Runs in the System Tray**  
  - Automatically starts minimized.
  - Shows a tray icon with next playback countdown.
  - Provides a right-click menu to check status, force playback, or add/remove from Windows startup.

- **Startup Integration**  
  Add or remove the app from Windows startup with a single click.

- **Lightweight**  
  Written in C#, using `System.Media.SoundPlayer` and `NAudio` for audio device control.

---

## Why Was This Built?

Many HDMI or USB soundbars and monitors automatically enter standby mode after a few minutes of no audio.  
This causes frustrating delays:
- You miss system notifications or sounds while the device powers on.
- You must manually wake the soundbar by changing inputs or pressing buttons.

`SoundBarKeepAlive` solves this by **keeping the audio stream alive** without producing audible sound.

---

## How It Works

1. Every 10 minutes (configurable in code), the app:
   - Checks the **default audio output device's volume** using `NAudio`.
   - If the volume is audible (>0% and not muted), **skips playback**.
   - If muted or 0%, **sets volume to 50%, un-mutes, and plays 8 seconds of silence**.
   - **Restores** the original volume and mute state after playback.

2. A small WAV file containing silence is generated on the fly and played via `SoundPlayer`.

3. The app lives in the **Windows notification area (system tray)**:
   - Displays when the next silent sound will play.
   - Lets you manually trigger playback.
   - Offers startup management and exit options.

---

## Installation & Usage

1. Clone or download this repository.
2. Install dependencies via NuGet:
   ```bash
   dotnet add package NAudio


## Requirements

Windows 10 or later

.NET 6.0 or newer (can be adjusted for older .NET versions)

NAudio (installed via NuGet)

