# PowerShell script to update winget manifest for a new version
# Usage: .\update-manifest.ps1 -Version "1.1.1" -InstallerPath "..\installer\dist\NubRub.msi" -ReleaseUrl "https://github.com/RubenKremer/NubRub/releases/download/v1.1.1/NubRub-1.1.1.msi"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$InstallerPath,
    
    [Parameter(Mandatory=$true)]
    [string]$ReleaseUrl,
    
    [string]$PackageIdentifier = "R.Kremer.NubRub",
    [string]$Publisher = "R. Kremer",
    [string]$PackageName = "NubRub",
    [string]$License = "PolyForm Noncommercial License 1.0.0",
    [string]$ShortDescription = "Adds swappable sound packs to your ThinkPad TrackPoint on Windows 11.",
    [string]$Locale = "en-US",
    [string]$ManifestVersion = "1.10.0"
)

# Validate installer exists
if (-not (Test-Path $InstallerPath)) {
    Write-Error "Installer not found at: $InstallerPath"
    exit 1
}

# Calculate SHA256 hash
Write-Host "Calculating SHA256 hash..." -ForegroundColor Cyan
$hash = (Get-FileHash -Path $InstallerPath -Algorithm SHA256).Hash
Write-Host "SHA256: $hash" -ForegroundColor Green

# Extract ProductCode from MSI
Write-Host "Extracting ProductCode from MSI..." -ForegroundColor Cyan
try {
    $msi = New-Object -ComObject WindowsInstaller.Installer
    $database = $msi.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $msi, @((Resolve-Path $InstallerPath).Path, 0))
    $view = $database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $database, "SELECT Value FROM Property WHERE Property='ProductCode'")
    $view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)
    $record = $view.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $view, $null)
    $productCode = $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, 1)
    $view.GetType().InvokeMember("Close", "InvokeMethod", $null, $view, $null)
    Write-Host "ProductCode: $productCode" -ForegroundColor Green
} catch {
    Write-Warning "Could not extract ProductCode automatically. You'll need to add it manually."
    $productCode = "{YOUR-PRODUCT-CODE-HERE}"
}

# Create version directory
$versionDir = "r\$($PackageIdentifier.Replace('.', '\'))\$Version"
$fullPath = Join-Path $PSScriptRoot $versionDir
Write-Host "Creating directory: $fullPath" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $fullPath | Out-Null

# Create version manifest
$versionManifest = @"
# Created using update-manifest.ps1
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$ManifestVersion.schema.json

PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
DefaultLocale: $Locale
ManifestType: version
ManifestVersion: $ManifestVersion
"@

$versionManifestPath = Join-Path $fullPath "$PackageIdentifier.yaml"
Write-Host "Creating version manifest: $versionManifestPath" -ForegroundColor Cyan
$versionManifest | Out-File -FilePath $versionManifestPath -Encoding UTF8

# Create locale manifest
$localeManifest = @"
# Created using update-manifest.ps1
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$ManifestVersion.schema.json

PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
PackageLocale: $Locale
Publisher: $Publisher
PackageName: $PackageName
License: $License
ShortDescription: $ShortDescription
ManifestType: defaultLocale
ManifestVersion: $ManifestVersion
"@

$localeManifestPath = Join-Path $fullPath "$PackageIdentifier.locale.$Locale.yaml"
Write-Host "Creating locale manifest: $localeManifestPath" -ForegroundColor Cyan
$localeManifest | Out-File -FilePath $localeManifestPath -Encoding UTF8

# Create installer manifest
$installerManifest = @"
# Created using update-manifest.ps1
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$ManifestVersion.schema.json

PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
InstallerLocale: $Locale
InstallerType: wix
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
InstallerSwitches:
  Silent: /quiet
  SilentWithProgress: /qb
ProductCode: '$productCode'
Installers:
- Architecture: x64
  InstallerUrl: $ReleaseUrl
  InstallerSha256: $hash
ManifestType: installer
ManifestVersion: $ManifestVersion
"@

$installerManifestPath = Join-Path $fullPath "$PackageIdentifier.installer.yaml"
Write-Host "Creating installer manifest: $installerManifestPath" -ForegroundColor Cyan
$installerManifest | Out-File -FilePath $installerManifestPath -Encoding UTF8

Write-Host "`nManifest files created successfully!" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Directory: $fullPath" -ForegroundColor Yellow
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Review the generated manifest files" -ForegroundColor White
Write-Host "2. Validate with: winget validate $fullPath" -ForegroundColor White
Write-Host "3. Test locally with: winget install --manifest $fullPath" -ForegroundColor White

