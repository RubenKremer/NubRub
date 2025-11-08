using System.Windows.Forms;
using System.Drawing;

using NubRub.Models;

namespace NubRub;

public partial class DebugWindow : Form
{
    private TextBox _outputTextBox = null!;
    private Button _clearButton = null!;
    private CheckBox _autoScrollCheckBox = null!;
    private bool _autoScroll = true;

    public DebugWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "NubRub - Debug Output";
        this.Size = new Size(600, 400);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(400, 300);
        this.FormClosing += (s, e) =>
        {
            // Prevent closing - minimize instead
            e.Cancel = true;
            this.WindowState = FormWindowState.Minimized;
        };

        // Output text box
        _outputTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen
        };
        this.Controls.Add(_outputTextBox);

        // Bottom panel for controls
        var bottomPanel = new Panel
        {
            Height = 40,
            Dock = DockStyle.Bottom
        };
        this.Controls.Add(bottomPanel);

        // Auto-scroll checkbox
        _autoScrollCheckBox = new CheckBox
        {
            Text = "Auto-scroll",
            Checked = true,
            Location = new Point(10, 10),
            AutoSize = true
        };
        _autoScrollCheckBox.CheckedChanged += (s, e) => _autoScroll = _autoScrollCheckBox.Checked;
        bottomPanel.Controls.Add(_autoScrollCheckBox);

        // Clear button
        _clearButton = new Button
        {
            Text = "Clear",
            Location = new Point(120, 8),
            Width = 80
        };
        _clearButton.Click += (s, e) => Clear();
        bottomPanel.Controls.Add(_clearButton);

        // Close button
        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(210, 8),
            Width = 80
        };
        closeButton.Click += (s, e) => this.Hide();
        bottomPanel.Controls.Add(closeButton);
    }

    public void Log(string message)
    {
        try
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    Invoke(new Action<string>(Log), message);
                }
                return;
            }

            if (!IsHandleCreated || _outputTextBox == null)
            {
                // Form not ready yet, queue the message
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _outputTextBox.AppendText($"[{timestamp}] {message}\r\n");

            if (_autoScroll)
            {
                _outputTextBox.SelectionStart = _outputTextBox.Text.Length;
                _outputTextBox.ScrollToCaret();
            }
        }
        catch
        {
            // Silently fail if logging fails
        }
    }

    public void LogRawInput(RawInputEventArgs e)
    {
        Log($"Movement: X={e.MouseX}, Y={e.MouseY}, HasMovement={e.HasMovement}");
    }

    public void LogDeviceInfo(string message)
    {
        Log($"Device: {message}");
    }

    public void LogAudio(string message)
    {
        Log($"Audio: {message}");
    }

    public void LogWiggle(string message)
    {
        Log($"Wiggle: {message}");
    }

    public void Clear()
    {
        try
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    Invoke(new Action(Clear));
                }
                return;
            }

            if (_outputTextBox != null && IsHandleCreated)
            {
                _outputTextBox.Clear();
                Log("NubRub Debug Window");
                Log("======================");
                Log("Waiting for TrackPoint input...");
                Log("");
            }
        }
        catch
        {
            // Silently fail if clear fails
        }
    }
}

