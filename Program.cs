using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using NubRub.Models;
using NubRub.Core;
using NubRub.Services;
using NubRub.UI;
using NubRub.Utilities;
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
    static void Main(string[] args)
    {
        bool createdNew;
                using (var mutex = new Mutex(true, "NubRub_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
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

            _wiggleDetector = new WiggleDetector(_config.Audio.WiggleDurationMs);
            _wiggleDetector.WiggleDetected += OnWiggleDetected;

            _trayIcon = new TrayIcon
            {
                Volume = _config.Audio.Volume,
                StartWithWindows = _config.Startup.RunAtLogin
            };

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
            
            string? nubrubFilePath = null;
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string arg = args[0].Trim('"');
                if (File.Exists(arg) && Path.GetExtension(arg).Equals(".nubrub", StringComparison.OrdinalIgnoreCase))
                {
                    nubrubFilePath = arg;
                }
            }
            
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
            _trayIcon.UpdateCheckRequested += OnUpdateCheckRequested;
            _trayIcon.ExitRequested += OnExitRequested;
            _trayIcon.AudioPackCreateRequested += OnAudioPackCreateRequested;
            _trayIcon.AudioPackEditRequested += OnAudioPackEditRequested;
            _trayIcon.AudioPackExportRequested += OnAudioPackExportRequested;
            _trayIcon.AudioPackImportRequested += OnAudioPackImportRequested;
            _trayIcon.AudioPackDeleteRequested += OnAudioPackDeleteRequested;

            // Try to match saved device
            EnumerateAndMatchDevices();

            if (_config != null)
            {
                UpdateStartupRegistry(_config.Startup.RunAtLogin);
            }

            // Register file association for .nubrub files if not already registered
            try
            {
                if (!FileAssociationManager.IsFileAssociationRegistered())
                {
                    string iconPath = FileAssociationManager.GetIconPath();
                    FileAssociationManager.RegisterFileAssociation(Application.ExecutablePath, iconPath);
                    _debugWindow?.Log("File association registered for .nubrub files");
                }
            }
            catch (Exception ex)
            {
                _debugWindow?.Log($"Failed to register file association: {ex.Message}");
            }

            // If a .nubrub file was provided via command line, import it after a short delay
            if (nubrubFilePath != null && _packManager != null)
            {
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    if (_mainForm != null && _mainForm.InvokeRequired)
                    {
                        _mainForm.Invoke(new Action(() => ImportNubrubFile(nubrubFilePath)));
                    }
                    else
                    {
                        ImportNubrubFile(nubrubFilePath);
                    }
                });
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

    private static void EnumerateAndMatchDevices()
    {
        try
        {
            _debugWindow?.Log("Enumerating HID devices...");
            List<DeviceInfo> availableDevices;
            try
            {
                if (_rawInputHandler == null)
                {
                    _debugWindow?.Log("ERROR: RawInputHandler is null!");
                    availableDevices = new List<DeviceInfo>();
                }
                else
                {
                    try
                    {
                        System.Threading.Thread.Sleep(50);
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
    }

    private static void OnWiggleDetected(object? sender, EventArgs e)
    {
        // Stop squeak, play trigger sound, then execute tel: link
        int durationSeconds = _wiggleDetector != null ? _wiggleDetector.WiggleDurationMs / 1000 : 25;
        _debugWindow?.LogWiggle($"{durationSeconds} seconds of continuous wiggling detected!");
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
        int durationSeconds = _wiggleDetector != null ? _wiggleDetector.WiggleDurationMs / 1000 : 25;
        _debugWindow?.LogWiggle($"Wiggle detector reset - ready for next {durationSeconds}s cycle");
    }

    private static void OnConfigPanelRequested(object? sender, EventArgs e)
    {
        ShowConfigPanel();
    }

    private static void OnAudioPackCreateRequested(object? sender, EventArgs e)
    {
        if (_mainForm == null)
            return;

        if (_mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnAudioPackCreateRequested), sender, e);
            return;
        }

        HandleAudioPackCreate();
    }

    private static void HandleAudioPackCreate()
    {
        try
        {
            // Determine the owner form - use config panel if it's open, otherwise use null (top-level)
            Form? ownerForm = null;
            if (_configPanel != null && _configPanel.Visible)
            {
                ownerForm = _configPanel;
            }
            // MainForm is always hidden, so we use null to make it a top-level window

            var wizard = new AudioPackWizard(_packManager);
            if (wizard.ShowDialog(ownerForm) == DialogResult.OK && !string.IsNullOrEmpty(wizard.CreatedPackId))
            {
                // Pack was created successfully, select it
                if (_config != null && _packManager != null)
                {
                    var createdPack = _packManager.GetPack(wizard.CreatedPackId);
                    string packName = createdPack?.Name ?? wizard.CreatedPackId;
                    
                    _config.Audio.AudioPack = wizard.CreatedPackId;
                    _configManager?.Save(_config);
                    
                    // Refresh config panel if open
                    if (_configPanel != null && _configPanel.Visible)
                    {
                        try
                        {
                            _configPanel.PopulateAudioPacks(wizard.CreatedPackId);
                        }
                        catch { }
                    }
                    
                    // Reload audio player with new pack
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.AudioPack = wizard.CreatedPackId;
                    }
                    
                    // Show success message
                    MessageBox.Show(
                        $"Audio pack '{packName}' has been created successfully and is now selected as the current pack.",
                        "Pack Created",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open wizard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OnAudioPackEditRequested(object? sender, EventArgs e)
    {
        if (_mainForm == null || _packManager == null)
            return;

        if (_mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnAudioPackEditRequested), sender, e);
            return;
        }

        HandleAudioPackEdit();
    }

    private static void HandleAudioPackEdit()
    {
        if (_packManager == null)
            return;

        // Declare variables at method scope so they're accessible in catch block
        AudioPackInfo? selectedPack = null;
        string? previousPackId = null;
        bool isCurrentlyActive = false;

        try
        {
            var allPacks = _packManager.GetAllPacks();
            var customPacks = allPacks.Where(p => !p.IsBuiltIn).ToList();

            if (customPacks.Count == 0)
            {
                MessageBox.Show(
                    "No custom audio packs available to edit.\n\nCreate a custom pack first using the 'Create...' option.",
                    "No Packs Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Show pack selection dialog
            using var selectDialog = new Form
            {
                Text = "Edit Audio Pack",
                Size = new System.Drawing.Size(400, 200),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterScreen
            };

            var label = new Label
            {
                Text = "Select pack to edit:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            selectDialog.Controls.Add(label);

            var packComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(340, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var pack in customPacks)
            {
                string displayName = string.IsNullOrWhiteSpace(pack.Version)
                    ? pack.Name
                    : $"{pack.Name} ({pack.Version})";
                packComboBox.Items.Add(new { DisplayName = displayName, Pack = pack });
            }
            packComboBox.DisplayMember = "DisplayName";
            packComboBox.SelectedIndex = 0;
            selectDialog.Controls.Add(packComboBox);

            var okButton = new Button
            {
                Text = "Edit",
                Location = new System.Drawing.Point(200, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.OK
            };
            selectDialog.Controls.Add(okButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(285, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.Cancel
            };
            selectDialog.Controls.Add(cancelButton);
            selectDialog.CancelButton = cancelButton;
            selectDialog.AcceptButton = okButton;

            // Determine the owner form
            Form? ownerForm = null;
            if (_configPanel != null && _configPanel.Visible)
            {
                ownerForm = _configPanel;
            }
            
            if (selectDialog.ShowDialog(ownerForm) == DialogResult.OK && packComboBox.SelectedItem != null)
            {
                var selectedItem = packComboBox.SelectedItem;
                var packProperty = selectedItem.GetType().GetProperty("Pack");
                if (packProperty != null)
                {
                    selectedPack = packProperty.GetValue(selectedItem) as AudioPackInfo;
                    if (selectedPack != null)
                    {
                        isCurrentlyActive = _config != null && 
                            selectedPack.PackId == _config.Audio.AudioPack;
                        
                        // Always release file locks and disable AudioPlayer when editing
                        // This ensures files are not locked, even if pack is not currently active
                        if (_audioPlayer != null)
                        {
                            previousPackId = _audioPlayer.AudioPack;
                            
                            // Step 1: Stop all audio playback first
                            _audioPlayer.StopSqueak();
                            
                            if (isCurrentlyActive)
                            {
                                // Step 2: Switch to a built-in pack first (this releases file locks)
                                var availablePacks = _packManager.GetAllPacks();
                                var builtInPack = availablePacks.FirstOrDefault(p => p.IsBuiltIn);
                                if (builtInPack != null)
                                {
                                    _audioPlayer.AudioPack = builtInPack.PackId;
                                    // Give it more time to fully release the file locks
                                    System.Threading.Thread.Sleep(500);
                                    
                                    // Double-check: ensure we're not still using the custom pack
                                    if (_audioPlayer.AudioPack == selectedPack.PackId)
                                    {
                                        // Force release locks and try again
                                        _audioPlayer.ReleaseFileLocks();
                                        _audioPlayer.AudioPack = builtInPack.PackId;
                                        System.Threading.Thread.Sleep(500);
                                    }
                                }
                                else
                                {
                                    // If no built-in pack available, just release locks
                                    _audioPlayer.ReleaseFileLocks();
                                    System.Threading.Thread.Sleep(500);
                                }
                            }
                            else
                            {
                                // Pack is not active, but release locks anyway to be safe
                                // This handles cases where files might still be locked from previous loads
                                _audioPlayer.ReleaseFileLocks();
                                System.Threading.Thread.Sleep(200);
                            }
                            
                            // Step 3: Disable AudioPlayer to prevent it from responding to movement
                            _audioPlayer.IsEnabled = false;
                        }
                        
                        // Open wizard in edit mode
                        var wizard = new AudioPackWizard(_packManager, selectedPack, _audioPlayer);
                        if (wizard.ShowDialog(ownerForm) == DialogResult.OK && !string.IsNullOrEmpty(wizard.CreatedPackId))
                        {
                            // Pack was updated/created successfully
                            if (_config != null && _packManager != null)
                            {
                                var updatedPack = _packManager.GetPack(wizard.CreatedPackId);
                                string packName = updatedPack?.Name ?? wizard.CreatedPackId;
                                
                                _config.Audio.AudioPack = wizard.CreatedPackId;
                                _configManager?.Save(_config);
                                
                                // Refresh config panel if open
                                if (_configPanel != null && _configPanel.Visible)
                                {
                                    try
                                    {
                                        _configPanel.PopulateAudioPacks(wizard.CreatedPackId);
                                    }
                                    catch { }
                                }
                                
                                // Step 5: Restore the original pack (or updated pack) and re-enable AudioPlayer
                                if (_audioPlayer != null)
                                {
                                    _audioPlayer.AudioPack = wizard.CreatedPackId;
                                    // Re-enable AudioPlayer
                                    _audioPlayer.IsEnabled = true;
                                }
                                
                                // Show success message
                                string message = selectedPack.PackId == wizard.CreatedPackId
                                    ? $"Audio pack '{packName}' has been updated successfully."
                                    : $"Audio pack '{packName}' has been created successfully.";
                                
                                MessageBox.Show(
                                    message,
                                    "Pack Saved",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            // Wizard was cancelled - restore the previous pack and re-enable AudioPlayer
                            if (isCurrentlyActive && previousPackId != null && _audioPlayer != null)
                            {
                                _audioPlayer.AudioPack = previousPackId;
                                // Re-enable AudioPlayer
                                _audioPlayer.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to edit audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            // On error, try to restore the previous pack if it was active
            if (selectedPack != null && isCurrentlyActive && previousPackId != null && _audioPlayer != null)
            {
                try
                {
                    _audioPlayer.AudioPack = previousPackId;
                }
                catch { }
            }
        }
    }

    private static void OnAudioPackExportRequested(object? sender, EventArgs e)
    {
        if (_mainForm == null || _packManager == null)
            return;

        if (_mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnAudioPackExportRequested), sender, e);
            return;
        }

        HandleAudioPackExport();
    }

    private static void HandleAudioPackExport()
    {
        if (_packManager == null)
            return;

        try
        {
            var allPacks = _packManager.GetAllPacks();
            var customPacks = allPacks.Where(p => !p.IsBuiltIn).ToList();

            if (customPacks.Count == 0)
            {
                MessageBox.Show(
                    "No custom audio packs available to export.\n\nCreate a custom pack first using the 'Create...' option.",
                    "No Packs Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Show pack selection dialog
            using var selectDialog = new Form
            {
                Text = "Export Audio Pack",
                Size = new System.Drawing.Size(400, 200),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterScreen
            };

            var label = new Label
            {
                Text = "Select pack to export:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            selectDialog.Controls.Add(label);

            var packComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(340, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var pack in customPacks)
            {
                string displayName = string.IsNullOrWhiteSpace(pack.Version)
                    ? pack.Name
                    : $"{pack.Name} ({pack.Version})";
                packComboBox.Items.Add(new { DisplayName = displayName, Pack = pack });
            }
            packComboBox.DisplayMember = "DisplayName";
            packComboBox.SelectedIndex = 0;
            selectDialog.Controls.Add(packComboBox);

            var okButton = new Button
            {
                Text = "Export",
                Location = new System.Drawing.Point(200, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.OK
            };
            selectDialog.Controls.Add(okButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(285, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.Cancel
            };
            selectDialog.Controls.Add(cancelButton);
            selectDialog.CancelButton = cancelButton;
            selectDialog.AcceptButton = okButton;

            if (selectDialog.ShowDialog(_mainForm) == DialogResult.OK && packComboBox.SelectedItem != null)
            {
                var selectedItem = packComboBox.SelectedItem;
                var packProperty = selectedItem.GetType().GetProperty("Pack");
                if (packProperty != null)
                {
                    var selectedPack = packProperty.GetValue(selectedItem) as AudioPackInfo;
                    if (selectedPack != null)
                    {
                        // Show save dialog
                        using var saveDialog = new SaveFileDialog
                        {
                            Filter = "NubRub Audio Pack (*.nubrub)|*.nubrub|All files (*.*)|*.*",
                            FilterIndex = 1,
                            DefaultExt = "nubrub",
                            FileName = $"{selectedPack.Name}.nubrub"
                        };

                        if (saveDialog.ShowDialog() == DialogResult.OK)
                        {
                            if (_packManager.ExportPack(selectedPack, saveDialog.FileName))
                            {
                                MessageBox.Show(
                                    $"Audio pack '{selectedPack.Name}' has been exported successfully.",
                                    "Export Successful",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to export audio pack '{selectedPack.Name}'.",
                                    "Export Failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OnAudioPackImportRequested(object? sender, EventArgs e)
    {
        if (_mainForm == null || _packManager == null)
            return;

        if (_mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnAudioPackImportRequested), sender, e);
            return;
        }

        HandleAudioPackImport();
    }

    private static void HandleAudioPackImport()
    {
        try
        {
            using var openDialog = new OpenFileDialog
            {
                Filter = "NubRub Audio Pack (*.nubrub)|*.nubrub|All files (*.*)|*.*",
                FilterIndex = 1,
                Title = "Import Audio Pack"
            };

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                ImportNubrubFile(openDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OnAudioPackDeleteRequested(object? sender, EventArgs e)
    {
        if (_mainForm == null || _packManager == null)
            return;

        if (_mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnAudioPackDeleteRequested), sender, e);
            return;
        }

        HandleAudioPackDelete();
    }

    private static void HandleAudioPackDelete()
    {
        if (_packManager == null)
            return;

        try
        {
            var allPacks = _packManager.GetAllPacks();
            var customPacks = allPacks.Where(p => !p.IsBuiltIn).ToList();

            if (customPacks.Count == 0)
            {
                MessageBox.Show(
                    "No custom audio packs available to delete.\n\nOnly custom packs can be deleted. Built-in packs cannot be removed.",
                    "No Packs Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Show pack selection dialog
            using var selectDialog = new Form
            {
                Text = "Delete Audio Pack",
                Size = new System.Drawing.Size(400, 200),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterScreen
            };

            var label = new Label
            {
                Text = "Select pack to delete:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            selectDialog.Controls.Add(label);

            var packComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(340, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var pack in customPacks)
            {
                string displayName = string.IsNullOrWhiteSpace(pack.Version)
                    ? pack.Name
                    : $"{pack.Name} ({pack.Version})";
                packComboBox.Items.Add(new { DisplayName = displayName, Pack = pack });
            }
            packComboBox.DisplayMember = "DisplayName";
            packComboBox.SelectedIndex = 0;
            selectDialog.Controls.Add(packComboBox);

            var okButton = new Button
            {
                Text = "Delete",
                Location = new System.Drawing.Point(200, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.OK
            };
            selectDialog.Controls.Add(okButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(285, 100),
                Size = new System.Drawing.Size(75, 30),
                DialogResult = DialogResult.Cancel
            };
            selectDialog.Controls.Add(cancelButton);
            selectDialog.CancelButton = cancelButton;
            selectDialog.AcceptButton = okButton;

            // Determine the owner form
            Form? ownerForm = null;
            if (_configPanel != null && _configPanel.Visible)
            {
                ownerForm = _configPanel;
            }
            
            if (selectDialog.ShowDialog(ownerForm) == DialogResult.OK && packComboBox.SelectedItem != null)
            {
                var selectedItem = packComboBox.SelectedItem;
                var packProperty = selectedItem.GetType().GetProperty("Pack");
                if (packProperty != null)
                {
                    var selectedPack = packProperty.GetValue(selectedItem) as AudioPackInfo;
                    if (selectedPack != null && !string.IsNullOrEmpty(selectedPack.PackDirectory))
                    {
                        // Show confirmation dialog
                        string packName = string.IsNullOrWhiteSpace(selectedPack.Version)
                            ? selectedPack.Name
                            : $"{selectedPack.Name} ({selectedPack.Version})";
                        
                        var confirmResult = MessageBox.Show(
                            $"Are you sure you want to delete the audio pack '{packName}'?\n\nThis action cannot be undone. All files in this pack will be permanently deleted.",
                            "Confirm Delete",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2);
                        
                        if (confirmResult == DialogResult.Yes)
                        {
                            bool isCurrentlyActive = _config != null && 
                                selectedPack.PackId == _config.Audio.AudioPack;
                            
                            // If currently active, switch to built-in pack first
                            if (isCurrentlyActive && _audioPlayer != null)
                            {
                                var availablePacks = _packManager.GetAllPacks();
                                var builtInPack = availablePacks.FirstOrDefault(p => p.IsBuiltIn);
                                if (builtInPack != null)
                                {
                                    _audioPlayer.StopSqueak();
                                    _audioPlayer.ReleaseFileLocks();
                                    System.Threading.Thread.Sleep(500);
                                    _audioPlayer.AudioPack = builtInPack.PackId;
                                    System.Threading.Thread.Sleep(200);
                                }
                            }
                            
                            // Delete the pack directory
                            try
                            {
                                if (Directory.Exists(selectedPack.PackDirectory))
                                {
                                    NubRub.Utilities.PathUtilities.SafeDeleteDirectory(selectedPack.PackDirectory);
                                }
                                
                                if (isCurrentlyActive && _config != null)
                                {
                                    var availablePacks = _packManager.GetAllPacks();
                                    var builtInPack = availablePacks.FirstOrDefault(p => p.IsBuiltIn);
                                    if (builtInPack != null)
                                    {
                                        _config.Audio.AudioPack = builtInPack.PackId;
                                        _configManager?.Save(_config);
                                        
                                        if (_audioPlayer != null)
                                        {
                                            _audioPlayer.AudioPack = builtInPack.PackId;
                                        }
                                    }
                                }
                                
                                // Refresh config panel if open
                                if (_configPanel != null && _configPanel.Visible)
                                {
                                    try
                                    {
                                        string currentPack = _config?.Audio.AudioPack ?? "squeak";
                                        _configPanel.PopulateAudioPacks(currentPack);
                                    }
                                    catch { }
                                }
                                
                                // Show success message
                                MessageBox.Show(
                                    $"Audio pack '{packName}' has been deleted successfully.",
                                    "Delete Successful",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    $"Failed to delete audio pack: {ex.Message}\n\nMake sure the pack is not in use and try again.",
                                    "Delete Failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ImportNubrubFile(string filePath)
    {
        if (_packManager == null)
            return;

        try
        {
            // Show import wizard/dialog
            var importWizard = new AudioPackImportWizard(_packManager, filePath);
            if (importWizard.ShowDialog(_mainForm) == DialogResult.OK && !string.IsNullOrEmpty(importWizard.ImportedPackId))
            {
                // Pack was imported successfully, select it
                if (_config != null && _packManager != null)
                {
                    var importedPack = _packManager.GetPack(importWizard.ImportedPackId);
                    string packName = importedPack?.Name ?? importWizard.ImportedPackId;
                    
                    _config.Audio.AudioPack = importWizard.ImportedPackId;
                    _configManager?.Save(_config);
                    
                    // Refresh config panel if open
                    if (_configPanel != null && _configPanel.Visible)
                    {
                        try
                        {
                            _configPanel.PopulateAudioPacks(importWizard.ImportedPackId);
                        }
                        catch { }
                    }
                    
                    // Reload audio player with new pack
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.AudioPack = importWizard.ImportedPackId;
                    }
                    
                    // Show success message
                    MessageBox.Show(
                        $"Audio pack '{packName}' has been imported successfully and is now selected as the current pack.",
                        "Import Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
            _rawInputHandler.ClearSelectedDevice();
            
            var detectionWindow = new DeviceDetectionWindow();
            DeviceInfo? detectedDevice = null;
            
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
                // Store the original audio pack for Cancel functionality
                string originalAudioPack = _config.Audio.AudioPack;

                _configPanel = new ConfigPanel(
                    devices,
                    _config.SelectedDevice,
                    _config.Audio.AudioPack,
                    _config.Audio.IdleCutoffMs,
                    _config.Audio.WiggleDurationMs,
                    _config.Audio.OnlyOnMovement,
                    _config.Audio.Volume,
                    _packManager);

                // Wire up audio pack changed event for test-drive functionality
                _configPanel.AudioPackChanged += (s, packId) =>
                {
                    // Immediately update the audio player to test the selected pack
                    if (_audioPlayer != null && !string.IsNullOrEmpty(packId))
                    {
                        _audioPlayer.AudioPack = packId;
                    }
                };

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

                var dialogResult = _configPanel.ShowDialog(_mainForm);
                
                if (dialogResult == DialogResult.OK)
                {
                    // Save settings
                    _config.SelectedDevice = _configPanel.SelectedDevice;
                    _config.Audio.AudioPack = _configPanel.AudioPack;
                    _config.Audio.IdleCutoffMs = _configPanel.IdleCutoffMs;
                    _config.Audio.WiggleDurationMs = _configPanel.WiggleDurationMs;
                    _config.Audio.OnlyOnMovement = _configPanel.OnlyOnMovement;
                    _config.Audio.Volume = _configPanel.Volume;

                    _configManager?.Save(_config);

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
                    if (_wiggleDetector != null)
                    {
                        _wiggleDetector.WiggleDurationMs = _configPanel.WiggleDurationMs;
                    }
                    _trayIcon!.Volume = _config.Audio.Volume;
                }
                else if (dialogResult == DialogResult.Cancel)
                {
                    // Cancel was clicked - restore the original audio pack
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.AudioPack = originalAudioPack;
                    }
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
        }
    }

    private static void OnUpdateCheckRequested(object? sender, EventArgs e)
    {
        // Ensure we're on the UI thread
        if (_mainForm != null && _mainForm.InvokeRequired)
        {
            _mainForm.Invoke(new Action<object?, EventArgs>(OnUpdateCheckRequested), sender, e);
            return;
        }

        _ = CheckForUpdatesAsync();
    }

    private static async Task CheckForUpdatesAsync()
    {
        UpdateChecker? updateChecker = null;
        UpdateProgressDialog? progressDialog = null;

        try
        {
            updateChecker = new UpdateChecker();
            progressDialog = new UpdateProgressDialog();
            
            // Show progress dialog
            progressDialog.Show();
            Application.DoEvents();

            var progress = new Progress<string>(status => progressDialog.SetStatus(status));
            var updateInfo = await updateChecker.CheckForUpdatesAsync(progress);

            if (progressDialog.Cancelled)
            {
                progressDialog.Close();
                return;
            }

            if (updateInfo == null)
            {
                var lastStatus = progressDialog.LastStatus;
                progressDialog.Close();
                
                var errorMessage = "Failed to check for updates.";
                if (!string.IsNullOrEmpty(lastStatus) && lastStatus.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = lastStatus;
                }
                else if (!string.IsNullOrEmpty(lastStatus) && lastStatus.Contains("Network error", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = lastStatus;
                }
                else if (!string.IsNullOrEmpty(lastStatus) && lastStatus.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = lastStatus;
                }
                else
                {
                    errorMessage = "Failed to check for updates. Please check your internet connection and try again.";
                }
                
                MessageBox.Show(
                    errorMessage,
                    "NubRub - Update Check Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!updateInfo.IsUpdateAvailable)
            {
                progressDialog.Close();
                MessageBox.Show(
                    "You're already on the latest version!",
                    "NubRub - No Updates Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            progressDialog.Close();
            var result = MessageBox.Show(
                $"A new version ({updateInfo.LatestVersion}) is available.\n\nCurrent version: {updateInfo.CurrentVersion}\n\nDownload and install now?",
                "NubRub - Update Available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            // Download the installer
            progressDialog = new UpdateProgressDialog();
            progressDialog.SetStatus("Downloading update...");
            progressDialog.Show();
            Application.DoEvents();

            var downloadProgress = new Progress<(long bytesDownloaded, long totalBytes)>(progress =>
            {
                progressDialog.SetProgress(progress.bytesDownloaded, progress.totalBytes);
            });

            string installerPath;
            try
            {
                installerPath = await updateChecker.DownloadInstallerAsync(updateInfo.DownloadUrl, downloadProgress);
            }
            catch (Exception ex)
            {
                progressDialog.Close();
                MessageBox.Show(
                    $"Failed to download update:\n\n{ex.Message}",
                    "NubRub - Download Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (progressDialog.Cancelled)
            {
                progressDialog.Close();
                // Clean up downloaded file
                try
                {
                    if (File.Exists(installerPath))
                    {
                        File.Delete(installerPath);
                    }
                }
                catch { }
                return;
            }

            progressDialog.Close();

            // Launch the installer
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{installerPath}\" /passive",
                    UseShellExecute = true,
                    Verb = "runas" // Request elevation for MSI installer
                };
                Process.Start(processStartInfo);
                
                // Exit the application so the installer can update it
                _debugWindow?.Log("Update installer launched. Application will exit.");
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch installer:\n\n{ex.Message}\n\nInstaller downloaded to:\n{installerPath}",
                    "NubRub - Installer Launch Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            progressDialog?.Close();
            _debugWindow?.Log($"Error during update check: {ex.Message}");
            MessageBox.Show(
                $"An error occurred while checking for updates:\n\n{ex.Message}",
                "NubRub - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            updateChecker?.Dispose();
            progressDialog?.Dispose();
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
