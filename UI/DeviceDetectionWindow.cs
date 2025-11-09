using System.Windows.Forms;
using System.Drawing;
using NubRub.Models;

namespace NubRub.UI;

public partial class DeviceDetectionWindow : Form
{
    private Label _instructionLabel = null!;
    private Label _statusLabel = null!;
    private Button _cancelButton = null!;
    private ProgressBar _progressBar = null!;
    
    public DeviceInfo? DetectedDevice { get; private set; }
    
    public DeviceDetectionWindow()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        this.Text = "NubRub - Device Detection";
        this.Size = new Size(500, 250);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        
        int y = 20;
        
        _instructionLabel = new Label
        {
            Text = "Please rub your trackpoint nub now!",
            Location = new Point(20, y),
            Size = new Size(460, 40),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(_instructionLabel);
        y += 50;
        
        _statusLabel = new Label
        {
            Text = "Waiting for input...",
            Location = new Point(20, y),
            Size = new Size(460, 30),
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Blue
        };
        this.Controls.Add(_statusLabel);
        y += 40;
        
        _progressBar = new ProgressBar
        {
            Location = new Point(20, y),
            Size = new Size(460, 25),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };
        this.Controls.Add(_progressBar);
        y += 40;
        
        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(200, y),
            Size = new Size(100, 30),
            DialogResult = DialogResult.Cancel
        };
        _cancelButton.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };
        this.Controls.Add(_cancelButton);
        
        this.CancelButton = _cancelButton;
    }
    
    public void DeviceDetected(DeviceInfo device)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<DeviceInfo>(DeviceDetected), device);
            return;
        }
        
        DetectedDevice = device;
        _statusLabel.Text = $"Device detected: {device.DisplayName}";
        _statusLabel.ForeColor = Color.Green;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 100;
        
        // Close after a short delay
        System.Threading.Thread.Sleep(1000);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}

