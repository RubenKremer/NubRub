using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NubRub;

public class UpdateChecker
{
    private const string GitHubApiUrl = "https://api.github.com/repos/RubenKremer/NubRub/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateChecker()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NubRub-UpdateChecker/1.0");
    }

    public Version GetCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Try to get InformationalVersion first (from <Version> property)
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
            {
                if (Version.TryParse(informationalVersion.InformationalVersion, out var version))
                {
                    return version;
                }
            }
            
            // Fall back to AssemblyVersion
            var assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion != null)
            {
                return assemblyVersion;
            }
        }
        catch
        {
            // If version retrieval fails, return a default version
        }
        
        return new Version(1, 0, 0);
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Checking for updates...");
            
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            
            // Check for 404 - no releases exist yet
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                progress?.Report("No releases found");
                return new UpdateInfo
                {
                    LatestVersion = GetCurrentVersion(),
                    CurrentVersion = GetCurrentVersion(),
                    IsUpdateAvailable = false,
                    DownloadUrl = string.Empty,
                    ReleaseName = string.Empty,
                    ReleaseNotes = null
                };
            }
            
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var release = JObject.Parse(responseContent);
            
            var tagName = release["tag_name"]?.ToString();
            if (string.IsNullOrEmpty(tagName))
            {
                progress?.Report("Error: No tag found in release");
                return null;
            }
            
            // Parse version from tag (e.g., "v1.0.0" -> Version(1,0,0))
            var version = ParseVersionFromTag(tagName);
            if (version == null)
            {
                progress?.Report($"Error: Could not parse version from tag: {tagName}");
                return null;
            }
            
            // Find MSI installer in assets
            var assets = release["assets"] as JArray;
            if (assets == null || assets.Count == 0)
            {
                progress?.Report("Error: No assets found in release");
                return null;
            }
            
            JObject? msiAsset = null;
            foreach (var asset in assets)
            {
                var name = asset["name"]?.ToString();
                if (!string.IsNullOrEmpty(name) && name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    msiAsset = asset as JObject;
                    break;
                }
            }
            
            if (msiAsset == null)
            {
                progress?.Report("Error: No MSI installer found in release assets");
                return null;
            }
            
            var downloadUrl = msiAsset["browser_download_url"]?.ToString();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                progress?.Report("Error: No download URL found for MSI installer");
                return null;
            }
            
            var currentVersion = GetCurrentVersion();
            var isUpdateAvailable = version.CompareTo(currentVersion) > 0;
            
            return new UpdateInfo
            {
                LatestVersion = version,
                CurrentVersion = currentVersion,
                IsUpdateAvailable = isUpdateAvailable,
                DownloadUrl = downloadUrl,
                ReleaseName = release["name"]?.ToString() ?? tagName,
                ReleaseNotes = release["body"]?.ToString()
            };
        }
        catch (HttpRequestException ex)
        {
            // Check if we can get more details from the inner exception
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage = $"{ex.Message} ({ex.InnerException.Message})";
            }
            progress?.Report($"Network error: {errorMessage}");
            return null;
        }
        catch (TaskCanceledException)
        {
            progress?.Report("Request timed out. Please check your internet connection.");
            return null;
        }
        catch (Exception ex)
        {
            progress?.Report($"Error checking for updates: {ex.Message}");
            return null;
        }
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<(long bytesDownloaded, long totalBytes)>? progress = null)
    {
        try
        {
            progress?.Report((0, 0));
            
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var tempPath = Path.GetTempPath();
            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"NubRub-{DateTime.Now:yyyyMMddHHmmss}.msi";
            }
            var filePath = Path.Combine(tempPath, fileName);
            
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var contentStream = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[8192];
                long bytesDownloaded = 0;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    bytesDownloaded += bytesRead;
                    progress?.Report((bytesDownloaded, totalBytes));
                }
            }
            
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download installer: {ex.Message}", ex);
        }
    }

    private Version? ParseVersionFromTag(string tagName)
    {
        // Remove "v" prefix if present (e.g., "v1.0.0" -> "1.0.0")
        var versionString = tagName.TrimStart('v', 'V');
        
        // Try to parse as Version
        if (Version.TryParse(versionString, out var parsedVersion))
        {
            return parsedVersion;
        }
        
        // If that fails, try to extract version numbers using regex
        var match = Regex.Match(versionString, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (match.Success)
        {
            var major = int.Parse(match.Groups[1].Value);
            var minor = int.Parse(match.Groups[2].Value);
            var build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            var revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            
            return new Version(major, minor, build, revision);
        }
        
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class UpdateInfo
{
    public Version LatestVersion { get; set; } = new Version(1, 0, 0);
    public Version CurrentVersion { get; set; } = new Version(1, 0, 0);
    public bool IsUpdateAvailable { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
}

