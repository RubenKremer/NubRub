using System.IO;
using System.Text;
using NubRub.Models;
using Newtonsoft.Json;

namespace NubRub;

/// <summary>
/// Manages loading and validation of audio packs (both built-in and custom).
/// Implements security measures to prevent path traversal, file size abuse, and malicious content.
/// </summary>
public class AudioPackManager
{
    private const string AUDIO_PACKS_FOLDER = "AudioPacks";
    private const int MAX_FILE_SIZE_MB = 50; // Maximum file size per audio file (50 MB)
    private const int MAX_FILES_PER_PACK = 20; // Maximum number of audio files per pack
    private const int MAX_PACK_NAME_LENGTH = 100; // Maximum length for pack name
    private static readonly string[] ALLOWED_EXTENSIONS = { ".wav" }; // Only WAV files allowed

    private readonly string _audioPacksPath;
    private readonly List<AudioPackInfo> _builtInPacks = new()
    {
        new AudioPackInfo { Name = "Squeak", PackId = "squeak", IsBuiltIn = true },
        new AudioPackInfo { Name = "NSFW", PackId = "nsfw", IsBuiltIn = true },
        new AudioPackInfo { Name = "Bugs", PackId = "bugs", IsBuiltIn = true },
        new AudioPackInfo { Name = "Glass", PackId = "glass", IsBuiltIn = true }
    };

    /// <summary>
    /// Gets the full path to the audio packs directory.
    /// </summary>
    public string AudioPacksPath => _audioPacksPath;

    public AudioPackManager()
    {
        // Store custom audio packs in LocalApplicationData/NubRub/AudioPacks
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string nubRubPath = Path.Combine(appDataPath, "NubRub");
        _audioPacksPath = Path.Combine(nubRubPath, AUDIO_PACKS_FOLDER);
        
        // Create directory if it doesn't exist
        try
        {
            Directory.CreateDirectory(_audioPacksPath);
            CreateInstructionFile();
        }
        catch
        {
            // Silently fail - will be handled when trying to load packs
        }
    }

    /// <summary>
    /// Creates an instruction file in the audio packs folder if it doesn't exist.
    /// </summary>
    private void CreateInstructionFile()
    {
        try
        {
            string instructionFilePath = Path.Combine(_audioPacksPath, "HOW_TO_CREATE_AUDIO_PACKS.txt");
            
            // Only create if it doesn't exist
            if (File.Exists(instructionFilePath))
                return;

            string instructions = @"HOW TO CREATE CUSTOM AUDIO PACKS
=====================================

This folder is where you can create your own custom audio packs for NubRub.

CREATING A CUSTOM AUDIO PACK
-----------------------------

1. Create a new folder in this directory (e.g., ""MyCustomPack"")

2. Inside that folder, create a file named ""pack.json"" with the following structure:

{
  ""name"": ""My Custom Pack"",
  ""version"": ""1.0"",
  ""rubsounds"": [
    ""sound1.wav"",
    ""sound2.wav"",
    ""sound3.wav""
  ],
  ""finishsound"": [
    ""trigger1.wav""
  ]
}

3. Place your WAV audio files in the same folder as pack.json

4. Restart NubRub or open Settings to see your pack in the dropdown

JSON STRUCTURE
--------------

- ""name"" (required): Display name for the audio pack (max 100 characters)
- ""version"" (optional): Version string (e.g., ""1.0"", ""2.1.3"")
- ""rubsounds"" (required): Array of WAV file names for movement sounds
  * These sounds play randomly during TrackPoint movement
  * Can have multiple files (recommended: 3-10 files)
- ""finishsound"" (required): Array of WAV file names for trigger sounds
  * These sounds play after 25 seconds of continuous wiggling
  * Can have multiple files (one will be randomly selected)

EXAMPLE PACK STRUCTURE
-----------------------

MyCustomPack/
  ├── pack.json
  ├── sound1.wav
  ├── sound2.wav
  ├── sound3.wav
  └── trigger1.wav

EXAMPLE pack.json
-----------------

{
  ""name"": ""Glass"",
  ""version"": ""1.0"",
  ""rubsounds"": [
    ""file-1.wav"",
    ""file-2.wav"",
    ""file-3.wav"",
    ""file-4.wav"",
    ""file-5.wav""
  ],
  ""finishsound"": [
    ""trigger-1.wav""
  ]
}

REQUIREMENTS
------------

- Only .wav files are supported
- Maximum file size: 50 MB per file
- Maximum files per pack: 20 files total (rubsounds + finishsound)
- File names must not contain invalid characters
- All files must be in the same folder as pack.json

TIPS
----

1. Test your pack: Create a simple pack with 1-2 sounds first
2. File naming: Use simple, descriptive names (avoid special characters)
3. Audio quality: Use reasonable quality settings (16-bit, 44.1kHz recommended)
4. File size: Keep files reasonably sized for better performance
5. Variety: Include multiple rub sounds for better variety during movement

TROUBLESHOOTING
---------------

Pack not showing in dropdown:
- Ensure pack.json is valid JSON
- Check that all audio files exist and are .wav format
- Verify the pack folder is in this directory
- Restart the application

Sounds not playing:
- Verify audio files are valid WAV files
- Check file sizes (must be under 50 MB)
- Ensure file names in JSON match actual file names exactly

Invalid pack errors:
- Check JSON syntax (use a JSON validator)
- Ensure all required fields are present
- Verify file names don't contain invalid characters

For more information, see the CUSTOM_AUDIO_PACKS.md file in the application directory.
";

            File.WriteAllText(instructionFilePath, instructions, Encoding.UTF8);
        }
        catch
        {
            // Silently fail - instruction file is optional
        }
    }

    /// <summary>
    /// Gets all available audio packs (both built-in and custom).
    /// </summary>
    public List<AudioPackInfo> GetAllPacks()
    {
        var packs = new List<AudioPackInfo>(_builtInPacks);
        
        // Load custom packs from file system
        try
        {
            if (Directory.Exists(_audioPacksPath))
            {
                var customPacks = LoadCustomPacks();
                packs.AddRange(customPacks);
            }
        }
        catch
        {
            // Silently fail - return only built-in packs
        }

        return packs;
    }

    /// <summary>
    /// Gets a specific audio pack by its ID.
    /// </summary>
    public AudioPackInfo? GetPack(string packId)
    {
        // Check built-in packs first
        var builtIn = _builtInPacks.FirstOrDefault(p => p.PackId.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null)
            return builtIn;

        // Check custom packs
        try
        {
            if (Directory.Exists(_audioPacksPath))
            {
                var customPacks = LoadCustomPacks();
                return customPacks.FirstOrDefault(p => p.PackId.Equals(packId, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            // Silently fail
        }

        return null;
    }

    /// <summary>
    /// Loads custom audio packs from the file system with security validations.
    /// </summary>
    private List<AudioPackInfo> LoadCustomPacks()
    {
        var packs = new List<AudioPackInfo>();

        try
        {
            if (!Directory.Exists(_audioPacksPath))
                return packs;

            // Get all subdirectories (each represents an audio pack)
            var packDirectories = Directory.GetDirectories(_audioPacksPath);
            
            foreach (var packDir in packDirectories)
            {
                try
                {
                    // Security: Validate directory path to prevent traversal
                    if (!IsPathSafe(packDir, _audioPacksPath))
                    {
                        continue; // Skip potentially malicious paths
                    }

                    // Look for pack.json file
                    string jsonPath = Path.Combine(packDir, "pack.json");
                    if (!File.Exists(jsonPath))
                        continue;

                    // Security: Validate JSON file path
                    if (!IsPathSafe(jsonPath, _audioPacksPath))
                    {
                        continue;
                    }

                    // Parse JSON with size limit
                    string jsonContent = File.ReadAllText(jsonPath, Encoding.UTF8);
                    if (jsonContent.Length > 100 * 1024) // Max 100 KB for JSON
                    {
                        continue; // Skip oversized JSON files
                    }

                    var packInfo = JsonConvert.DeserializeObject<AudioPackInfo>(jsonContent);
                    if (packInfo == null)
                        continue;

                    // Security: Validate pack name
                    if (string.IsNullOrWhiteSpace(packInfo.Name) || 
                        packInfo.Name.Length > MAX_PACK_NAME_LENGTH ||
                        ContainsInvalidChars(packInfo.Name))
                    {
                        continue;
                    }

                    // Set pack directory and ID
                    packInfo.PackDirectory = packDir;
                    packInfo.PackId = Path.GetFileName(packDir);
                    packInfo.IsBuiltIn = false;

                    // Security: Validate and sanitize file lists
                    packInfo.RubSounds = ValidateAndSanitizeFileList(packInfo.RubSounds, packDir);
                    packInfo.FinishSounds = ValidateAndSanitizeFileList(packInfo.FinishSounds, packDir);

                    // Security: Limit total number of files
                    int totalFiles = packInfo.RubSounds.Count + packInfo.FinishSounds.Count;
                    if (totalFiles > MAX_FILES_PER_PACK)
                    {
                        // Trim to limit
                        int allowedRub = Math.Min(packInfo.RubSounds.Count, MAX_FILES_PER_PACK);
                        packInfo.RubSounds = packInfo.RubSounds.Take(allowedRub).ToList();
                        packInfo.FinishSounds = new List<string>(); // Remove finish sounds if over limit
                    }

                    // Only add pack if it has at least one valid file
                    if (packInfo.RubSounds.Count > 0 || packInfo.FinishSounds.Count > 0)
                    {
                        packs.Add(packInfo);
                    }
                }
                catch
                {
                    // Skip invalid packs - continue with next directory
                    continue;
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return packs;
    }

    /// <summary>
    /// Validates and sanitizes a list of audio file names, ensuring they exist and are safe.
    /// </summary>
    private List<string> ValidateAndSanitizeFileList(List<string> fileNames, string packDirectory)
    {
        var validFiles = new List<string>();

        foreach (var fileName in fileNames)
        {
            try
            {
                // Security: Validate file name
                if (string.IsNullOrWhiteSpace(fileName) || 
                    ContainsInvalidChars(fileName) ||
                    fileName.Length > 255) // Max Windows filename length
                {
                    continue;
                }

                // Security: Only allow specific extensions
                string extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!ALLOWED_EXTENSIONS.Contains(extension))
                {
                    continue; // Skip non-WAV files
                }

                // Security: Validate full path to prevent directory traversal
                string fullPath = Path.Combine(packDirectory, fileName);
                if (!IsPathSafe(fullPath, packDirectory))
                {
                    continue; // Skip potentially malicious paths
                }

                // Security: Check if file exists and validate size
                if (!File.Exists(fullPath))
                {
                    continue; // Skip missing files
                }

                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MAX_FILE_SIZE_MB * 1024 * 1024)
                {
                    continue; // Skip oversized files
                }

                // File is valid - add to list
                validFiles.Add(fileName);
            }
            catch
            {
                // Skip invalid files
                continue;
            }
        }

        return validFiles;
    }

    /// <summary>
    /// Validates that a path is safe and doesn't contain directory traversal attempts.
    /// </summary>
    private bool IsPathSafe(string path, string baseDirectory)
    {
        try
        {
            // Get full canonical paths
            string fullPath = Path.GetFullPath(path);
            string fullBase = Path.GetFullPath(baseDirectory);

            // Ensure the path is within the base directory
            return fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false; // Invalid path
        }
    }

    /// <summary>
    /// Checks if a string contains invalid characters for file/directory names.
    /// </summary>
    private bool ContainsInvalidChars(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return name.IndexOfAny(invalidChars) >= 0;
    }

    /// <summary>
    /// Gets the full path to an audio file in a pack.
    /// </summary>
    public string? GetAudioFilePath(AudioPackInfo pack, string fileName)
    {
        try
        {
            if (pack.IsBuiltIn)
            {
                // Built-in packs are loaded from embedded resources, not file system
                return null;
            }

            // Security: Validate file name and path
            if (string.IsNullOrWhiteSpace(fileName) || ContainsInvalidChars(fileName))
            {
                return null;
            }

            string fullPath = Path.Combine(pack.PackDirectory, fileName);
            
            // Security: Ensure path is safe
            if (!IsPathSafe(fullPath, pack.PackDirectory))
            {
                return null;
            }

            // Security: Verify file exists and is within size limits
            if (!File.Exists(fullPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > MAX_FILE_SIZE_MB * 1024 * 1024)
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }
}

