using Newtonsoft.Json;

namespace NubRub.Models;

public class AppConfig
{
    [JsonProperty("SelectedDevice")]
    public DeviceInfo? SelectedDevice { get; set; }

    [JsonProperty("Audio")]
    public AudioSettings Audio { get; set; } = new();

    [JsonProperty("Startup")]
    public StartupSettings Startup { get; set; } = new();

    [JsonProperty("Enabled")]
    public bool Enabled { get; set; } = true;
}

public class AudioSettings
{
    [JsonProperty("Volume")]
    public double Volume { get; set; } = 0.6;

    [JsonProperty("IdleCutoffMs")]
    public int IdleCutoffMs { get; set; } = 250;

    [JsonProperty("OnlyOnMovement")]
    public bool OnlyOnMovement { get; set; } = true;

    [JsonProperty("AudioPack")]
    public string AudioPack { get; set; } = "squeak";

    [JsonProperty("WiggleDurationMs")]
    public int WiggleDurationMs { get; set; } = 25000;
}

public class StartupSettings
{
    [JsonProperty("RunAtLogin")]
    public bool RunAtLogin { get; set; } = false;
}

