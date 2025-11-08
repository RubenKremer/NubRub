using System.Diagnostics;
using System.Windows.Forms;
using NubRub.Models;
using Microsoft.Win32;
using System.Threading;

namespace NubRub;

static class Program
{
    private static ConfigManager? _configManager;
    private static RawInputHandler? _rawInputHandler;
    private static AudioPlayer? _audioPlayer;
    private static AudioPackManager? _packManager;
    private static WiggleDetector? _wiggleDetector;
    private static TrayIcon? _trayIcon;
    private static MainForm? _mainForm;
    private static ConfigPanel? _configPanel;
    private static DebugWindow? _debugWindow;
    private static AppConfig? _config;

    [STAThread]
    static void Main()
    {
        // Ensure only one instance is running
        bool createdNew;
                using (var mutex = new Mutex(true, "NubRub_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "NubRub is already running.\n\nCheck the system tray for the NubRub icon.",
                    "NubRub - Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            
            try
            {
        ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Initialize components
            _configManager = new ConfigManager();
            _config = _configManager.Load();
            _packManager = new AudioPackManager();

            _rawInputHandler = new RawInputHandler();
            _audioPlayer = new AudioPlayer(_packManager)
            {
                AudioPack = _config.Audio.AudioPack,
                Volume = _config.Audio.Volume,
                IdleCutoffMs = _config.Audio.IdleCutoffMs
            };

            _wiggleDetector = new WiggleDetector();
            _wiggleDetector.WiggleDetected += OnWiggleDetected;

            _trayIcon = new TrayIcon
            {
                Volume = _config.Audio.Volume,
                StartWithWindows = _config.Startup.RunAtLogin
            };

            // Create debug window (but don't show it by default)
            try
            {
                _debugWindow = new DebugWindow();
                // Don't show by default - user can open it from tray menu
                
                // Wait a moment for the form to be fully initialized
                Application.DoEvents();
                System.Threading.Thread.Sleep(100); // Give it time to initialize
                Application.DoEvents();
                
                _debugWindow.Log("NubRub Debug Window");
                _debugWindow.Log("======================");
                _debugWindow.Log("Initializing NubRub...");
            }
            catch (Exception ex)
            {
                // If debug window fails, show message and continue
                MessageBox.Show(
                    $"Failed to create debug window: {ex.Message}\n\nContinuing anyway...",
                    "NubRub - Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            _mainForm = new MainForm();
            _debugWindow?.Log("MainForm created");
            
            // Wire up DebugLog BEFORE registration so we can see registration errors
            _rawInputHandler.DebugLog += (s, msg) => _debugWindow?.Log(msg);
            
            _debugWindow?.Log("Attempting to register Raw Input...");
            bool registered = _mainForm.Initialize(_rawInputHandler);
            if (registered)
            {
                _debugWindow?.Log("Raw Input registered successfully");
            }
            else
            {
                _debugWindow?.Log("ERROR: Failed to register Raw Input!");
                _debugWindow?.Log("This will prevent device enumeration from working.");
                _debugWindow?.Log("Check the logs above for registration error details.");
            }

            // Wire up remaining events (DebugLog already wired above)
            _rawInputHandler.InputReceived += OnRawInputReceived;
            _trayIcon.ConfigPanelRequested += OnConfigPanelRequested;
            _trayIcon.DebugWindowRequested += OnDebugWindowRequested;
            _trayIcon.StartWithWindowsToggled += OnStartWithWindowsToggled;
            _trayIcon.ExitRequested += OnExitRequested;

            // Try to match saved device
            try
            {
                _debugWindow?.Log("Enumerating HID devices...");
                List<DeviceInfo> availableDevices;
                try
                {
                    _debugWindow?.Log("Calling EnumerateDevices()...");
                    _debugWindow?.Log("Step 1: Creating RawInputHandler instance...");
                    
                    if (_rawInputHandler == null)
                    {
                        _debugWindow?.Log("ERROR: RawInputHandler is null!");
                        availableDevices = new List<DeviceInfo>();
                    }
                    else
                    {
                        _debugWindow?.Log("Step 2: RawInputHandler exists, calling EnumerateDevices()...");
                        _debugWindow?.Log($"RawInputHandler type: {_rawInputHandler.GetType().Name}");
                        _debugWindow?.Log($"RawInputHandler is null: {_rawInputHandler == null}");
                        
                        try
                        {
                            _debugWindow?.Log("About to call EnumerateDevices() method...");
                            System.Threading.Thread.Sleep(50); // Small delay
                            Application.DoEvents();
                            
                            if (_rawInputHandler == null)
                            {
                                availableDevices = new List<DeviceInfo>();
                            }
                            else
                            {
                                availableDevices = _rawInputHandler.EnumerateDevices();
                            }
                            _debugWindow?.Log($"EnumerateDevices() completed successfully");
                            _debugWindow?.Log($"Found {availableDevices.Count} device(s) available for selection");
                            
                            if (availableDevices.Count > 0)
                            {
                                _debugWindow?.Log("");
                                _debugWindow?.Log("Devices are now available in the config panel!");
                                _debugWindow?.Log("Right-click the tray icon and select 'Select HID Device' to choose your TrackPoint.");
                            }
                            else
                            {
                                _debugWindow?.Log("");
                                _debugWindow?.Log("WARNING: No devices found. Check the enumeration summary above for details.");
                            }
                        }
                        catch (Exception innerEx)
                        {
                            _debugWindow?.Log($"CRASH inside EnumerateDevices(): {innerEx.Message}");
                            _debugWindow?.Log($"Inner exception type: {innerEx.GetType().Name}");
                            if (innerEx.InnerException != null)
                            {
                                _debugWindow?.Log($"Inner exception: {innerEx.InnerException.Message}");
                            }
                            _debugWindow?.Log($"Stack trace: {innerEx.StackTrace}");
                            _debugWindow?.Log("");
                            _debugWindow?.Log("This crash suggests:");
                            _debugWindow?.Log("  - Memory allocation issue");
                            _debugWindow?.Log("  - Windows API call failure");
                            _debugWindow?.Log("  - Invalid device handle");
                            _debugWindow?.Log("  - Struct marshalling issue");
                            availableDevices = new List<DeviceInfo>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _debugWindow?.Log($"CRASH calling EnumerateDevices(): {ex.Message}");
                    _debugWindow?.Log($"Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        _debugWindow?.Log($"Inner exception: {ex.InnerException.Message}");
                    }
                    _debugWindow?.Log($"Stack trace: {ex.StackTrace}");
                    _debugWindow?.Log("");
                    _debugWindow?.Log("This crash suggests:");
                    _debugWindow?.Log("  - RawInputHandler is null or invalid");
                    _debugWindow?.Log("  - Method call issue");
                    availableDevices = new List<DeviceInfo>();
                }
                
                _debugWindow?.Log($"Found {availableDevices.Count} mouse HID device(s)");
                
                if (availableDevices.Count == 0)
                {
                    _debugWindow?.Log("");
                    _debugWindow?.Log("TROUBLESHOOTING:");
                    _debugWindow?.Log("1. Check Visual Studio Output window (Debug) for detailed device info");
                    _debugWindow?.Log("2. Try running as Administrator");
                    _debugWindow?.Log("3. Ensure TrackPoint is enabled in BIOS/Windows settings");
                    _debugWindow?.Log("4. Check Device Manager for HID-compliant mouse devices");
                }
                
                if (availableDevices.Count == 0)
                {
                    _debugWindow?.Log("WARNING: No devices found. This might be due to:");
                    _debugWindow?.Log("  - Raw Input not registered properly");
                    _debugWindow?.Log("  - No mouse devices connected");
                    _debugWindow?.Log("  - Permission issues");
                    
                    // Check if we're running as admin
                    try
                    {
                        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                        var principal = new System.Security.Principal.WindowsPrincipal(identity);
                        bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                        if (!isAdmin)
                        {
                            _debugWindow?.Log("NOTE: Not running as Administrator - this may cause device access issues");
                        }
                        else
                        {
                            _debugWindow?.Log("Running as Administrator - permissions should be OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        _debugWindow?.Log($"Could not check admin status: {ex.Message}");
                    }
                }
                
                foreach (var device in availableDevices)
                {
                    try
                    {
                        _debugWindow?.LogDeviceInfo($"{device.DisplayName} - Handle: {device.Handle}");
                    }
                    catch (Exception ex)
                    {
                        _debugWindow?.Log($"Error logging device info: {ex.Message}");
                    }
                }

                try
                {
                    var matchedDevice = _configManager?.FindMatchingDevice(availableDevices, _config?.SelectedDevice);
                    if (matchedDevice != null && _rawInputHandler != null)
                    {
                        _rawInputHandler.SetSelectedDevice(matchedDevice.Handle);
                        _debugWindow?.LogDeviceInfo($"Selected device: {matchedDevice.DisplayName}");
                    }
                    else
                    {
                        // No device selected - show auto-detection window
                        _debugWindow?.Log("No device selected. Starting auto-detection...");
                        if (availableDevices.Count > 0)
                        {
                            ShowDeviceDetectionWindow(availableDevices);
                        }
                        else
                        {
                            _debugWindow?.Log("No devices found. Use tray menu to select a device.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _debugWindow?.Log($"Error matching device: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _debugWindow?.Log($"FATAL ERROR during device enumeration on startup: {ex.Message}");
                _debugWindow?.Log($"Exception type: {ex.GetType().Name}");
                _debugWindow?.Log($"Stack trace: {ex.StackTrace}");
                _debugWindow?.Log("Use tray menu to select a device.");
            }

            // Set up startup registry if needed
            if (_config != null)
            {
                UpdateStartupRegistry(_config.Startup.RunAtLogin);
            }

                Application.Run(_mainForm);
            }
            catch (Exception ex)
            {
                // Try to show debug window first
                try
                {
                    if (_debugWindow == null)
                    {
                        _debugWindow = new DebugWindow();
                        _debugWindow.Show();
                        Application.DoEvents();
                    }
                    
                    _debugWindow.Log($"FATAL ERROR: {ex.Message}");
                    _debugWindow.Log($"Stack Trace: {ex.StackTrace}");
                    _debugWindow.Log("");
                    _debugWindow.Log("Application will remain open for debugging.");
                    _debugWindow.Show();
                }
                catch
                {
                    // If debug window fails, show message box
                }
                
                // Show error message
                MessageBox.Show(
                    $"An error occurred while starting NubRub:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nDebug window should be open for more details.",
                    "NubRub - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                
                // Keep the application running so debug window stays open
                if (_mainForm == null)
                {
                    _mainForm = new MainForm();
                }
                Application.Run(_mainForm);
            }
            // Mutex will be released when app exits
        }
    }

    private static void OnRawInputReceived(object? sender, RawInputEventArgs e)
    {
        // Only log raw input if device is selected (to reduce spam)
        if (_config?.SelectedDevice != null)
        {
            _debugWindow?.LogRawInput(e);
        }

        // Only process if device is selected
        if (_config == null)
        {
            return;
        }
        if (_config.SelectedDevice == null)
        {
            // No device selected - ignore all input
            _debugWindow?.Log("Input received but no device selected");
            return;
        }
        if (_config.Audio.OnlyOnMovement && !e.HasMovement) return;

        if (e.HasMovement)
        {
            _audioPlayer?.OnMovement();
            _wiggleDetector?.OnMovement();
            _debugWindow?.LogAudio("Squeak started");
        }
    }

    private static void OnWiggleDetected(object? sender, EventArgs e)
    {
        // Stop squeak, play trigger sound, then execute tel: link
        _debugWindow?.LogWiggle("45 seconds of continuous wiggling detected!");
        _audioPlayer?.StopSqueak();
        _audioPlayer?.PlayTriggerSound();
        _debugWindow?.LogAudio("Playing trigger sound...");
        _audioPlayer!.TriggerSoundCompleted += OnTriggerSoundCompleted;
    }

    private static void OnTriggerSoundCompleted(object? sender, EventArgs e)
    {
        _audioPlayer!.TriggerSoundCompleted -= OnTriggerSoundCompleted;
        
        // Trigger sound completed - no tel: link execution
        _debugWindow?.LogWiggle("Trigger sound completed");
        
        // Reset wiggle detector
        _wiggleDetector?.Reset();
        _debugWindow?.LogWiggle("Wiggle detector reset - ready for next 25s cycle");
    }

    private static void OnConfigPanelRequested(object? sender, EventArgs e)
    {
        ShowConfigPanel();
    }

    private static void OnDebugWindowRequested(object? sender, EventArgs e)
    {
        if (_debugWindow != null)
        {
            if (_debugWindow.Visible)
            {
                _debugWindow.Hide();
            }
            else
            {
                _debugWindow.Show();
                _debugWindow.BringToFront();
            }
        }
    }

    private static void ShowDeviceDetectionWindow(List<DeviceInfo> availableDevices)
    {
        if (_mainForm == null || _rawInputHandler == null) return;
        
        try
        {
            // Clear device selection to allow all input through
            _rawInputHandler.ClearSelectedDevice();
            
            var detectionWindow = new DeviceDetectionWindow();
            DeviceInfo? detectedDevice = null;
            
            // Set up event handler to detect input
            EventHandler<RawInputEventArgs>? detectionHandler = null;
            detectionHandler = (sender, e) =>
            {
                if (e.HasMovement)
                {
                    // Find which device this handle belongs to
                    var device = availableDevices.FirstOrDefault(d => d.Handle == e.DeviceHandle);
                    if (device != null)
                    {
                        detectedDevice = device;
                        detectionWindow.DeviceDetected(device);
                        _rawInputHandler.InputReceived -= detectionHandler;
                    }
                }
            };
            
            _rawInputHandler.InputReceived += detectionHandler;
            
            // Show the window
            if (detectionWindow.ShowDialog(_mainForm) == DialogResult.OK && detectedDevice != null)
            {
                // Save the detected device
                _config!.SelectedDevice = detectedDevice;
                _rawInputHandler.SetSelectedDevice(detectedDevice.Handle);
                _configManager?.Save(_config);
                
                _debugWindow?.LogDeviceInfo($"Auto-detected device: {detectedDevice.DisplayName}");
                _debugWindow?.Log("Device saved. You can change it from the tray menu.");
            }
            else
            {
                _rawInputHandler.InputReceived -= detectionHandler;
                _debugWindow?.Log("Device detection cancelled.");
            }
        }
        catch (Exception ex)
        {
            _debugWindow?.Log($"ERROR in ShowDeviceDetectionWindow: {ex.Message}");
            _debugWindow?.Log($"Stack Trace: {ex.StackTrace}");
        }
    }
    
    private static void ShowConfigPanel()
    {
        // Ensure we're on the UI thread
        if (_mainForm != null && _mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action(ShowConfigPanel));
            return;
        }

        try
        {
            if (_rawInputHandler == null || _config == null)
            {
                _debugWindow?.Log("Cannot show config panel: RawInputHandler or Config is null");
                return;
            }

            // Dispose old config panel if it exists
            if (_configPanel != null)
            {
                try
                {
                    _configPanel.Dispose();
                }
                catch { }
                _configPanel = null;
            }

            _debugWindow?.Log("Enumerating HID devices...");
            List<DeviceInfo> devices;
            try
            {
                devices = _rawInputHandler.EnumerateDevices();
                _debugWindow?.Log($"Found {devices.Count} device(s) for selection");
            }
            catch (Exception ex)
            {
                _debugWindow?.Log($"ERROR enumerating devices: {ex.Message}");
                MessageBox.Show(
                    $"An error occurred while enumerating HID devices:\n\n{ex.Message}",
                    "NubRub - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (devices.Count == 0)
            {
                MessageBox.Show(
                    "No HID mouse devices found. Please ensure your TrackPoint is connected.",
                    "NubRub - No Devices",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                _configPanel = new ConfigPanel(
                    devices,
                    _config.SelectedDevice,
                    _config.Audio.AudioPack,
                    _config.Audio.IdleCutoffMs,
                    _config.Audio.OnlyOnMovement,
                    _config.Audio.Volume,
                    _packManager);

                // Wire up auto-detect event
                _configPanel.AutoDetectRequested += (s, e) =>
                {
                    // Re-enumerate devices to get fresh list
                    try
                    {
                        List<DeviceInfo> freshDevices = _rawInputHandler.EnumerateDevices();
                        if (freshDevices.Count > 0)
                        {
                            // Store the current device before detection
                            DeviceInfo? previousDevice = _config?.SelectedDevice;
                            
                            // Show detection window (modal, will block until closed)
                            ShowDeviceDetectionWindow(freshDevices);
                            
                            // After detection, refresh the device list with the newly detected device
                            try
                            {
                                List<DeviceInfo> updatedDevices = _rawInputHandler.EnumerateDevices();
                                // Use the current config's selected device (which may have been updated by ShowDeviceDetectionWindow)
                                _configPanel.PopulateDevices(updatedDevices, _config?.SelectedDevice);
                            }
                            catch (Exception ex)
                            {
                                _debugWindow?.Log($"Error refreshing device list after auto-detect: {ex.Message}");
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "No HID mouse devices found. Please ensure your TrackPoint is connected.",
                                "NubRub - No Devices",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"An error occurred while enumerating devices:\n\n{ex.Message}",
                            "NubRub - Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                };

                if (_configPanel.ShowDialog(_mainForm) == DialogResult.OK)
                {
                    // Save settings
                    _config.SelectedDevice = _configPanel.SelectedDevice;
                    _config.Audio.AudioPack = _configPanel.AudioPack;
                    _config.Audio.IdleCutoffMs = _configPanel.IdleCutoffMs;
                    _config.Audio.OnlyOnMovement = _configPanel.OnlyOnMovement;
                    _config.Audio.Volume = _configPanel.Volume;

                    _configManager?.Save(_config);

                    // Update components
                    if (_config.SelectedDevice != null)
                    {
                        _rawInputHandler?.SetSelectedDevice(_config.SelectedDevice.Handle);
                        _debugWindow?.LogDeviceInfo($"Device selected: {_config.SelectedDevice.DisplayName}");
                    }
                    else
                    {
                        _rawInputHandler?.ClearSelectedDevice();
                        _debugWindow?.LogDeviceInfo("Device selection cleared");
                    }

                    if (_audioPlayer != null)
                    {
                        _audioPlayer.AudioPack = _configPanel.AudioPack;
                        _audioPlayer.Volume = _configPanel.Volume;
                        _audioPlayer.IdleCutoffMs = _configPanel.IdleCutoffMs;
                    }
                    _trayIcon!.Volume = _config.Audio.Volume;
                }
            }
            finally
            {
                // Dispose the config panel after use
                if (_configPanel != null)
                {
                    try
                    {
                        _configPanel.Dispose();
                    }
                    catch { }
                    _configPanel = null;
                }
            }
        }
        catch (Exception ex)
        {
            _debugWindow?.Log($"ERROR in ShowConfigPanel: {ex.Message}");
            _debugWindow?.Log($"Stack Trace: {ex.StackTrace}");
            MessageBox.Show(
                $"An error occurred while showing the configuration panel:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "NubRub - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void OnStartWithWindowsToggled(object? sender, bool runAtLogin)
    {
        if (_config != null)
        {
            _config.Startup.RunAtLogin = runAtLogin;
            _configManager?.Save(_config);
            UpdateStartupRegistry(runAtLogin);
        }
    }

    private static void UpdateStartupRegistry(bool runAtLogin)
    {
        try
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null) return;

            if (runAtLogin)
            {
                key.SetValue("NubRub", Application.ExecutablePath);
            }
            else
            {
                key.DeleteValue("NubRub", false);
            }
        }
        catch
        {
            // Silently fail on registry errors
        }
    }

    private static void OnExitRequested(object? sender, EventArgs e)
    {
        _debugWindow?.Log("Application shutting down...");
        _audioPlayer?.Dispose();
        _wiggleDetector?.Dispose();
        _trayIcon?.Dispose();
        _debugWindow?.Dispose();
        Application.Exit();
    }
}
