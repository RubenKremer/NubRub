# TrackPoint Squeak — Windows 11 Utility

A tiny Windows 11 app that plays a subtle **squeaky** sound **only when the Lenovo ThinkPad TrackPoint** (the red pointing stick) is used. The normal touchpad and any other mice continue to work as usual and **do not** trigger audio.

---

## Key Features
- 🎯 **Per‑device filtering**: List all mouse‑like HID devices and let the user **select the TrackPoint** in a small config panel.
- 🔊 **Squeak-on-activity**: Start a looped squeak while the TrackPoint is moving/scrolling; stop shortly after it’s idle.
- 🧩 **No driver required**: Pure user‑mode application; does not interfere with pointer behavior.
- 🪟 **Windows 11 only**: Optimized and tested for Windows 11.
- 🧰 **Minimal UI**: Runs **minimized in the system tray** (lower-right taskbar); optional tray menu with quick controls.

---

## How It Works (High Level)
- Uses the Win32 **Raw Input** API (`RegisterRawInputDevices`, `WM_INPUT`, `GetRawInputDeviceInfo`) to get **per‑device** events from mouse‑class HID devices (Usage Page `0x01` / Usage `0x02`).
- On first run (or via the config panel), the app lists candidate devices. For each device we show friendly details (Manufacturer/Product, VID:PID) by querying HID attributes and strings.
- The selected device’s **handle and identity** are saved in a local config file so the app can filter runtime events to the TrackPoint only.
- Audio is handled by a low‑latency output (e.g., WASAPI/XAudio2 or NAudio in C#). A short squeak loop plays while activity is detected; an **idle debounce** (e.g., 150–300 ms) stops playback when motion ceases.

> Because the app only **observes** raw input and never requests exclusive access, the TrackPoint and touchpad continue to behave normally for the OS and all applications.

---

## Why Touchpad Doesn’t Trigger the Sound
Modern precision touchpads usually report under **Digitizer** (Usage Page `0x0D`, Usage `0x05`) rather than Mouse. By subscribing to mouse raw input and filtering by the chosen device handle, the app naturally **ignores** touchpad events. External USB/Bluetooth mice are also ignored unless explicitly selected.

---

## System Tray Behavior
- The app **launches minimized** and places an icon in the **system tray** (lower‑right taskbar).
- **Left‑click** toggles the config panel (open/close).
- **Right‑click** opens a context menu with:
  - **Enable/Disable Squeak**
  - **Select HID Device…** (opens the config panel)
  - **Volume** (slider)
  - **Start with Windows** (toggle)
  - **Exit**

---

## Config Panel
A compact dialog with:
- **Device Picker**: Lists all mouse‑class HID devices with:
  - Manufacturer / Product strings (when available)
  - **VID:PID** (e.g., `17EF:60EE`)
  - Device path (advanced details collapsed by default)
- **Test Squeak** button: Play sample audio to verify output/volume.
- **Idle Cutoff** (ms): Time of no movement before stopping the squeak (default 250 ms).
- **Only on Movement** toggle: Ignore button events, only react to x/y movement & scroll.
- **Volume** slider.
- **Save** / **Cancel**.

> The selection persists in a JSON config file in `%LOCALAPPDATA%/TrackPointSqueak/config.json` (path may be adjusted during implementation).

---

## Technical Design

### Input Path
- Register for Raw Input with `RIM_TYPEMOUSE` and `RIDEV_INPUTSINK` so the app receives events even when unfocused.
- Enumerate devices at startup (`GetRawInputDeviceList`). For each mouse device:
  - Query `RIDI_DEVICENAME` → device path.
  - Open the device to query HID attributes:
    - `HidD_GetAttributes` → Vendor ID (VID) / Product ID (PID)
    - `HidD_GetManufacturerString` / `HidD_GetProductString`
- Persist a **device identity** (device path + VID/PID) and, at runtime, compare incoming `WM_INPUT` events via `RAWINPUTHEADER.hDevice`.

### Audio Path
- Keep the audio device open to reduce latency. Preload a short squeak sample (WAV/PCM).
- **Start** playback on first matching TrackPoint event after idle; **stop** after the idle cutoff elapses.
- Optional **attack/release ramps** to avoid clicks.
- Implementation options:
  - **C#**: NAudio (WASAPI), XAudio2 via SharpDX (legacy), or Windows.Media.Audio (UWP interop if desired).
  - **C++**: XAudio2 or WASAPI directly.

### Process Model
- Single user‑mode process; no admin rights needed.
- Tray icon + message loop window to receive `WM_INPUT`.
- Optional background service is **not required**.

---

## Installation & Startup
- **Portable**: Ship a single EXE (self‑contained if .NET). First run creates the config folder.
- **Installer (optional)**: Provide MSI/Setup with options for auto‑start and Start Menu shortcuts.
- **Auto‑start**: User can enable from the tray menu (adds a `Run` registry entry or a Startup shortcut).

---

## Usage
1. Launch the app — it starts minimized in the tray.
2. Open the tray menu → **Select HID Device…**
3. Choose your **TrackPoint** from the list and click **Save**.
4. Move the TrackPoint — you should hear the squeak. Touchpad/other mice won’t trigger it.
5. Adjust **Volume** or **Idle Cutoff** to taste.

---

## Edge Cases & Resilience
- **Device path changes** after docking/undocking or firmware updates: the app attempts to re‑match by VID/PID and product string; if ambiguous, it prompts to re‑select.
- **Multiple TrackPoint‑like devices** (e.g., external keyboard with pointing stick): the picker prevents confusion; only the selected one triggers audio.
- **Exclusive access conflicts**: none—app never takes exclusive access.
- **High DPI / multi‑monitor**: unaffected; app observes input only.

---

## Privacy & Security
- The app does **not** capture or transmit keystrokes or personally identifiable data.
- Only minimal per‑device metadata (VID/PID, product/manufacturer strings, device path) is stored locally for filtering.
- No network access is required.

---

## Limitations
- Windows 11 only.
- If Lenovo/Windows reports the TrackPoint under a non‑standard usage, manual selection is required (supported via picker).
- The app can’t silence other apps’ sounds; it only plays (or mutes) its own.

---

## Roadmap (Optional Enhancements)
- **Profiles** per device (different sounds or volumes).
- **Per‑app rules** (mute during meetings/recording).
- **Built‑in sample editor** and custom sound packs.
- **Automatic TrackPoint detection** heuristics.

---

## Build Notes (Reference Implementation)

### Option A — C# (.NET 8)
- UI: WinUI 3 or WinForms/WPF + tray icon.
- Raw Input: P/Invoke for `RegisterRawInputDevices`, `GetRawInputDeviceInfo`, `GetRawInputData`.
- HID details: P/Invoke `hid.dll` for `HidD_*` calls.
- Audio: NAudio (WASAPI) for low‑latency looped playback.
- Packaging: Publish **single‑file** self‑contained `win‑x64`.

### Option B — C++/Win32
- Pure Win32 message loop + tray icon.
- Raw Input and HID via Windows headers.
- Audio via XAudio2 or WASAPI.
- Packaging via CMake + WiX/MSIX if desired.

---

## Configuration File (proposed)
```jsonc
{
  "SelectedDevice": {
    "DevicePath": "\\\\?\\HID#VID_17EF&PID_60EE&...",
    "VendorId": 6127,
    "ProductId": 24814,
    "Manufacturer": "Lenovo",
    "Product": "TrackPoint",
    "LastSeen": "2025-11-07T10:15:30Z"
  },
  "Audio": {
    "Volume": 0.6,
    "IdleCutoffMs": 250,
    "OnlyOnMovement": true,
    "SoundFile": "squeak.wav"
  },
  "Startup": {
    "RunAtLogin": true
  }
}
```

---

## Troubleshooting
- **No sound**: Verify the correct device is selected; click **Test Squeak**. Check system output device/volume.
- **Sound triggers from touchpad**: Re‑select the TrackPoint; ensure the chosen device is the stick, not the touchpad. Consider enabling **Only on Movement** if button events cause confusion.
- **After docking/undocking**: If the squeak stops, open the picker; the device path may have changed—re‑select or let the app auto‑rebind.

---

## License
TBD.

