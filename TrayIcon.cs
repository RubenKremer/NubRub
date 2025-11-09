using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

namespace NubRub;

public class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _startWithWindowsItem;

    public event EventHandler? ConfigPanelRequested;
    public event EventHandler? DebugWindowRequested;
    public event EventHandler<bool>? StartWithWindowsToggled;
    public event EventHandler? UpdateCheckRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? AudioPackCreateRequested;
    public event EventHandler? AudioPackEditRequested;
    public event EventHandler? AudioPackExportRequested;
    public event EventHandler? AudioPackImportRequested;
    public event EventHandler? AudioPackDeleteRequested;

    private double _volume = 0.6;
    private bool _startWithWindows = false;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startWithWindows = value;
            UpdateStartWithWindowsText();
        }
    }

    public TrayIcon()
    {
        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        Icon? customIcon = LoadTrayIcon();
        
        _notifyIcon = new NotifyIcon
        {
            Icon = customIcon ?? SystemIcons.Application,
                Text = "NubRub",
            Visible = true
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ConfigPanelRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        CreateContextMenu();
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        var selectDeviceItem = new ToolStripMenuItem("Settings...", null, (s, e) =>
        {
            ConfigPanelRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(selectDeviceItem);

        var debugWindowItem = new ToolStripMenuItem("Show Debug Window", null, (s, e) =>
        {
            DebugWindowRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(debugWindowItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        _startWithWindowsItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            _startWithWindows = !_startWithWindows;
            UpdateStartWithWindowsText();
            StartWithWindowsToggled?.Invoke(this, _startWithWindows);
        });
        _contextMenu.Items.Add(_startWithWindowsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Audio Pack Management submenu
        var audioPackSubmenu = new ToolStripMenuItem("Audio Pack Management");
        var createPackItem = new ToolStripMenuItem("Create...", null, (s, e) =>
        {
            AudioPackCreateRequested?.Invoke(this, EventArgs.Empty);
        });
        audioPackSubmenu.DropDownItems.Add(createPackItem);
        
        var editPackItem = new ToolStripMenuItem("Edit...", null, (s, e) =>
        {
            AudioPackEditRequested?.Invoke(this, EventArgs.Empty);
        });
        audioPackSubmenu.DropDownItems.Add(editPackItem);
        
        var exportPackItem = new ToolStripMenuItem("Export...", null, (s, e) =>
        {
            AudioPackExportRequested?.Invoke(this, EventArgs.Empty);
        });
        audioPackSubmenu.DropDownItems.Add(exportPackItem);
        
        var importPackItem = new ToolStripMenuItem("Import...", null, (s, e) =>
        {
            AudioPackImportRequested?.Invoke(this, EventArgs.Empty);
        });
        audioPackSubmenu.DropDownItems.Add(importPackItem);
        
        var deletePackItem = new ToolStripMenuItem("Delete...", null, (s, e) =>
        {
            AudioPackDeleteRequested?.Invoke(this, EventArgs.Empty);
        });
        audioPackSubmenu.DropDownItems.Add(deletePackItem);
        
        _contextMenu.Items.Add(audioPackSubmenu);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var updateCheckItem = new ToolStripMenuItem("Check for updates", null, (s, e) =>
        {
            UpdateCheckRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(updateCheckItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(exitItem);

        _notifyIcon!.ContextMenuStrip = _contextMenu;
        UpdateStartWithWindowsText();
    }

    private void UpdateStartWithWindowsText()
    {
        if (_startWithWindowsItem != null)
        {
            _startWithWindowsItem.Checked = _startWithWindows;
        }
    }

    private Icon? LoadTrayIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Try to load ICO file first
            var icoStream = assembly.GetManifestResourceStream("NubRub.Resources.icons.trayicon.ico");
            if (icoStream != null)
            {
                return new Icon(icoStream);
            }
            
            // Try to load PNG and convert to Icon
            var pngStream = assembly.GetManifestResourceStream("NubRub.Resources.icons.trayicon.png");
            if (pngStream != null)
            {
                using (var bitmap = new Bitmap(pngStream))
                {
                    // Convert PNG to Icon (16x16 is standard for tray icons)
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

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}

