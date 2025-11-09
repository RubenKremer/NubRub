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

### Method 1: Using the Audio Pack Wizard (Recommended)

NubRub includes a built-in wizard that makes creating, editing, and managing audio packs easy:

1. **Right-click the tray icon** → **Audio Pack Management**
2. Choose from the following options:
   - **Create...** - Create a new audio pack using a step-by-step wizard
     - Step 1: Enter pack name (version is auto-managed)
     - Step 2: Add rub sounds (movement sounds) and finish sounds (trigger sounds)
     - Step 3: Review and create the pack
   - **Edit...** - Edit an existing custom pack
     - Modify the pack name
     - Add, remove, or replace audio files
     - Choose to update the existing pack (version auto-increments) or save as a new pack
   - **Export...** - Export a custom pack to a `.nubrub` file for sharing
   - **Import...** - Import a `.nubrub` file to add a pack
   - **Delete...** - Delete a custom pack (with confirmation)

3. **Select your pack** in Settings by left-clicking the tray icon or right-clicking → **Settings...**
4. Choose your pack from the "Audio Pack" dropdown
5. Click **Save** to apply your settings

### Method 2: Manual Creation (Advanced)

You can also create packs manually by editing JSON files:

1. **Create your pack** following the structure above in `%LOCALAPPDATA%\NubRub\AudioPacks\`
2. **Open Settings** by left-clicking the tray icon or right-clicking → **Settings...**
3. **Select your pack** from the "Audio Pack" dropdown
4. Click **Save** to apply your settings

Custom packs will appear in the dropdown with their name and version (e.g., "My Custom Pack (1.0)").

### Sharing Audio Packs

Audio packs can be shared using the `.nubrub` file format:

1. **Export a pack**: Right-click tray icon → **Audio Pack Management** → **Export...**
   - Select the pack to export
   - Choose a location to save the `.nubrub` file
2. **Import a pack**: Right-click tray icon → **Audio Pack Management** → **Import...**
   - Select a `.nubrub` file
   - Review the pack details
   - Click **Import** to add it to your collection
3. **Double-click a `.nubrub` file** in Windows Explorer to automatically open the import wizard

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
- Try using the **Import...** wizard to re-import the pack

**Sounds not playing:**
- Verify audio files are valid WAV files
- Check file sizes (must be under 50 MB)
- Ensure file names in JSON match actual file names exactly (case-sensitive on some systems)
- Try editing the pack using **Edit...** to verify all files are properly configured

**Invalid pack errors:**
- Check JSON syntax (use a JSON validator)
- Ensure all required fields are present
- Verify file names don't contain invalid characters
- Use the **Edit...** wizard to fix issues

**Cannot edit/delete a pack:**
- Make sure the pack is not currently active (switch to another pack first)
- Close any file explorers that might have the pack folder open
- Restart the application if the pack was recently in use

## Tips

- Test with 1-2 sounds first to verify it works
- Use simple, descriptive file names
- Recommended: 16-bit, 44.1kHz audio quality
- Include multiple rub sounds for variety

