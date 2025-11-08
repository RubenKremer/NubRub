# NubRub

A Windows 11 system tray application that plays squeaky sounds when the TrackPoint (Lenovo ThinkPad pointing stick) is used, with an additional feature that plays a trigger sound after 25 seconds of continuous wiggling.

## Features

- **Per-device filtering**: Select your TrackPoint from a list of HID devices
- **Squeak-on-activity**: Plays a looped squeak sound while the TrackPoint is moving
- **25-second wiggle detection**: After 25 seconds of continuous movement (with <2s breaks allowed), plays a distinct trigger sound
- **System tray integration**: Runs minimized in the system tray
- **Configuration panel**: Easy device selection and settings adjustment
- **No driver required**: Pure user-mode application

## Requirements

- Windows 11
- .NET 8 Runtime (included in self-contained build)

## Building

```bash
dotnet build
```

## Publishing

To create a single-file self-contained executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/NubRub.exe`

## Creating an Installer

To create a Windows installer for distribution, see the `installer/` directory in the project root. The installer directory contains:

- **WiX Toolset script** (`NubRub.wxs`) - Professional MSI installer
- **Build script** - Automated installer creation
- **Documentation** - Complete setup guide

Quick start:
1. Build the application (see Publishing above)
2. Run `installer/build-installer.bat`
3. The installer will be created in the `installer/dist/` folder

See `installer/README.md` for detailed instructions.

## Usage

1. Launch the application - it starts minimized in the system tray
2. Right-click the tray icon → **Select HID Device...**
3. Choose your TrackPoint from the list and click **Save**
4. Move the TrackPoint - you should hear the squeak
5. Wiggle the TrackPoint continuously for 25 seconds to trigger the sound

## Configuration

Configuration is stored in `%LOCALAPPDATA%/NubRub/config.json`

Settings include:
- Selected device
- Audio volume (0-100%)
- Idle cutoff time (ms)
- Only on movement (ignore button events)
- Start with Windows

## Audio Files

The application includes built-in audio packs. Custom audio packs can be added to `%LOCALAPPDATA%\NubRub\AudioPacks\`. See `CUSTOM_AUDIO_PACKS.md` for details.

