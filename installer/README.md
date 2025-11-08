# NubRub Installer Setup Guide

This directory contains installer scripts for creating a Windows installer for NubRub.

**Steps:**

1. **Download WiX Toolset** from https://wixtoolset.org/releases/ (version 6.0 or later)
   See `INSTALL_WIX.md` for installation instructions.

2. **Build the installer**:
   ```build-wix-installer.bat```

The installer will be created in the `dist/` folder.

---

## Build Scripts

### `build-wix-installer.bat`
Automates the build process for WiX:
1. Builds the application using `dotnet publish`
2. Compiles the WiX installer
3. Creates the distribution package in `dist/`

---

## Silent Installation

The MSI installer supports standard Windows Installer silent installation switches:

**Silent installation (no UI):**
```batch
msiexec /i NubRub.msi /quiet
```
or
```batch
msiexec /i NubRub.msi /qn
```

**Unattended installation (progress bar only):**
```batch
msiexec /i NubRub.msi /passive
```

**Basic UI (progress bar only):**
```batch
msiexec /i NubRub.msi /qb
```

**Silent uninstall:**
```batch
msiexec /x NubRub.msi /quiet
```

---

## Troubleshooting

### "File not found" errors
- Verify the publish path in the installer script matches your actual build output
- Check that `dotnet publish` completed successfully

### "Access denied" during installation
- The installer is configured for per-user installation (no admin required)
- If you need system-wide installation, change `InstallScope` to `perMachine` in the WiX script

