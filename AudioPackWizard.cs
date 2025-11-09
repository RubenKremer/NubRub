using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NubRub.Models;
using NubRub.Utilities;

namespace NubRub;

public partial class AudioPackWizard : Form
{
    private const int TOTAL_STEPS = 3;
    private int _currentStep = 1;
    
    private AudioPackManager? _packManager;
    private AudioPackInfo _currentPack;
    private AudioPackInfo? _originalPack;
    private bool _isEditMode = false;
    public string? CreatedPackId { get; private set; }
    
    // Step 1 controls
    private TextBox? _packNameTextBox;
    private Label? _versionLabel;
    private Label? _errorLabel;
    
    // Step 2 controls
    private ListBox? _rubSoundsListBox;
    private ListBox? _finishSoundsListBox;
    private Button? _addRubSoundButton;
    private Button? _removeRubSoundButton;
    private Button? _markAsFinishButton;
    private Button? _removeFinishButton;
    private Button? _playButton;
    private WaveOutEvent? _previewPlayer;
    private AudioFileReader? _previewReader;
    
    // Step 3 controls
    private Label? _reviewTitleLabel;
    private TextBox? _reviewTextBox;
    
    // Navigation
    private Button? _nextButton;
    private Button? _previousButton;
    private Button? _cancelButton;
    private Button? _updateButton;
    private Button? _saveAsNewButton;
    private Label? _stepLabel;
    private Panel? _stepPanel;

    private AudioPlayer? _audioPlayer;
    
    public AudioPackWizard(AudioPackManager? packManager = null, AudioPackInfo? existingPack = null, AudioPlayer? audioPlayer = null)
    {
        _packManager = packManager;
        _audioPlayer = audioPlayer;
        _originalPack = existingPack;
        _isEditMode = existingPack != null;
        
        if (_isEditMode && existingPack != null)
        {
            _currentPack = new AudioPackInfo
            {
                Name = existingPack.Name,
                Version = existingPack.Version,
                RubSounds = new List<string>(existingPack.RubSounds),
                FinishSounds = new List<string>(existingPack.FinishSounds),
                PackDirectory = existingPack.PackDirectory,
                PackId = existingPack.PackId,
                IsBuiltIn = existingPack.IsBuiltIn
            };
            
            // Load existing files
            LoadExistingFiles();
        }
        else
        {
            _currentPack = new AudioPackInfo
            {
                Name = string.Empty,
                Version = "1.0",
                RubSounds = new List<string>(),
                FinishSounds = new List<string>()
            };
        }
        
        InitializeComponent();
        ShowStep(1);
    }
    
    private void LoadExistingFiles()
    {
        if (_originalPack == null || string.IsNullOrEmpty(_originalPack.PackDirectory))
            return;
            
        if (!Directory.Exists(_originalPack.PackDirectory))
            return;
            
        try
        {
            // Convert relative file names to full paths
            var rubSoundPaths = new List<string>();
            foreach (var fileName in _currentPack.RubSounds)
            {
                string fullPath = Path.Combine(_originalPack.PackDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    rubSoundPaths.Add(fullPath);
                }
            }
            _currentPack.RubSounds = rubSoundPaths;
            
            var finishSoundPaths = new List<string>();
            foreach (var fileName in _currentPack.FinishSounds)
            {
                string fullPath = Path.Combine(_originalPack.PackDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    finishSoundPaths.Add(fullPath);
                }
            }
            _currentPack.FinishSounds = finishSoundPaths;
        }
        catch
        {
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopPreview();
        base.OnFormClosing(e);
    }

    private void InitializeComponent()
    {
        this.Text = _isEditMode ? "NubRub - Edit Audio Pack" : "NubRub - Create Audio Pack";
        this.Size = new Size(500, 450);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        // Step indicator
        _stepLabel = new Label
        {
            Text = "Step 1 of 3",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        this.Controls.Add(_stepLabel);

        // Step panel (will contain step-specific controls)
        _stepPanel = new Panel
        {
            Location = new Point(20, 50),
            Size = new Size(440, 300),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(_stepPanel);

        // Navigation buttons
        _previousButton = new Button
        {
            Text = "Previous",
            Location = new Point(200, 360),
            Size = new Size(75, 30),
            Enabled = false
        };
        _previousButton.Click += (s, e) => NavigatePrevious();
        this.Controls.Add(_previousButton);

        _nextButton = new Button
        {
            Text = "Next",
            Location = new Point(285, 360),
            Size = new Size(75, 30)
        };
        _nextButton.Click += (s, e) => NavigateNext();
        this.Controls.Add(_nextButton);

        // Edit mode buttons (for Step 3)
        _updateButton = new Button
        {
            Text = "Update",
            Location = new Point(200, 360),
            Size = new Size(100, 30),
            Visible = false
        };
        _updateButton.Click += (s, e) => UpdatePackAndClose();
        this.Controls.Add(_updateButton);

        _saveAsNewButton = new Button
        {
            Text = "Save as new pack",
            Location = new Point(310, 360),
            Size = new Size(115, 30),
            Visible = false
        };
        _saveAsNewButton.Click += (s, e) => SaveAsNewPackAndClose();
        this.Controls.Add(_saveAsNewButton);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(370, 360),
            Size = new Size(75, 30),
            DialogResult = DialogResult.Cancel
        };
        this.Controls.Add(_cancelButton);
        this.CancelButton = _cancelButton;

        InitializeStep1();
        InitializeStep2();
        InitializeStep3();
    }

    private void InitializeStep1()
    {
        if (_stepPanel == null) return;

        var titleLabel = new Label
        {
            Text = "Pack Information",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _stepPanel.Controls.Add(titleLabel);

        var packNameLabel = new Label
        {
            Text = "Pack Name:",
            Location = new Point(10, 40),
            AutoSize = true
        };
        _stepPanel.Controls.Add(packNameLabel);

        _packNameTextBox = new TextBox
        {
            Location = new Point(10, 60),
            Size = new Size(410, 23),
            MaxLength = 100
        };
        // Pre-fill pack name in edit mode
        if (_isEditMode && _currentPack != null)
        {
            _packNameTextBox.Text = _currentPack.Name;
        }
        _packNameTextBox.TextChanged += (s, e) => ValidateStep1();
        _stepPanel.Controls.Add(_packNameTextBox);

        var versionInfoLabel = new Label
        {
            Text = "Version:",
            Location = new Point(10, 100),
            AutoSize = true
        };
        _stepPanel.Controls.Add(versionInfoLabel);

        _versionLabel = new Label
        {
            Text = _isEditMode && _currentPack != null 
                ? $"{_currentPack.Version} (will be incremented on update)"
                : "1.0 (auto-managed)",
            Location = new Point(10, 120),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        _stepPanel.Controls.Add(_versionLabel);

        _errorLabel = new Label
        {
            Text = "",
            Location = new Point(10, 160),
            Size = new Size(410, 40),
            ForeColor = Color.Red,
            Visible = false
        };
        _stepPanel.Controls.Add(_errorLabel);
    }

    private void InitializeStep2()
    {
        if (_stepPanel == null) return;

        var titleLabel = new Label
        {
            Text = "Audio Files",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        titleLabel.Name = "Step2Title";
        titleLabel.Visible = false;
        _stepPanel.Controls.Add(titleLabel);

        // Rub Sounds section
        var rubLabel = new Label
        {
            Text = "Rub Sounds (play during movement):",
            Location = new Point(10, 35),
            Size = new Size(200, 20),
            AutoSize = false,
            Font = new Font("Segoe UI", 8, FontStyle.Regular)
        };
        rubLabel.Name = "Step2RubLabel";
        rubLabel.Visible = false;
        _stepPanel.Controls.Add(rubLabel);

        _rubSoundsListBox = new ListBox
        {
            Location = new Point(10, 55),
            Size = new Size(200, 100),
            SelectionMode = SelectionMode.MultiExtended
        };
        _rubSoundsListBox.Name = "Step2RubListBox";
        _rubSoundsListBox.Visible = false;
        _stepPanel.Controls.Add(_rubSoundsListBox);

        _addRubSoundButton = new Button
        {
            Text = "Add...",
            Location = new Point(10, 160),
            Size = new Size(60, 25)
        };
        _addRubSoundButton.Name = "Step2AddButton";
        _addRubSoundButton.Visible = false;
        _addRubSoundButton.Click += (s, e) => AddRubSound();
        _stepPanel.Controls.Add(_addRubSoundButton);

        _markAsFinishButton = new Button
        {
            Text = ">>>",
            Location = new Point(140, 160),
            Size = new Size(70, 25),
            Enabled = false
        };
        _markAsFinishButton.Name = "Step2MarkFinishButton";
        _markAsFinishButton.Visible = false;
        _markAsFinishButton.Click += (s, e) => MarkAsFinishSound();
        _stepPanel.Controls.Add(_markAsFinishButton);

        _removeRubSoundButton = new Button
        {
            Text = "Remove",
            Location = new Point(10, 190),
            Size = new Size(200, 25),
            Enabled = false
        };
        _removeRubSoundButton.Name = "Step2RemoveButton";
        _removeRubSoundButton.Visible = false;
        _removeRubSoundButton.Click += (s, e) => RemoveRubSound();
        _stepPanel.Controls.Add(_removeRubSoundButton);

        // Finish Sounds section
        var finishLabel = new Label
        {
            Text = "Finish Sounds (play after 25s rubbing):",
            Location = new Point(220, 35),
            Size = new Size(250, 20),
            AutoSize = false,
            Font = new Font("Segoe UI", 8, FontStyle.Regular)
        };
        finishLabel.Name = "Step2FinishLabel";
        finishLabel.Visible = false;
        _stepPanel.Controls.Add(finishLabel);

        _finishSoundsListBox = new ListBox
        {
            Location = new Point(220, 55),
            Size = new Size(200, 100),
            SelectionMode = SelectionMode.MultiExtended
        };
        _finishSoundsListBox.Name = "Step2FinishListBox";
        _finishSoundsListBox.Visible = false;
        _stepPanel.Controls.Add(_finishSoundsListBox);

        // Single Play button for both lists
        _playButton = new Button
        {
            Text = "Play",
            Location = new Point(220, 160),
            Size = new Size(60, 25),
            Enabled = false
        };
        _playButton.Name = "Step2PlayButton";
        _playButton.Visible = false;
        _playButton.Click += (s, e) => PlaySelectedSound();
        _stepPanel.Controls.Add(_playButton);

        _removeFinishButton = new Button
        {
            Text = "Remove",
            Location = new Point(220, 190),
            Size = new Size(200, 25),
            Enabled = false
        };
        _removeFinishButton.Name = "Step2RemoveFinishButton";
        _removeFinishButton.Visible = false;
        _removeFinishButton.Click += (s, e) => RemoveFinishSound();
        _stepPanel.Controls.Add(_removeFinishButton);

        // Wire up selection changed events
        if (_rubSoundsListBox != null)
        {
            _rubSoundsListBox.SelectedIndexChanged += (s, e) =>
            {
                UpdateStep2ButtonStates();
            };
        }

        if (_finishSoundsListBox != null)
        {
            _finishSoundsListBox.SelectedIndexChanged += (s, e) =>
            {
                UpdateStep2ButtonStates();
            };
        }
    }

    private void InitializeStep3()
    {
        if (_stepPanel == null) return;

        _reviewTitleLabel = new Label
        {
            Text = "Review",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _reviewTitleLabel.Name = "Step3Title";
        _reviewTitleLabel.Visible = false;
        _stepPanel.Controls.Add(_reviewTitleLabel);

        _reviewTextBox = new TextBox
        {
            Location = new Point(10, 35),
            Size = new Size(410, 250),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9, FontStyle.Regular)
        };
        _reviewTextBox.Name = "Step3Review";
        _reviewTextBox.Visible = false;
        _stepPanel.Controls.Add(_reviewTextBox);
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        if (_stepLabel != null)
            _stepLabel.Text = $"Step {_currentStep} of {TOTAL_STEPS}";
        
        if (_currentStep == TOTAL_STEPS && _isEditMode)
        {
            // Hide Previous, Next buttons, show Update and Save as new buttons
            if (_previousButton != null) _previousButton.Visible = false;
            if (_nextButton != null) _nextButton.Visible = false;
            if (_updateButton != null)
            {
                _updateButton.Visible = true;
                _updateButton.Location = new Point(150, 360);
            }
            if (_saveAsNewButton != null)
            {
                _saveAsNewButton.Visible = true;
                _saveAsNewButton.Location = new Point(260, 360);
            }
            if (_cancelButton != null)
            {
                _cancelButton.Location = new Point(385, 360);
                _cancelButton.Size = new Size(75, 30);
            }
        }
        else
        {
            // Show Previous and Next buttons, hide Update and Save as new buttons
            if (_previousButton != null) _previousButton.Visible = _currentStep > 1;
            if (_nextButton != null)
            {
                _nextButton.Visible = true;
                _nextButton.Text = _currentStep == TOTAL_STEPS ? "Finish" : "Next";
            }
            if (_updateButton != null) _updateButton.Visible = false;
            if (_saveAsNewButton != null) _saveAsNewButton.Visible = false;
            // Restore Cancel button to original position
            if (_cancelButton != null)
            {
                _cancelButton.Location = new Point(370, 360);
                _cancelButton.Size = new Size(75, 30);
            }
        }

        // Hide all step controls
        if (_stepPanel != null)
        {
            foreach (Control control in _stepPanel.Controls)
            {
                if (control.Name == null || !control.Name.StartsWith("Step"))
                {
                    // Step 1 controls don't have names, hide them based on step
                    if (_currentStep != 1)
                    {
                        control.Visible = false;
                    }
                }
                else
                {
                    // Step 2+ controls have names
                    if (control.Name.StartsWith($"Step{_currentStep}"))
                    {
                        control.Visible = true;
                    }
                    else
                    {
                        control.Visible = false;
                    }
                }
            }
        }

        // Show Step 1 controls
        if (_currentStep == 1)
        {
            if (_packNameTextBox != null)
            {
                _packNameTextBox.Visible = true;
                // Pre-fill pack name in edit mode
                if (_isEditMode && _currentPack != null && string.IsNullOrEmpty(_packNameTextBox.Text))
                {
                    _packNameTextBox.Text = _currentPack.Name;
                }
            }
            if (_versionLabel != null)
            {
                _versionLabel.Visible = true;
                if (_isEditMode && _currentPack != null)
                {
                    _versionLabel.Text = $"{_currentPack.Version} (will be incremented on update)";
                }
            }
            if (_errorLabel != null) _errorLabel.Visible = false;
            ValidateStep1();
        }

        // Show Step 2 controls
        if (_currentStep == 2)
        {
            // Show all Step 2 controls
            if (_stepPanel != null)
            {
                foreach (Control control in _stepPanel.Controls)
                {
                    if (control.Name != null && control.Name.StartsWith("Step2"))
                    {
                        control.Visible = true;
                    }
                }
            }
            
            // Refresh rub sounds list
            if (_rubSoundsListBox != null && _currentPack != null)
            {
                _rubSoundsListBox.Visible = true;
                _rubSoundsListBox.Items.Clear();
                foreach (var sound in _currentPack.RubSounds)
                {
                    _rubSoundsListBox.Items.Add(Path.GetFileName(sound));
                }
            }
            
            // Refresh finish sounds list
            if (_finishSoundsListBox != null && _currentPack != null)
            {
                _finishSoundsListBox.Visible = true;
                _finishSoundsListBox.Items.Clear();
                foreach (var sound in _currentPack.FinishSounds)
                {
                    _finishSoundsListBox.Items.Add(Path.GetFileName(sound));
                }
            }
        }

        // Show Step 3 controls
        if (_currentStep == 3)
        {
            if (_reviewTitleLabel != null) _reviewTitleLabel.Visible = true;
            if (_reviewTextBox != null)
            {
                _reviewTextBox.Visible = true;
                UpdateReviewText();
            }
        }

        UpdateNavigationButtons();
    }

    private void NavigateNext()
    {
        if (_currentStep == 1)
        {
            if (!ValidateStep1())
                return;
            _currentPack.Name = _packNameTextBox?.Text.Trim() ?? string.Empty;
        }
        else if (_currentStep == 2)
        {
            // Validate Step 2 - need at least one rub sound
            if (_currentPack.RubSounds.Count == 0)
            {
                MessageBox.Show("Please add at least one rub sound.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else if (_currentStep == 3)
        {
            // Step 3 is review - create the pack (for create mode)
            // For edit mode, buttons handle the actions
            if (!_isEditMode)
            {
                if (!CreatePack())
                {
                    return;
                }
                
                // Pack created successfully, close dialog
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }
            // In edit mode, don't proceed - buttons handle the actions
            return;
        }

        if (_currentStep < TOTAL_STEPS)
        {
            ShowStep(_currentStep + 1);
        }
    }

    private void UpdateReviewText()
    {
        if (_reviewTextBox == null) return;

        var review = new System.Text.StringBuilder();
        review.AppendLine("Audio Pack Summary");
        review.AppendLine("==================");
        review.AppendLine();
        review.AppendLine($"Pack Name: {_currentPack.Name}");
        review.AppendLine($"Version: {_currentPack.Version}");
        review.AppendLine();
        review.AppendLine($"Rub Sounds ({_currentPack.RubSounds.Count}):");
        if (_currentPack.RubSounds.Count > 0)
        {
            foreach (var sound in _currentPack.RubSounds)
            {
                review.AppendLine($"  • {Path.GetFileName(sound)}");
            }
        }
        else
        {
            review.AppendLine("  (none)");
        }
        review.AppendLine();
        review.AppendLine($"Finish Sounds ({_currentPack.FinishSounds.Count}):");
        if (_currentPack.FinishSounds.Count > 0)
        {
            foreach (var sound in _currentPack.FinishSounds)
            {
                review.AppendLine($"  • {Path.GetFileName(sound)}");
            }
        }
        else
        {
            review.AppendLine("  (none)");
        }
        review.AppendLine();
        if (_isEditMode)
        {
            review.AppendLine("Choose to update this pack or save as a new one.");
        }
        else
        {
            review.AppendLine("Click 'Finish' to create this audio pack.");
        }

        _reviewTextBox.Text = review.ToString();
    }

    private void NavigatePrevious()
    {
        if (_currentStep > 1)
        {
            ShowStep(_currentStep - 1);
        }
    }

    private bool ValidateStep1()
    {
        if (_packNameTextBox == null || _errorLabel == null)
            return false;

        string packName = _packNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(packName))
        {
            _errorLabel.Text = "Pack name is required.";
            _errorLabel.Visible = true;
            if (_nextButton != null) _nextButton.Enabled = false;
            return false;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (packName.IndexOfAny(invalidChars) >= 0)
        {
            _errorLabel.Text = "Pack name contains invalid characters.";
            _errorLabel.Visible = true;
            if (_nextButton != null) _nextButton.Enabled = false;
            return false;
        }

        if (_packManager != null)
        {
            var allPacks = _packManager.GetAllPacks();
            var conflictingPack = allPacks.FirstOrDefault(p => 
                p.Name.Equals(packName, StringComparison.OrdinalIgnoreCase) && 
                !p.IsBuiltIn);
            
            if (conflictingPack != null)
            {
                // Allow same name if editing the same pack
                if (_isEditMode && _originalPack != null && 
                    conflictingPack.PackId == _originalPack.PackId)
                {
                    // Same pack, same name - this is allowed
                }
                else
                {
                    // Different pack with same name - not allowed
                    _errorLabel.Text = "A pack with this name already exists.";
                    _errorLabel.Visible = true;
                    if (_nextButton != null) _nextButton.Enabled = false;
                    return false;
                }
            }
        }

        _errorLabel.Visible = false;
        if (_nextButton != null) _nextButton.Enabled = true;
        return true;
    }

    private void AddRubSound()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = true,
            Title = "Select Rub Sound Files"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            foreach (string fileName in dialog.FileNames)
            {
                // Validate file
                if (!Path.GetExtension(fileName).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Only WAV files are supported.\nSkipping: {Path.GetFileName(fileName)}", 
                        "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Length > 50 * 1024 * 1024)
                {
                    MessageBox.Show($"File size exceeds 50 MB limit.\nSkipping: {Path.GetFileName(fileName)}", 
                        "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                // Add to list if not already present
                if (!_currentPack.RubSounds.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    _currentPack.RubSounds.Add(fileName);
                }
            }

            // Refresh list box
            if (_rubSoundsListBox != null)
            {
                _rubSoundsListBox.Items.Clear();
                foreach (var sound in _currentPack.RubSounds)
                {
                    _rubSoundsListBox.Items.Add(Path.GetFileName(sound));
                }
            }
        }
    }

    private void RemoveRubSound()
    {
        if (_rubSoundsListBox == null) return;

        var selectedIndices = new List<int>();
        foreach (int index in _rubSoundsListBox.SelectedIndices)
        {
            selectedIndices.Add(index);
        }

        // Remove in reverse order to maintain indices
        selectedIndices.Sort();
        selectedIndices.Reverse();

        foreach (int index in selectedIndices)
        {
            if (index >= 0 && index < _currentPack.RubSounds.Count)
            {
                _currentPack.RubSounds.RemoveAt(index);
            }
        }

        // Refresh list box
        _rubSoundsListBox.Items.Clear();
        foreach (var sound in _currentPack.RubSounds)
        {
            _rubSoundsListBox.Items.Add(Path.GetFileName(sound));
        }

        UpdateStep2ButtonStates();
    }

    private void UpdateStep2ButtonStates()
    {
        if (_rubSoundsListBox != null)
        {
            bool hasSelection = _rubSoundsListBox.SelectedIndices.Count > 0;
            bool hasSingleSelection = _rubSoundsListBox.SelectedIndices.Count == 1;
            
            if (_removeRubSoundButton != null)
                _removeRubSoundButton.Enabled = hasSelection;
            if (_markAsFinishButton != null)
                _markAsFinishButton.Enabled = hasSelection;
        }

        if (_finishSoundsListBox != null)
        {
            bool hasSelection = _finishSoundsListBox.SelectedIndices.Count > 0;
            
            if (_removeFinishButton != null)
                _removeFinishButton.Enabled = hasSelection;
        }

        // Update play button - enabled if exactly one item is selected in either list
        if (_playButton != null)
        {
            bool rubHasSingle = _rubSoundsListBox != null && _rubSoundsListBox.SelectedIndices.Count == 1;
            bool finishHasSingle = _finishSoundsListBox != null && _finishSoundsListBox.SelectedIndices.Count == 1;
            _playButton.Enabled = rubHasSingle || finishHasSingle;
        }
    }

    private void MarkAsFinishSound()
    {
        if (_rubSoundsListBox == null || _rubSoundsListBox.SelectedIndices.Count == 0) return;

        var selectedIndices = new List<int>();
        foreach (int index in _rubSoundsListBox.SelectedIndices)
        {
            selectedIndices.Add(index);
        }

        // Remove in reverse order to maintain indices
        selectedIndices.Sort();
        selectedIndices.Reverse();

        foreach (int index in selectedIndices)
        {
            if (index >= 0 && index < _currentPack.RubSounds.Count)
            {
                string soundPath = _currentPack.RubSounds[index];
                // Move from rub sounds to finish sounds
                _currentPack.RubSounds.RemoveAt(index);
                if (!_currentPack.FinishSounds.Contains(soundPath, StringComparer.OrdinalIgnoreCase))
                {
                    _currentPack.FinishSounds.Add(soundPath);
                }
            }
        }

        // Refresh both list boxes
        if (_rubSoundsListBox != null)
        {
            _rubSoundsListBox.Items.Clear();
            foreach (var sound in _currentPack.RubSounds)
            {
                _rubSoundsListBox.Items.Add(Path.GetFileName(sound));
            }
        }

        if (_finishSoundsListBox != null)
        {
            _finishSoundsListBox.Items.Clear();
            foreach (var sound in _currentPack.FinishSounds)
            {
                _finishSoundsListBox.Items.Add(Path.GetFileName(sound));
            }
        }
    }

    private void RemoveFinishSound()
    {
        if (_finishSoundsListBox == null) return;

        var selectedIndices = new List<int>();
        foreach (int index in _finishSoundsListBox.SelectedIndices)
        {
            selectedIndices.Add(index);
        }

        // Remove in reverse order to maintain indices
        selectedIndices.Sort();
        selectedIndices.Reverse();

        foreach (int index in selectedIndices)
        {
            if (index >= 0 && index < _currentPack.FinishSounds.Count)
            {
                _currentPack.FinishSounds.RemoveAt(index);
            }
        }

        // Refresh list box
        _finishSoundsListBox.Items.Clear();
        foreach (var sound in _currentPack.FinishSounds)
        {
            _finishSoundsListBox.Items.Add(Path.GetFileName(sound));
        }

        UpdateStep2ButtonStates();
    }

    private void PlaySelectedSound()
    {
        if (_rubSoundsListBox != null && _rubSoundsListBox.SelectedIndices.Count == 1)
        {
            int selectedIndex = _rubSoundsListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _currentPack.RubSounds.Count)
            {
                string filePath = _currentPack.RubSounds[selectedIndex];
                PlayAudioFile(filePath);
                return;
            }
        }

        if (_finishSoundsListBox != null && _finishSoundsListBox.SelectedIndices.Count == 1)
        {
            int selectedIndex = _finishSoundsListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _currentPack.FinishSounds.Count)
            {
                string filePath = _currentPack.FinishSounds[selectedIndex];
                PlayAudioFile(filePath);
                return;
            }
        }
    }

    private void PlayAudioFile(string filePath)
    {
        try
        {
            // Stop any currently playing preview
            StopPreview();

            if (!File.Exists(filePath))
            {
                MessageBox.Show("Audio file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _previewReader = new AudioFileReader(filePath);
            _previewPlayer = new WaveOutEvent();
            _previewPlayer.Init(_previewReader);
            _previewPlayer.PlaybackStopped += (s, e) =>
            {
                StopPreview();
            };
            _previewPlayer.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to play audio file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            StopPreview();
        }
    }

    private void StopPreview()
    {
        try
        {
            _previewPlayer?.Stop();
            _previewPlayer?.Dispose();
            _previewPlayer = null;
            _previewReader?.Dispose();
            _previewReader = null;
        }
        catch
        {
        }
    }

    private void UpdateNavigationButtons()
    {
        if (_previousButton != null)
            _previousButton.Enabled = _currentStep > 1;

        if (_nextButton != null)
        {
            if (_currentStep == TOTAL_STEPS)
            {
                _nextButton.Text = "Finish";
            }
            else
            {
                _nextButton.Text = "Next";
            }
        }
    }

    private bool CreatePack()
    {
        if (_packManager == null)
        {
            MessageBox.Show("Audio pack manager is not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            // Sanitize pack name for folder name
            string folderName = PathUtilities.SanitizeDirectoryName(_currentPack.Name);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                MessageBox.Show("Invalid pack name for folder creation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bool isSavingAsNewFromEdit = _isEditMode && _originalPack != null;
            bool isSameDirectory = isSavingAsNewFromEdit && 
                _originalPack != null &&
                !string.IsNullOrEmpty(_originalPack.PackDirectory) &&
                Path.GetFileName(_originalPack.PackDirectory) == folderName;
            
            // If saving as new pack and a pack with the same name exists, auto-rename
            if (isSavingAsNewFromEdit)
            {
                var allPacks = _packManager.GetAllPacks();
                var existingPack = allPacks.FirstOrDefault(p => 
                    p.Name.Equals(_currentPack.Name, StringComparison.OrdinalIgnoreCase) && 
                    !p.IsBuiltIn);
                
                if (existingPack != null)
                {
                    // Find the next available "Copy" name
                    string baseName = _currentPack.Name;
                    string newName = $"{baseName} Copy";
                    int copyNumber = 1;
                    
                    while (allPacks.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) && !p.IsBuiltIn))
                    {
                        copyNumber++;
                        newName = $"{baseName} Copy ({copyNumber})";
                    }
                    
                    _currentPack.Name = newName;
                    folderName = PathUtilities.SanitizeDirectoryName(newName);
                }
            }
            
            string packDirectory = Path.Combine(_packManager.AudioPacksPath, folderName);
            
            // If same directory (saving as new with same folder name), copy files to temp location first
            string? tempCopyDirectory = null;
            if (isSameDirectory && Directory.Exists(packDirectory))
            {
                tempCopyDirectory = Path.Combine(Path.GetTempPath(), $"NubRub_Temp_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempCopyDirectory);
                
                foreach (string sourcePath in _currentPack.RubSounds)
                {
                    if (File.Exists(sourcePath))
                    {
                        string tempFileName = Path.GetFileName(sourcePath);
                        string tempPath = Path.Combine(tempCopyDirectory, tempFileName);
                        File.Copy(sourcePath, tempPath, true);
                    }
                }
                foreach (string sourcePath in _currentPack.FinishSounds)
                {
                    if (File.Exists(sourcePath))
                    {
                        string tempFileName = Path.GetFileName(sourcePath);
                        string tempPath = Path.Combine(tempCopyDirectory, tempFileName);
                        File.Copy(sourcePath, tempPath, true);
                    }
                }
            }
            
            if (Directory.Exists(packDirectory))
            {
                // Only show overwrite dialog if not saving as new from edit (shouldn't happen)
                if (!isSavingAsNewFromEdit)
                {
                    var result = MessageBox.Show(
                        $"A pack folder named '{folderName}' already exists.\n\nWould you like to overwrite it?",
                        "Pack Already Exists",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        PathUtilities.SafeDeleteDirectory(packDirectory);
                    }
                    else
                    {
                        // Clean up temp directory if created
                        if (tempCopyDirectory != null && Directory.Exists(tempCopyDirectory))
                        {
                            try { PathUtilities.SafeDeleteDirectory(tempCopyDirectory); } catch { }
                        }
                        return false;
                    }
                }
                else
                {
                    // This shouldn't happen after auto-rename, but if it does, delete it
                    PathUtilities.SafeDeleteDirectory(packDirectory);
                }
            }

            Directory.CreateDirectory(packDirectory);

            var rubSoundFileNames = new List<string>();
            foreach (string sourcePath in _currentPack.RubSounds)
            {
                string actualSourcePath = sourcePath;
                
                // If we copied to temp, use temp path
                if (tempCopyDirectory != null && Directory.Exists(tempCopyDirectory))
                {
                    string tempFileName = Path.GetFileName(sourcePath);
                    string tempPath = Path.Combine(tempCopyDirectory, tempFileName);
                    if (File.Exists(tempPath))
                    {
                        actualSourcePath = tempPath;
                    }
                }
                
                if (!File.Exists(actualSourcePath))
                {
                    MessageBox.Show($"Source file not found: {Path.GetFileName(sourcePath)}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                string finalFileName = Path.GetFileName(actualSourcePath);
                string destPath = Path.Combine(packDirectory, finalFileName);
                PathUtilities.SafeCopyFile(actualSourcePath, destPath);
                rubSoundFileNames.Add(finalFileName);
            }

            var finishSoundFileNames = new List<string>();
            foreach (string sourcePath in _currentPack.FinishSounds)
            {
                string actualSourcePath = sourcePath;
                
                // If we copied to temp, use temp path
                if (tempCopyDirectory != null && Directory.Exists(tempCopyDirectory))
                {
                    string tempFileName = Path.GetFileName(sourcePath);
                    string tempPath = Path.Combine(tempCopyDirectory, tempFileName);
                    if (File.Exists(tempPath))
                    {
                        actualSourcePath = tempPath;
                    }
                }
                
                if (!File.Exists(actualSourcePath))
                {
                    MessageBox.Show($"Source file not found: {Path.GetFileName(sourcePath)}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                string finalFileName = Path.GetFileName(actualSourcePath);
                string destPath = Path.Combine(packDirectory, finalFileName);
                PathUtilities.SafeCopyFile(actualSourcePath, destPath);
                finishSoundFileNames.Add(finalFileName);
            }
            
            // Clean up temp directory if created
            if (tempCopyDirectory != null && Directory.Exists(tempCopyDirectory))
            {
                try { PathUtilities.SafeDeleteDirectory(tempCopyDirectory); } catch { }
            }
            
            var packJson = new
            {
                name = _currentPack.Name,
                version = _currentPack.Version,
                rubsounds = rubSoundFileNames,
                finishsound = finishSoundFileNames
            };

            string jsonPath = Path.Combine(packDirectory, "pack.json");
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(packJson, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

            // Store the created pack ID (folder name)
            CreatedPackId = folderName;

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    
    private bool UpdatePack()
    {
        if (_packManager == null || _originalPack == null || string.IsNullOrEmpty(_originalPack.PackDirectory))
        {
            MessageBox.Show("Cannot update pack: original pack information is missing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            if (_audioPlayer != null)
            {
                bool isCurrentlyActive = _originalPack.PackId == _audioPlayer.AudioPack;
                
                if (isCurrentlyActive)
                {
                    // Switch to built-in pack to release file locks
                    var allPacks = _packManager.GetAllPacks();
                    var builtInPack = allPacks.FirstOrDefault(p => p.IsBuiltIn);
                    if (builtInPack != null)
                    {
                        _audioPlayer.AudioPack = builtInPack.PackId;
                        // Give it more time to fully release the file locks
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        _audioPlayer.ReleaseFileLocks();
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                else
                {
                    // Pack is not active, but release locks anyway to be safe
                    // This handles cases where files might still be locked from previous loads
                    _audioPlayer.ReleaseFileLocks();
                    System.Threading.Thread.Sleep(500);
                }
                
                // Force garbage collection to ensure all file handles are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                System.Threading.Thread.Sleep(200);
            }
            
            string newVersion = IncrementVersion(_currentPack.Version);
            _currentPack.Version = newVersion;
            
            _currentPack.Name = _packNameTextBox?.Text.Trim() ?? _currentPack.Name;
            
            string packDirectory = _originalPack.PackDirectory;
            
            var rubSoundFileNames = new List<string>();
            foreach (string sourcePath in _currentPack.RubSounds)
            {
                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show($"Source file not found: {Path.GetFileName(sourcePath)}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(packDirectory, fileName);
                
                // If source and destination are the same file, skip copy (file is already in place)
                if (string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    rubSoundFileNames.Add(fileName);
                    continue;
                }
                
                PathUtilities.SafeCopyFile(sourcePath, destPath);
                rubSoundFileNames.Add(fileName);
            }

            var finishSoundFileNames = new List<string>();
            foreach (string sourcePath in _currentPack.FinishSounds)
            {
                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show($"Source file not found: {Path.GetFileName(sourcePath)}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(packDirectory, fileName);
                
                // If source and destination are the same file, skip copy (file is already in place)
                if (string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    finishSoundFileNames.Add(fileName);
                    continue;
                }
                
                PathUtilities.SafeCopyFile(sourcePath, destPath);
                finishSoundFileNames.Add(fileName);
            }

            // Remove files that are no longer in the pack
            var allExistingFiles = Directory.GetFiles(packDirectory, "*.wav");
            var currentFileNames = new HashSet<string>(rubSoundFileNames.Concat(finishSoundFileNames), StringComparer.OrdinalIgnoreCase);
            
            foreach (var existingFile in allExistingFiles)
            {
                string fileName = Path.GetFileName(existingFile);
                if (!currentFileNames.Contains(fileName) && !fileName.Equals("pack.json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(existingFile);
                    }
                    catch { }
                }
            }
            
            var packJson = new
            {
                name = _currentPack.Name,
                version = newVersion,
                rubsounds = rubSoundFileNames,
                finishsound = finishSoundFileNames
            };

            string jsonPath = Path.Combine(packDirectory, "pack.json");
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(packJson, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

            // Store the updated pack ID (same as original)
            CreatedPackId = _originalPack.PackId;

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update audio pack: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
    
    
    private string IncrementVersion(string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
            return "1.1";
        
        try
        {
            // Try to parse as "major.minor" format
            var parts = currentVersion.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                minor++;
                return $"{major}.{minor}";
            }
            else if (parts.Length == 1 && int.TryParse(parts[0], out int single))
            {
                return $"{single}.1";
            }
        }
        catch { }
        
        // Fallback: append .1 or increment
        return $"{currentVersion}.1";
    }
    
    private void UpdatePackAndClose()
    {
        if (UpdatePack())
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
    
    private void SaveAsNewPackAndClose()
    {
        if (CreatePack())
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
