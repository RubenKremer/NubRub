# Security Documentation for NubRub

## Overview

This document outlines the security measures implemented in NubRub to protect users from potential vulnerabilities when using custom audio packs.

## Custom Audio Pack Security

### File System Access

**Location**: Custom audio packs are stored in `%LOCALAPPDATA%\NubRub\AudioPacks\`

**Security Measures**:
1. **Path Validation**: All file paths are validated to prevent directory traversal attacks (e.g., `..\..\..\Windows\System32`)
2. **Canonical Path Resolution**: Uses `Path.GetFullPath()` to resolve canonical paths and ensure files are within the audio pack directory
3. **Directory Isolation**: Each audio pack must be in its own subdirectory, preventing access to other packs' files

### File Validation

**File Type Restrictions**:
- Only `.wav` files are allowed
- File extensions are validated case-insensitively
- Invalid file types are silently skipped

**File Size Limits**:
- Maximum file size: **50 MB per audio file**
- Maximum JSON file size: **100 KB**
- Files exceeding these limits are rejected

**File Count Limits**:
- Maximum files per pack: **20 files total** (rub sounds + finish sounds combined)
- Packs exceeding this limit are trimmed to the maximum

### JSON Validation

**Structure Validation**:
- JSON files must be named `pack.json` and located in the pack's root directory
- JSON structure is validated against the `AudioPackInfo` model
- Malformed JSON files are silently skipped

**Content Validation**:
- Pack names are limited to **100 characters**
- Pack names are validated for invalid filename characters
- File names in JSON are validated for:
  - Invalid filename characters
  - Maximum length (255 characters, Windows limit)
  - Path traversal attempts

### Path Traversal Prevention

All file paths are validated using canonical path resolution to ensure files are within the audio pack directory. This prevents directory traversal attacks (`..\..\..\`), absolute paths, UNC paths, and symbolic links.

### Error Handling

**Silent Failures**:
- Invalid packs are silently skipped (no error messages to prevent information leakage)
- Missing files are skipped without error
- Corrupted audio files are skipped

**Rationale**: Silent failures prevent attackers from gaining information about the file system structure through error messages.

### Resource Limits

**Memory Protection**:
- Audio files are loaded on-demand, not all at once
- Disposed immediately after use
- No caching of audio file contents

**File Handle Protection**:
- All file handles are properly disposed
- Using statements ensure cleanup even on exceptions

## Built-in vs Custom Packs

**Built-in Packs**:
- Loaded from embedded resources (compiled into the executable)
- No file system access required
- Always available

**Custom Packs**:
- Loaded from user's `%LOCALAPPDATA%\NubRub\AudioPacks\` directory
- Subject to all security validations
- Can be added/removed by the user

## Recommendations for Users

1. **Source Verification**: Only install audio packs from trusted sources
2. **Directory Permissions**: The audio packs directory should have standard user permissions (no admin access required)
3. **Regular Updates**: Keep the application updated to receive security patches

## Security Measures

- **File Type**: Only WAV files allowed
- **File Size**: 50 MB per file maximum
- **File Count**: 20 files per pack maximum
- **Path Validation**: Canonical path resolution prevents directory traversal
- **JSON Validation**: Strongly-typed deserialization prevents injection
- **Resource Limits**: Hard limits prevent DoS attacks

## Reporting Security Issues

If you discover a security vulnerability, please report it responsibly. Do not create public issues for security vulnerabilities.

