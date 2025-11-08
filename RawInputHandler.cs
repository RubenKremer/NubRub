using System.Runtime.InteropServices;
using NubRub.Models;
using System;

namespace NubRub;

public class RawInputHandler
{
    private const int WM_INPUT = 0x00FF;
    private const int RID_INPUT = 0x10000003;
    private const int RIM_TYPEMOUSE = 0;
    private const int RIM_TYPEKEYBOARD = 1;
    private const int RIM_TYPEHID = 2;
    private const uint RIDEV_INPUTSINK = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWMOUSE mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint RegisterRawInputDevices(
        [MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        IntPtr pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType;
        private uint _padding; // Ensure 16 bytes on 64-bit
    }

    private IntPtr? _selectedDeviceHandle;
    public event EventHandler<RawInputEventArgs>? InputReceived;
    public event EventHandler<string>? DebugLog;

    public bool Register(IntPtr hwnd)
    {
        var rid = new RAWINPUTDEVICE[1];
        rid[0].usUsagePage = 0x01; // Generic Desktop
        rid[0].usUsage = 0x02; // Mouse
        rid[0].dwFlags = RIDEV_INPUTSINK;
        rid[0].hwndTarget = hwnd;

        uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE));
        uint result = RegisterRawInputDevices(rid, 1, size);
        return result != 0;
    }

    public bool ProcessInput(IntPtr lParam)
    {
        uint dwSize = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

        if (dwSize == 0)
            return false;

        IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == 0)
                return false;

            RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);
            
            // If no device selected, allow all input through (for detection mode)
            if (!_selectedDeviceHandle.HasValue || rawInput.header.hDevice == _selectedDeviceHandle.Value)
            {
                InputReceived?.Invoke(this, new RawInputEventArgs
                {
                    X = rawInput.mouse.lLastX,
                    Y = rawInput.mouse.lLastY,
                    DeviceHandle = rawInput.header.hDevice
                });
                return true;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return false;
    }

    public void SetSelectedDevice(IntPtr deviceHandle)
    {
        _selectedDeviceHandle = deviceHandle;
    }

    public void ClearSelectedDevice()
    {
        _selectedDeviceHandle = null;
    }

    // MINIMAL VERSION - Just get device handles and types, no detailed info
    public List<DeviceInfo> EnumerateDevices()
    {
        var devices = new List<DeviceInfo>();
        
        DebugLog?.Invoke(this, "=== Starting minimal device enumeration ===");
        
        try
        {
            uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
            uint deviceCount = 0;
            
            // Get device count
            uint result = GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, size);
            if (result == unchecked((uint)-1))
            {
                int error = Marshal.GetLastWin32Error();
                DebugLog?.Invoke(this, $"GetRawInputDeviceList failed: error {error} (0x{error:X})");
                return devices;
            }
            
            if (deviceCount == 0)
            {
                DebugLog?.Invoke(this, "No raw input devices found");
                return devices;
            }
            
            DebugLog?.Invoke(this, $"Found {deviceCount} raw input device(s)");
            
            // Get device list
            IntPtr buffer = Marshal.AllocHGlobal((int)(deviceCount * size));
            try
            {
                result = GetRawInputDeviceList(buffer, ref deviceCount, size);
                if (result == unchecked((uint)-1))
                {
                    int error = Marshal.GetLastWin32Error();
                    DebugLog?.Invoke(this, $"GetRawInputDeviceList (2nd call) failed: error {error} (0x{error:X})");
                    return devices;
                }
                
                // Just enumerate devices and create basic DeviceInfo from handle
                for (uint i = 0; i < deviceCount; i++)
                {
                    try
                    {
                        IntPtr ptr = new IntPtr(buffer.ToInt64() + (i * size));
                        RAWINPUTDEVICELIST deviceList = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(ptr);
                        
                        string typeName = deviceList.dwType == RIM_TYPEMOUSE ? "Mouse" : 
                                         deviceList.dwType == RIM_TYPEKEYBOARD ? "Keyboard" :
                                         deviceList.dwType == RIM_TYPEHID ? "HID" : "Other";
                        
                        DebugLog?.Invoke(this, $"Device {i}: Type={typeName}, Handle={deviceList.hDevice}");
                        
                        // Add all HID and Mouse devices
                        if (deviceList.dwType == RIM_TYPEMOUSE || deviceList.dwType == RIM_TYPEHID)
                        {
                            var deviceInfo = new DeviceInfo
                            {
                                Handle = deviceList.hDevice,
                                DevicePath = $"Handle_{deviceList.hDevice}",
                                Manufacturer = string.Empty,
                                Product = $"{typeName} Device {i}",
                                VendorId = 0,
                                ProductId = 0
                            };
                            devices.Add(deviceInfo);
                            DebugLog?.Invoke(this, $"  Added: {deviceInfo.DisplayName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog?.Invoke(this, $"  Error processing device {i}: {ex.Message}");
                        // Continue with next device
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            
            DebugLog?.Invoke(this, $"=== Enumeration complete: {devices.Count} device(s) found ===");
        }
        catch (Exception ex)
        {
            DebugLog?.Invoke(this, $"Exception in EnumerateDevices: {ex.Message}");
            DebugLog?.Invoke(this, $"Stack trace: {ex.StackTrace}");
        }
        
        return devices;
    }
}
