using Newtonsoft.Json;

namespace NubRub.Models;

/// <summary>
/// Represents metadata for an audio pack loaded from a JSON file.
/// </summary>
public class AudioPackInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";

    [JsonProperty("rubsounds")]
    public List<string> RubSounds { get; set; } = new();

    [JsonProperty("finishsound")]
    public List<string> FinishSounds { get; set; } = new();

    /// <summary>
    /// Full path to the directory containing this audio pack.
    /// </summary>
    [JsonIgnore]
    public string PackDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a built-in pack (embedded resources) or a custom pack (file system).
    /// </summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// Unique identifier for the pack (built-in packs use their name, custom packs use directory name).
    /// </summary>
    [JsonIgnore]
    public string PackId { get; set; } = string.Empty;
}

