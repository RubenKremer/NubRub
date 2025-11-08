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

**Implementation**:
```csharp
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
```

**Protection Against**:
- `..\..\..\` directory traversal
- Absolute paths outside the audio pack directory
- UNC paths
- Symbolic links (resolved to canonical paths)

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

## Potential Vulnerabilities and Mitigations

### 1. Malicious Audio Files
**Risk**: Audio files could potentially contain embedded metadata or be crafted to exploit audio codec vulnerabilities.

**Mitigation**:
- NAudio library handles audio decoding (trusted library)
- Files are validated for size limits
- Only WAV format is supported (simpler format, less attack surface)

### 2. JSON Injection
**Risk**: Malicious JSON could attempt to inject code or access system resources.

**Mitigation**:
- JSON is deserialized to strongly-typed objects (`AudioPackInfo`)
- No code execution from JSON
- File paths are validated before use

### 3. Denial of Service (DoS)
**Risk**: Extremely large files or many files could consume system resources.

**Mitigation**:
- File size limits (50 MB per file)
- File count limits (20 files per pack)
- JSON size limits (100 KB)

### 4. Path Traversal
**Risk**: Malicious file names could attempt to access files outside the audio pack directory.

**Mitigation**:
- Path validation using canonical paths
- Directory isolation
- Invalid character filtering

## Security Best Practices

1. **Principle of Least Privilege**: Application runs with user privileges, not admin
2. **Input Validation**: All user-provided data (file names, JSON) is validated
3. **Defense in Depth**: Multiple layers of validation (path, size, type, content)
4. **Fail Securely**: Invalid inputs are rejected silently without exposing system information
5. **Resource Limits**: Hard limits prevent resource exhaustion attacks

## Reporting Security Issues

If you discover a security vulnerability, please report it responsibly. Do not create public issues for security vulnerabilities.

## Version History

- **v1.0** (Current): Initial security implementation with path traversal prevention, file validation, and resource limits.

