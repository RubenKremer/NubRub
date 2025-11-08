namespace NubRub.Models;

public class RawInputEventArgs : EventArgs
{
    public int X { get; set; }
    public int Y { get; set; }
    public IntPtr DeviceHandle { get; set; }
    
    // Properties for DebugWindow compatibility
    public int MouseX => X;
    public int MouseY => Y;
    public bool HasMovement => X != 0 || Y != 0;
}
