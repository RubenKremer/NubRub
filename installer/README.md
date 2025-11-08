# NubRub Installer Setup Guide

This directory contains installer scripts for creating a Windows installer for NubRub.

**Steps:**

1. **Download WiX Toolset** from https://wixtoolset.org/releases/ (version 6.0 or later)
   See `INSTALL_WIX.md` for installation instructions.

2. **Build your application** first:
   ```build.bat```

3. **Compile the installer**:
   ```build-installer.bat```

The installer will be created in the `dist/` folder.

---

## Build Scripts

### `build-installer.bat`
Automates the build process for WiX:
1. Builds the application
2. Compiles the WiX installer

---

## Troubleshooting

### "File not found" errors
- Verify the publish path in the installer script matches your actual build output
- Check that `dotnet publish` completed successfully

### "Access denied" during installation
- The installer is configured for per-user installation (no admin required)
- If you need system-wide installation, change `InstallScope` to `perMachine` in the WiX script

