using System.IO;
using System.Text;
using NubRub.Models;
using Newtonsoft.Json;

namespace NubRub;

public class ConfigManager
{
    private readonly string _configPath;
    private AppConfig? _config;

    public ConfigManager()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string configDir = Path.Combine(appDataPath, "NubRub");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");
    }

    public AppConfig Load()
    {
        if (_config != null)
            return _config;

        if (File.Exists(_configPath))
        {
            try
            {
                string json = File.ReadAllText(_configPath, Encoding.UTF8);
                _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                _config = new AppConfig();
            }
        }
        else
        {
            _config = new AppConfig();
        }

        return _config;
    }

    public void Save(AppConfig config)
    {
        try
        {
            _config = config;
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configPath, json, Encoding.UTF8);
        }
        catch
        {
            // Silently fail on save errors
        }
    }

    public DeviceInfo? FindMatchingDevice(List<DeviceInfo> availableDevices, DeviceInfo? savedDevice)
    {
        if (savedDevice == null) return null;

        foreach (var device in availableDevices)
        {
            if (savedDevice.Matches(device))
            {
                return device;
            }
        }

        return null;
    }
}

