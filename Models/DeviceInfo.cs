namespace NubRub.Models;

public class DeviceInfo
{
    public string DevicePath { get; set; } = string.Empty;
    public IntPtr Handle { get; set; } = IntPtr.Zero;
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;

    public string DisplayName
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(Manufacturer) 
                ? Product 
                : $"{Manufacturer} {Product}";
            
            if (VendorId != 0 || ProductId != 0)
            {
                name += $" (VID:{VendorId:X4} PID:{ProductId:X4})";
            }
            else
            {
                name += $" (Handle: {Handle})";
            }
            
            return name;
        }
    }

    public bool Matches(DeviceInfo other)
    {
        if (other == null) return false;
        
        // First try exact device path match
        if (!string.IsNullOrEmpty(DevicePath) && !string.IsNullOrEmpty(other.DevicePath))
        {
            if (DevicePath.Equals(other.DevicePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Fallback to VID/PID + product string match
        if (VendorId == other.VendorId && ProductId == other.ProductId)
        {
            if (!string.IsNullOrEmpty(Product) && !string.IsNullOrEmpty(other.Product))
            {
                return Product.Equals(other.Product, StringComparison.OrdinalIgnoreCase);
            }
            return true; // Match on VID/PID alone if product strings unavailable
        }

        return false;
    }
}

