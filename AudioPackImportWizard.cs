using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NubRub.Models;
using Newtonsoft.Json;

namespace NubRub;

public partial class AudioPackImportWizard : Form
{
    private readonly AudioPackManager? _packManager;
    private readonly string _nubrubFilePath;
    private AudioPackInfo? _packInfo;
    private string? _tempExtractPath;
    
    private Label? _titleLabel;
    private TextBox? _reviewTextBox;
    private Button? _importButton;
    private Button? _cancelButton;
    private Label? _errorLabel;

    public string? ImportedPackId { get; private set; }

    public AudioPackImportWizard(AudioPackManager packManager, string nubrubFilePath)
    {
        _packManager = packManager;
        _nubrubFilePath = nubrubFilePath;
        
        InitializeComponent();
        LoadPackInfo();
    }

    private void InitializeComponent()
    {
        this.Text = "Import Audio Pack";
        this.Size = new Size(500, 390);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        _titleLabel = new Label
        {
            Text = "Review Audio Pack",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        this.Controls.Add(_titleLabel);

        _reviewTextBox = new TextBox
        {
            Location = new Point(20, 50),
            Size = new Size(440, 200),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9, FontStyle.Regular)
        };
        this.Controls.Add(_reviewTextBox);

        _errorLabel = new Label
        {
            Text = "",
            Location = new Point(20, 260),
            Size = new Size(440, 30),
            ForeColor = Color.Red,
            Visible = false
        };
        this.Controls.Add(_errorLabel);

        _importButton = new Button
        {
            Text = "Import",
            Location = new Point(320, 300),
            Size = new Size(65, 30),
            Enabled = false
        };
        _importButton.Click += (s, e) => ImportPack();
        this.Controls.Add(_importButton);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(395, 300),
            Size = new Size(65, 30),
            DialogResult = DialogResult.Cancel
        };
        this.Controls.Add(_cancelButton);
        this.CancelButton = _cancelButton;
    }

    private void LoadPackInfo()
    {
        try
        {
            if (!File.Exists(_nubrubFilePath))
            {
                ShowError("File not found.");
                return;
            }

            // Extract to temporary directory
            _tempExtractPath = Path.Combine(Path.GetTempPath(), $"NubRub_Import_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempExtractPath);

            // Extract ZIP archive
            ZipFile.ExtractToDirectory(_nubrubFilePath, _tempExtractPath);

            // Load pack.json
            string jsonPath = Path.Combine(_tempExtractPath, "pack.json");
            if (!File.Exists(jsonPath))
            {
                ShowError("Invalid pack file: pack.json not found.");
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath, Encoding.UTF8);
            _packInfo = JsonConvert.DeserializeObject<AudioPackInfo>(jsonContent);
            
            if (_packInfo == null || string.IsNullOrWhiteSpace(_packInfo.Name))
            {
                ShowError("Invalid pack file: pack.json is invalid or missing required fields.");
                return;
            }

            // Verify audio files exist
            var missingFiles = new List<string>();
            foreach (var fileName in _packInfo.RubSounds)
            {
                string filePath = Path.Combine(_tempExtractPath, fileName);
                if (!File.Exists(filePath))
                {
                    missingFiles.Add(fileName);
                }
            }
            foreach (var fileName in _packInfo.FinishSounds)
            {
                string filePath = Path.Combine(_tempExtractPath, fileName);
                if (!File.Exists(filePath))
                {
                    missingFiles.Add(fileName);
                }
            }

            if (missingFiles.Count > 0)
            {
                ShowError($"Missing audio files: {string.Join(", ", missingFiles)}");
                return;
            }

            // Show pack info
            UpdateReviewText();
            if (_importButton != null)
                _importButton.Enabled = true;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load pack: {ex.Message}");
        }
    }

    private void UpdateReviewText()
    {
        if (_reviewTextBox == null || _packInfo == null) return;

        var review = new StringBuilder();
        review.AppendLine("Audio Pack Information");
        review.AppendLine("======================");
        review.AppendLine();
        review.AppendLine($"Pack Name: {_packInfo.Name}");
        review.AppendLine($"Version: {_packInfo.Version}");
        review.AppendLine();
        review.AppendLine($"Rub Sounds ({_packInfo.RubSounds.Count}):");
        if (_packInfo.RubSounds.Count > 0)
        {
            foreach (var sound in _packInfo.RubSounds)
            {
                review.AppendLine($"  • {sound}");
            }
        }
        else
        {
            review.AppendLine("  (none)");
        }
        review.AppendLine();
        review.AppendLine($"Finish Sounds ({_packInfo.FinishSounds.Count}):");
        if (_packInfo.FinishSounds.Count > 0)
        {
            foreach (var sound in _packInfo.FinishSounds)
            {
                review.AppendLine($"  • {sound}");
            }
        }
        else
        {
            review.AppendLine("  (none)");
        }
        review.AppendLine();
        review.AppendLine("Click 'Import' to add this pack to your audio packs.");

        _reviewTextBox.Text = review.ToString();
    }

    private void ShowError(string message)
    {
        if (_errorLabel != null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }
        if (_importButton != null)
            _importButton.Enabled = false;
    }

    private void ImportPack()
    {
        if (_packManager == null || _packInfo == null || string.IsNullOrEmpty(_tempExtractPath))
            return;

        try
        {
            // Check if pack already exists
            var allPacks = _packManager.GetAllPacks();
            var existingPack = allPacks.FirstOrDefault(p => 
                p.Name.Equals(_packInfo.Name, StringComparison.OrdinalIgnoreCase) && !p.IsBuiltIn);

            if (existingPack != null)
            {
                var result = MessageBox.Show(
                    $"A pack named '{_packInfo.Name}' already exists.\n\nWould you like to overwrite it?",
                    "Pack Already Exists",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // Delete existing pack
                    if (!string.IsNullOrEmpty(existingPack.PackDirectory) && Directory.Exists(existingPack.PackDirectory))
                    {
                        Directory.Delete(existingPack.PackDirectory, true);
                    }
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
                else
                {
                    // User chose No - rename the pack
                    _packInfo.Name = $"{_packInfo.Name} (Imported)";
                }
            }

            // Sanitize pack name for folder name
            string folderName = SanitizeDirectoryName(_packInfo.Name);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                MessageBox.Show("Invalid pack name for folder creation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create pack directory
            string packDirectory = Path.Combine(_packManager.AudioPacksPath, folderName);
            if (Directory.Exists(packDirectory))
            {
                Directory.Delete(packDirectory, true);
            }
            Directory.CreateDirectory(packDirectory);

            // Copy pack.json
            string sourceJsonPath = Path.Combine(_tempExtractPath, "pack.json");
            string destJsonPath = Path.Combine(packDirectory, "pack.json");
            File.Copy(sourceJsonPath, destJsonPath, true);

            // Copy all audio files
            var allFiles = new List<string>();
            allFiles.AddRange(_packInfo.RubSounds);
            allFiles.AddRange(_packInfo.FinishSounds);

            foreach (var fileName in allFiles)
            {
                string sourceFilePath = Path.Combine(_tempExtractPath, fileName);
                string destFilePath = Path.Combine(packDirectory, fileName);
                if (File.Exists(sourceFilePath))
                {
                    File.Copy(sourceFilePath, destFilePath, true);
                }
            }

            // Store the imported pack ID
            ImportedPackId = folderName;

            // Clean up temp directory
            try
            {
                if (Directory.Exists(_tempExtractPath))
                {
                    Directory.Delete(_tempExtractPath, true);
                }
            }
            catch { }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string SanitizeDirectoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove invalid characters
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = name;
        foreach (char c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Trim and replace spaces with underscores
        sanitized = sanitized.Trim().Replace(' ', '_');

        // Remove consecutive underscores
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');

        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "AudioPack";
        }

        return sanitized;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Clean up temp directory
        try
        {
            if (!string.IsNullOrEmpty(_tempExtractPath) && Directory.Exists(_tempExtractPath))
            {
                Directory.Delete(_tempExtractPath, true);
            }
        }
        catch { }

        base.OnFormClosing(e);
    }
}

