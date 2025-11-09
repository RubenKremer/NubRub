using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NubRub;

/// <summary>
/// Manages Windows file association for .nubrub files.
/// </summary>
public static class FileAssociationManager
{
    private const string FILE_EXTENSION = ".nubrub";
    private const string PROG_ID = "NubRub.Pack";
    private const string FILE_TYPE_NAME = "NubRub Audio Pack";

    /// <summary>
    /// Registers the .nubrub file extension with Windows.
    /// </summary>
    public static bool RegisterFileAssociation(string applicationPath, string iconPath)
    {
        try
        {
            // Register file extension
            using (var key = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{FILE_EXTENSION}"))
            {
                key?.SetValue("", PROG_ID);
            }

            // Register ProgID
            using (var key = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{PROG_ID}"))
            {
                if (key != null)
                {
                    key.SetValue("", FILE_TYPE_NAME);
                    
                    using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", $"\"{iconPath}\",0");
                    }

                    // Register open command
                    using (var shellKey = key.CreateSubKey("shell\\open\\command"))
                    {
                        shellKey?.SetValue("", $"\"{applicationPath}\" \"%1\"");
                    }
                }
            }

            // Notify Windows of the change
            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unregisters the .nubrub file extension.
    /// </summary>
    public static bool UnregisterFileAssociation()
    {
        try
        {
            // Remove file extension
            Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{FILE_EXTENSION}", false);

            // Remove ProgID
            Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{PROG_ID}", false);

            // Notify Windows of the change
            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the file association is already registered.
    /// </summary>
    public static bool IsFileAssociationRegistered()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{FILE_EXTENSION}"))
            {
                return key != null;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the icon path for .nubrub files. Prioritizes nubrub.ico specifically for the .nubrub file type.
    /// </summary>
    public static string GetIconPath()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        
        string nubrubIconPath = Path.Combine(appDir, "Resources", "icons", "nubrub.ico");
        if (File.Exists(nubrubIconPath))
        {
            return nubrubIconPath;
        }
        
        string nubrubIconPath2 = Path.Combine(appDir, "nubrub.ico");
        if (File.Exists(nubrubIconPath2))
        {
            return nubrubIconPath2;
        }
        
        string appIconPath = Path.Combine(appDir, "Resources", "icons", "appicon.ico");
        if (File.Exists(appIconPath))
        {
            return appIconPath;
        }
        
        string appIconPath2 = Path.Combine(appDir, "appicon.ico");
        if (File.Exists(appIconPath2))
        {
            return appIconPath2;
        }
        
        return Application.ExecutablePath;
    }
}

/// <summary>
/// Native Windows API methods for file association.
/// </summary>
internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

