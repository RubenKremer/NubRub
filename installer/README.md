# NubRub Installer Setup Guide

This directory contains installer scripts for creating a Windows installer for NubRub.

**Steps:**

1. **Download WiX Toolset** from https://wixtoolset.org/releases/ (version 3.11 or later)

2. **Generate GUIDs**:
   - Use PowerShell: `[guid]::NewGuid()`
   - Replace these placeholders in `NubRub.wxs`:
     - `YOUR-UPGRADE-GUID-HERE` (for UpgradeCode)
     - `YOUR-COMPONENT-GUID-HERE` (for ApplicationFiles component)
     - `YOUR-SHORTCUT-GUID-HERE` (for ApplicationShortcut component)
     - `YOUR-DESKTOP-GUID-HERE` (for DesktopShortcut component)

3. **Build your application** first:
   ```build.bat```

4. **Update the installer script**:
   - Open `NubRub.wxs`
   - Update `Manufacturer` with your name/company
   - Verify the `Source` path points to your published executable

5. **Compile the installer**:
   ```build-installer.bat```

---

### 3. MSIX (Modern Windows packaging)

**Pros:**
- Modern Windows 10/11 packaging format
- Automatic updates via Microsoft Store
- Sandboxed execution
- Better security

**Cons:**
- Requires code signing for best experience
- More complex setup
- Limited to Windows 10/11

**Steps:**

1. **Install Windows SDK** (includes MSIX packaging tools)

2. **Use MSIX Packaging Tool** or **MakeAppx.exe** to create the package

3. **Sign the package** (optional but recommended)

---

## Build Scripts

### `build-installer.bat`
Automates the build process for WiX:
1. Builds the application
2. Compiles the WiX installer

---

## Customization

### Adding License File
1. Create a `License.rtf` file
2. Update the installer script to reference it:
   - WiX: Already configured in the script

### Adding Readme/Info Files
1. Create `Readme.txt` or `Info.rtf`
2. Update the installer script as needed

### Custom Icons and Images
- **Banner**: 55x55 pixels (WiX)
- **Dialog**: 164x314 pixels (WiX)
- **App Icon**: Already configured from `Resources\icons\appicon.ico`

---

## Testing the Installer

1. **Test on a clean system** (or VM) without the application installed
2. **Test installation**:
   - Install to default location
   - Install to custom location
   - Install with/without desktop shortcut
3. **Test uninstallation**:
   - Verify all files are removed
   - Verify registry entries are cleaned up
   - Verify shortcuts are removed
4. **Test upgrade**:
   - Install version 1.0
   - Install version 1.1 (should upgrade, not duplicate)

---

## Distribution

### Code Signing (Recommended)
For production releases, you should sign your installer:
- **WiX**: Use `signtool.exe` after building

### Virus Scanning
Some antivirus software may flag unsigned installers. Code signing helps reduce false positives.

---

## Troubleshooting

### "File not found" errors
- Verify the publish path in the installer script matches your actual build output
- Check that `dotnet publish` completed successfully

### "Access denied" during installation
- The installer is configured for per-user installation (no admin required)
- If you need system-wide installation, change `InstallScope` to `perMachine` in the WiX script

### Installer is too large
- Since you're using self-contained deployment, the installer will be large (~50-100MB)
- Consider using framework-dependent deployment and requiring .NET 8 runtime separately

---

## Additional Resources

- **WiX Toolset Documentation**: https://wixtoolset.org/documentation/
- **MSIX Documentation**: https://docs.microsoft.com/en-us/windows/msix/

