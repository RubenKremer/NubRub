using System;
using System.Windows.Forms;

namespace NubRub;

public partial class UpdateProgressDialog : Form
{
    private ProgressBar _progressBar = null!;
    private Label _statusLabel = null!;
    private Button _cancelButton = null!;
    private bool _cancelled = false;

    public bool Cancelled => _cancelled;
    public string LastStatus => _statusLabel?.Text ?? string.Empty;

    public UpdateProgressDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Checking for Updates";
        this.Size = new System.Drawing.Size(400, 150);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.TopMost = true;

        _statusLabel = new Label
        {
            Text = "Checking for updates...",
            Location = new System.Drawing.Point(12, 15),
            Size = new System.Drawing.Size(360, 20),
            AutoSize = false
        };
        this.Controls.Add(_statusLabel);

        _progressBar = new ProgressBar
        {
            Location = new System.Drawing.Point(12, 45),
            Size = new System.Drawing.Size(360, 23),
            Style = ProgressBarStyle.Marquee
        };
        this.Controls.Add(_progressBar);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(297, 80),
            Size = new System.Drawing.Size(75, 23),
            DialogResult = DialogResult.Cancel
        };
        _cancelButton.Click += (s, e) =>
        {
            _cancelled = true;
            this.DialogResult = DialogResult.Cancel;
        };
        this.Controls.Add(_cancelButton);

        this.CancelButton = _cancelButton;
    }

    public void SetStatus(string status)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<string>(SetStatus), status);
            return;
        }
        _statusLabel.Text = status;
        Application.DoEvents();
    }

    public void SetProgress(int percent)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<int>(SetProgress), percent);
            return;
        }
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = Math.Clamp(percent, 0, 100);
        Application.DoEvents();
    }

    public void SetProgress(long bytesDownloaded, long totalBytes)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action<long, long>(SetProgress), bytesDownloaded, totalBytes);
            return;
        }
        
        if (totalBytes > 0)
        {
            var percent = (int)((bytesDownloaded * 100) / totalBytes);
            SetProgress(percent);
            
            var downloadedMB = bytesDownloaded / (1024.0 * 1024.0);
            var totalMB = totalBytes / (1024.0 * 1024.0);
            _statusLabel.Text = $"Downloading... {downloadedMB:F1} MB / {totalMB:F1} MB";
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _statusLabel.Text = "Downloading...";
        }
        
        Application.DoEvents();
    }
}

