# Custom Audio Packs Guide

## Overview

NubRub now supports custom audio packs! You can create your own audio packs with custom sounds for movement and trigger events.

## Creating a Custom Audio Pack

### Directory Structure

1. Navigate to: `%LOCALAPPDATA%\NubRub\AudioPacks\`
   - On Windows, this is typically: `C:\Users\YourUsername\AppData\Local\NubRub\AudioPacks\`

2. Create a new folder for your audio pack (e.g., `MyCustomPack`)

3. Inside that folder, create a `pack.json` file with the following structure:

```json
{
  "name": "My Custom Pack",
  "version": "1.0",
  "rubsounds": [
    "sound1.wav",
    "sound2.wav",
    "sound3.wav"
  ],
  "finishsound": [
    "trigger1.wav"
  ]
}
```

### JSON Structure

- **`name`** (required): Display name for the audio pack (max 100 characters)
- **`version`** (optional): Version string (e.g., "1.0", "2.1.3")
- **`rubsounds`** (required): Array of WAV file names for movement sounds
  - These sounds play randomly during TrackPoint movement
  - Can have multiple files (recommended: 3-10 files)
- **`finishsound`** (required): Array of WAV file names for trigger sounds
  - These sounds play after 25 seconds of continuous wiggling
  - Can have multiple files (one will be randomly selected)

### Audio Files

- **Format**: Only `.wav` files are supported
- **Location**: Place all audio files in the same folder as `pack.json`
- **File Size**: Maximum 50 MB per file
- **File Count**: Maximum 20 files total per pack (rubsounds + finishsound combined)

### Example Pack Structure

```
AudioPacks/
  └── MyCustomPack/
      ├── pack.json
      ├── sound1.wav
      ├── sound2.wav
      ├── sound3.wav
      └── trigger1.wav
```

### Example pack.json

```json
{
  "name": "Glass",
  "version": "1.0",
  "rubsounds": [
    "file-1.wav",
    "file-2.wav",
    "file-3.wav",
    "file-4.wav",
    "file-5.wav"
  ],
  "finishsound": [
    "trigger-1.wav"
  ]
}
```

## Using Custom Audio Packs

1. **Create your pack** following the structure above
2. **Restart NubRub** (or open Settings)
3. **Open Settings** from the tray icon menu
4. **Select your pack** from the "Audio Pack" dropdown
5. **Save** your settings

Custom packs will appear in the dropdown with their name and version (e.g., "My Custom Pack (1.0)").

## Security Notes

- Only `.wav` files are allowed
- File paths are validated to prevent directory traversal attacks
- File sizes are limited to 50 MB per file
- Maximum 20 files per pack
- Invalid packs are silently skipped

See `SECURITY.md` for detailed security information.

## Troubleshooting

**Pack not showing in dropdown:**
- Ensure `pack.json` is valid JSON
- Check that all audio files exist and are `.wav` format
- Verify the pack folder is in `%LOCALAPPDATA%\NubRub\AudioPacks\`
- Restart the application

**Sounds not playing:**
- Verify audio files are valid WAV files
- Check file sizes (must be under 50 MB)
- Ensure file names in JSON match actual file names exactly (case-sensitive on some systems)

**Invalid pack errors:**
- Check JSON syntax (use a JSON validator)
- Ensure all required fields are present
- Verify file names don't contain invalid characters

## Tips

- Test with 1-2 sounds first to verify it works
- Use simple, descriptive file names
- Recommended: 16-bit, 44.1kHz audio quality
- Include multiple rub sounds for variety

