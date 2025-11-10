using System.Diagnostics;
using System.Windows.Forms;
using NubRub.Models;
using NubRub.Services;

namespace NubRub.UI;

public delegate void AudioPackChangedEventHandler(object sender, string packId);

public partial class ConfigPanel : Form
{
    private ComboBox _deviceComboBox = null!;
    private Button _autoDetectButton = null!;
    private ComboBox _audioPackComboBox = null!;
    private Button _openAudioPacksFolderButton = null!;
    private NumericUpDown _idleCutoffNumeric = null!;
    private NumericUpDown _wiggleDurationNumeric = null!;
    private CheckBox _onlyOnMovementCheckBox = null!;
    private TrackBar _volumeTrackBar = null!;
    private Label _volumeLabel = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private AudioPackManager? _packManager;
    private string _originalAudioPack = "squeak";

    public event EventHandler? AutoDetectRequested;
    public event AudioPackChangedEventHandler? AudioPackChanged;

    public DeviceInfo? SelectedDevice { get; private set; }
    public string AudioPack { get; private set; } = "squeak";
    public int IdleCutoffMs { get; private set; }
    public int WiggleDurationMs { get; private set; }
    public bool OnlyOnMovement { get; private set; }
    public double Volume { get; private set; }

    public ConfigPanel(List<DeviceInfo> devices, DeviceInfo? currentDevice, string audioPack, int idleCutoffMs, int wiggleDurationMs, bool onlyOnMovement, double volume, AudioPackManager? packManager = null)
    {
        _packManager = packManager ?? new AudioPackManager();
        _originalAudioPack = audioPack; // Store original pack for Cancel functionality
        InitializeComponent();
        PopulateDevices(devices, currentDevice);
        PopulateAudioPacks(audioPack);
        _idleCutoffNumeric.Value = idleCutoffMs;
        _wiggleDurationNumeric.Value = wiggleDurationMs / 1000; // Convert milliseconds to seconds
        _onlyOnMovementCheckBox.Checked = onlyOnMovement;
        _volumeTrackBar.Value = (int)(volume * 100);
        UpdateVolumeLabel();
    }

    private void InitializeComponent()
    {
        this.Text = "NubRub Configuration";
        this.Size = new Size(500, 400);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        int y = 10;

        // Device selection
        var deviceLabel = new Label
        {
            Text = "Select HID Device:",
            Location = new Point(10, y),
            AutoSize = true
        };
        this.Controls.Add(deviceLabel);
        y += 25;

        _deviceComboBox = new ComboBox
        {
            Location = new Point(10, y),
            Width = 340, // Reduced width to make room for button
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "DisplayName"
        };
        this.Controls.Add(_deviceComboBox);

        _autoDetectButton = new Button
        {
            Text = "Auto-detect",
            Location = new Point(360, y),
            Width = 110,
            Height = 23, // Match combo box height
            UseVisualStyleBackColor = true
        };
        _autoDetectButton.Click += (s, e) => AutoDetectRequested?.Invoke(this, EventArgs.Empty);
        this.Controls.Add(_autoDetectButton);
        y += 35;

        // Audio Pack selection
        var audioPackLabel = new Label
        {
            Text = "Audio Pack:",
            Location = new Point(10, y),
            AutoSize = true
        };
        this.Controls.Add(audioPackLabel);
        y += 25;

        _audioPackComboBox = new ComboBox
        {
            Location = new Point(10, y),
            Width = 340, // Reduced width to make room for button
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _audioPackComboBox.SelectedIndexChanged += (s, e) => OnAudioPackSelectionChanged();
        this.Controls.Add(_audioPackComboBox);

        _openAudioPacksFolderButton = new Button
        {
            Text = "Open Folder",
            Location = new Point(360, y),
            Width = 110,
            Height = 23, // Match combo box height
            UseVisualStyleBackColor = true
        };
        _openAudioPacksFolderButton.Click += (s, e) => OpenAudioPacksFolder();
        this.Controls.Add(_openAudioPacksFolderButton);
        y += 35;

        // Idle Cutoff (left side)
        var idleLabel = new Label
        {
            Text = "Idle Cutoff (ms):",
            Location = new Point(10, y),
            AutoSize = true
        };
        this.Controls.Add(idleLabel);

        // Trigger Timer (right side)
        var wiggleDurationLabel = new Label
        {
            Text = "Trigger Timer (seconds):",
            Location = new Point(260, y),
            AutoSize = true
        };
        this.Controls.Add(wiggleDurationLabel);
        y += 25;

        _idleCutoffNumeric = new NumericUpDown
        {
            Location = new Point(10, y),
            Width = 100,
            Minimum = 50,
            Maximum = 2000,
            Value = 250
        };
        this.Controls.Add(_idleCutoffNumeric);

        _wiggleDurationNumeric = new NumericUpDown
        {
            Location = new Point(260, y),
            Width = 100,
            Minimum = 1,
            Maximum = 300,
            Value = 25,
            DecimalPlaces = 0
        };
        this.Controls.Add(_wiggleDurationNumeric);
        y += 35;

        // Only on Movement
        _onlyOnMovementCheckBox = new CheckBox
        {
            Text = "Only on Movement (ignore button events)",
            Location = new Point(10, y),
            AutoSize = true
        };
        this.Controls.Add(_onlyOnMovementCheckBox);
        y += 30;

        // Volume
        _volumeLabel = new Label
        {
            Text = "Volume: 60%",
            Location = new Point(10, y),
            AutoSize = true
        };
        this.Controls.Add(_volumeLabel);
        y += 25;

        _volumeTrackBar = new TrackBar
        {
            Location = new Point(10, y),
            Width = 460,
            Minimum = 0,
            Maximum = 100,
            Value = 60,
            TickFrequency = 10
        };
        _volumeTrackBar.ValueChanged += (s, e) => UpdateVolumeLabel();
        this.Controls.Add(_volumeTrackBar);
        y += 60;

        // Buttons
        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(300, y),
            Width = 80,
            Height = 30,
            DialogResult = DialogResult.OK
        };
        _saveButton.Click += (s, e) => SaveSettings();
        this.Controls.Add(_saveButton);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(390, y),
            Width = 80,
            Height = 30,
            DialogResult = DialogResult.Cancel
        };
        this.Controls.Add(_cancelButton);
    }

    public void PopulateDevices(List<DeviceInfo> devices, DeviceInfo? currentDevice)
    {
        _deviceComboBox.Items.Clear();
        foreach (var device in devices)
        {
            _deviceComboBox.Items.Add(device);
        }

        if (currentDevice != null)
        {
            for (int i = 0; i < _deviceComboBox.Items.Count; i++)
            {
                if (_deviceComboBox.Items[i] is DeviceInfo device && currentDevice.Matches(device))
                {
                    _deviceComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        else if (_deviceComboBox.Items.Count > 0)
        {
            _deviceComboBox.SelectedIndex = 0;
        }
    }

    public void PopulateAudioPacks(string currentPack)
    {
        _audioPackComboBox.Items.Clear();
        
        var allPacks = _packManager?.GetAllPacks() ?? new List<AudioPackInfo>();
        
        foreach (var pack in allPacks)
        {
            // Display format: "Pack Name (version)" or "Pack Name" if no version
            string displayName = string.IsNullOrWhiteSpace(pack.Version) 
                ? pack.Name 
                : $"{pack.Name} ({pack.Version})";
            
            // Store the pack ID as the value
            _audioPackComboBox.Items.Add(new { DisplayName = displayName, PackId = pack.PackId });
        }
        
        _audioPackComboBox.DisplayMember = "DisplayName";
        _audioPackComboBox.ValueMember = "PackId";

        // Select current pack
        for (int i = 0; i < _audioPackComboBox.Items.Count; i++)
        {
            var item = _audioPackComboBox.Items[i];
            if (item == null) continue;
            
            var packIdProperty = item.GetType().GetProperty("PackId");
            if (packIdProperty != null)
            {
                string? packId = packIdProperty.GetValue(item)?.ToString();
                if (packId != null && packId.Equals(currentPack, StringComparison.OrdinalIgnoreCase))
                {
                    _audioPackComboBox.SelectedIndex = i;
                    return;
                }
            }
        }
        
        // If current pack not found, select first item
        if (_audioPackComboBox.Items.Count > 0)
        {
            _audioPackComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateVolumeLabel()
    {
        _volumeLabel.Text = $"Volume: {_volumeTrackBar.Value}%";
    }

    private void OpenAudioPacksFolder()
    {
        try
        {
            string audioPacksPath = _packManager?.AudioPacksPath ?? string.Empty;
            
            if (string.IsNullOrEmpty(audioPacksPath))
            {
                MessageBox.Show(
                    "Unable to determine audio packs folder path.",
                    "NubRub - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Ensure the directory exists
            if (!Directory.Exists(audioPacksPath))
            {
                try
                {
                    Directory.CreateDirectory(audioPacksPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to create audio packs folder:\n\n{ex.Message}",
                        "NubRub - Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

                try
                {
                    string instructionFilePath = Path.Combine(audioPacksPath, "HOW_TO_CREATE_AUDIO_PACKS.txt");
                    if (!File.Exists(instructionFilePath))
                    {
                        var tempManager = new AudioPackManager();
                    }
                }
            catch
            {
            }

            // Open the folder in Windows Explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = audioPacksPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open audio packs folder:\n\n{ex.Message}",
                "NubRub - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SaveSettings()
    {
        SelectedDevice = _deviceComboBox.SelectedItem as DeviceInfo;
        
        // Extract pack ID from selected item
        AudioPack = GetSelectedAudioPackId();
        
        IdleCutoffMs = (int)_idleCutoffNumeric.Value;
        WiggleDurationMs = (int)(_wiggleDurationNumeric.Value * 1000); // Convert seconds to milliseconds
        OnlyOnMovement = _onlyOnMovementCheckBox.Checked;
        Volume = _volumeTrackBar.Value / 100.0;
    }

    /// <summary>
    /// Gets the currently selected audio pack ID from the combo box.
    /// </summary>
    private string GetSelectedAudioPackId()
    {
        var selectedItem = _audioPackComboBox.SelectedItem;
        if (selectedItem != null)
        {
            var packIdProperty = selectedItem.GetType().GetProperty("PackId");
            if (packIdProperty != null)
            {
                return packIdProperty.GetValue(selectedItem)?.ToString() ?? "squeak";
            }
            else
            {
                return selectedItem.ToString() ?? "squeak";
            }
        }
        return "squeak";
    }

    /// <summary>
    /// Handles the audio pack combo box selection change event.
    /// Fires the AudioPackChanged event to allow immediate testing.
    /// </summary>
    private void OnAudioPackSelectionChanged()
    {
        string selectedPackId = GetSelectedAudioPackId();
        AudioPackChanged?.Invoke(this, selectedPackId);
    }

    /// <summary>
    /// Restores the original audio pack selection (for Cancel functionality).
    /// </summary>
    public void RestoreOriginalAudioPack()
    {
        // Find and select the original pack in the combo box
        for (int i = 0; i < _audioPackComboBox.Items.Count; i++)
        {
            var item = _audioPackComboBox.Items[i];
            if (item == null) continue;
            
            var packIdProperty = item.GetType().GetProperty("PackId");
            if (packIdProperty != null)
            {
                string? packId = packIdProperty.GetValue(item)?.ToString();
                if (packId != null && packId.Equals(_originalAudioPack, StringComparison.OrdinalIgnoreCase))
                {
                    _audioPackComboBox.SelectedIndex = i;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Gets the original audio pack ID that was set when the dialog was opened.
    /// </summary>
    public string OriginalAudioPack => _originalAudioPack;
}

