using System.Windows.Forms;
using System.Drawing;
using System.Reflection;

namespace NubRub;

public partial class MainForm : Form
{
    private const int WM_INPUT = 0x00FF;
    private RawInputHandler? _rawInputHandler;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "NubRub";
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Size = new Size(1, 1); // Need non-zero size for handle
        this.Location = new Point(-1000, -1000); // Move off-screen
        this.Opacity = 0; // Make invisible
        
        // Set application icon if available
        Icon? appIcon = LoadApplicationIcon();
        if (appIcon != null)
        {
            this.Icon = appIcon;
        }
    }
    
    private Icon? LoadApplicationIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Try to load ICO file first
            var icoStream = assembly.GetManifestResourceStream("NubRub.Resources.icons.appicon.ico");
            if (icoStream != null)
            {
                return new Icon(icoStream);
            }
            
            // Try to load PNG and convert to Icon
            var pngStream = assembly.GetManifestResourceStream("NubRub.Resources.icons.appicon.png");
            if (pngStream != null)
            {
                using (var bitmap = new Bitmap(pngStream))
                {
                    // Convert PNG to Icon (32x32 or 48x48 recommended for app icons)
                    IntPtr hIcon = bitmap.GetHicon();
                    return Icon.FromHandle(hIcon);
                }
            }
        }
        catch
        {
            // If icon loading fails, return null to use default
        }
        
        return null;
    }

    public bool Initialize(RawInputHandler rawInputHandler)
    {
        _rawInputHandler = rawInputHandler;
        // Ensure handle is created
        if (!this.IsHandleCreated)
        {
            this.CreateHandle();
        }
        
        // Force handle creation and wait a bit for it to be fully initialized
        IntPtr handle = this.Handle;
        System.Diagnostics.Debug.WriteLine($"MainForm.Initialize: Handle={handle} (0x{handle:X}), IsHandleCreated={this.IsHandleCreated}");
        
        // Small delay to ensure handle is fully initialized
        System.Threading.Thread.Sleep(50);
        Application.DoEvents();
        
        return _rawInputHandler.Register(handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT && _rawInputHandler != null)
        {
            _rawInputHandler.ProcessInput(m.LParam);
        }
        base.WndProc(ref m);
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false); // Always hide the form
    }
}

