@echo off
REM Build script for WiX Toolset installer
REM Requires WiX Toolset to be installed

REM Get the directory where this script is located
set SCRIPT_DIR=%~dp0
set INSTALLER_DIR=%SCRIPT_DIR%
set PROJECT_DIR=%SCRIPT_DIR%..

echo Building NubRub application...
cd /d "%PROJECT_DIR%"
call dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Building WiX installer...
cd /d "%INSTALLER_DIR%"

REM Build the WiX installer using WiX 6.0 unified command
REM The wix build command replaces both candle and light from WiX 3.x
wix build -arch x64 -ext WixToolset.UI.wixext NubRub.wxs -o NubRub.msi
if errorlevel 1 (
    echo.
    echo WiX build failed!
    echo.
    echo If you see "'wix' is not recognized", please:
    echo   1. Install WiX Toolset v6.0 from https://wixtoolset.org/releases/
    echo   2. Add WiX bin directory to your PATH
    echo   3. Restart your command prompt after installation
    echo.
    pause
    exit /b 1
)

REM Clean up build artifacts (CAB files are now embedded in MSI)
echo Cleaning up build artifacts...
if exist "*.cab" del /F /Q "*.cab" >nul 2>&1
if exist "*.wixobj" del /F /Q "*.wixobj" >nul 2>&1
if exist "*.wixpdb" del /F /Q "*.wixpdb" >nul 2>&1

echo.
echo Creating distribution package...
cd /d "%INSTALLER_DIR%"

REM Create dist directory if it doesn't exist
if not exist "dist" mkdir dist

REM Copy the MSI installer to dist folder
copy /Y NubRub.msi dist\NubRub.msi >nul

echo.
echo Installer created successfully!
echo   - MSI: dist\NubRub.msi
pause

