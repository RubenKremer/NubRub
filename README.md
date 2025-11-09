# NubRub

A Windows 11 system tray application that plays sounds when the TrackPoint (Lenovo ThinkPad pointing stick) is used, with an additional feature that plays a trigger sound after 25 seconds of continuous wiggling.

## Features

- **Per-device filtering**: Select your TrackPoint from a list of HID devices
- **Sound-on-activity**: Plays a sound while the TrackPoint is moving
- **25-second wiggle detection**: After 25 seconds of continuous movement (with <2s breaks allowed), plays a distinct trigger sound
- **System tray integration**: Runs minimized in the system tray
- **Configuration panel**: Easy device selection and settings adjustment
- **Audio Pack Management**: Create, edit, export, import, and delete custom audio packs with a built-in wizard
- **No driver required**: Pure user-mode application

## Requirements

- Windows 11
- .NET 8 Runtime (included in self-contained build)

## Installation

### Via Windows Package Manager (winget)

If NubRub is available in the Windows Package Manager repository, you can install it using:

```powershell
winget install R.Kremer.NubRub
```

Or simply:

```powershell
winget install NubRub
```

### Manual Installation

Download the latest MSI installer from the [Releases](https://github.com/RubenKremer/NubRub/releases) page and run it.

## Building

```bash
build.bat
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
2. Left-click the tray icon (or right-click → **Settings...**) to open the configuration panel
3. Select your TrackPoint from the device dropdown (or use **Auto-detect**)
4. Choose an audio pack and adjust settings as needed
5. Click **Save** to apply your settings
6. Move the TrackPoint - you should hear the sound
7. Wiggle the TrackPoint continuously for 25 seconds to trigger the sound

## Audio Files

The application includes built-in audio packs (Squeak, NSFW, Bugs, Glass). Custom audio packs can be created and managed using the built-in wizard:

**Audio Pack Management** (Right-click tray icon → **Audio Pack Management**):
- **Create...** - Create new audio packs with a step-by-step wizard
- **Edit...** - Edit existing packs (update or save as new)
- **Export...** - Export packs to `.nubrub` files for sharing
- **Import...** - Import `.nubrub` files to add packs
- **Delete...** - Delete custom packs (with confirmation)

Custom audio packs can also be created manually by editing JSON files in `%LOCALAPPDATA%\NubRub\AudioPacks\`. See `docs/CUSTOM_AUDIO_PACKS.md` for detailed instructions.

## Security

For security information, including vulnerability reporting and security best practices, see `docs/SECURITY.md`.

