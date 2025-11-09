using System;
using System.IO;
using System.Threading;

namespace NubRub.Utilities;

public static class PathUtilities
{
    /// <summary>
    /// Sanitizes a directory name by removing invalid characters and normalizing it.
    /// </summary>
    /// <param name="name">The name to sanitize.</param>
    /// <returns>A sanitized directory name safe for use in file system paths.</returns>
    public static string SanitizeDirectoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove invalid characters
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = name;
        foreach (char c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Trim and replace spaces with underscores
        sanitized = sanitized.Trim().Replace(' ', '_');

        // Remove consecutive underscores
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');

        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "AudioPack";
        }

        return sanitized;
    }

    /// <summary>
    /// Safely deletes a directory with retry logic to handle locked files.
    /// </summary>
    /// <param name="path">The path of the directory to delete.</param>
    /// <param name="recursive">Whether to delete recursively.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <exception cref="IOException">Thrown if deletion fails after all retries.</exception>
    public static void SafeDeleteDirectory(string path, bool recursive = true, int maxRetries = 5)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                Directory.Delete(path, recursive);
                return; // Success
            }
            catch (IOException) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                // Exponential backoff: 200ms, 400ms, 800ms, 1600ms, 3200ms
                int delay = 200 * (int)Math.Pow(2, retryCount - 1);
                Thread.Sleep(delay);

                // Force garbage collection to release any lingering file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (UnauthorizedAccessException) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                // Exponential backoff: 200ms, 400ms, 800ms, 1600ms, 3200ms
                int delay = 200 * (int)Math.Pow(2, retryCount - 1);
                Thread.Sleep(delay);

                // Force garbage collection to release any lingering file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // If we get here, all retries failed
        throw new IOException($"Failed to delete directory after {maxRetries} attempts: {path}");
    }

    /// <summary>
    /// Safely copies a file with retry logic to handle locked files.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="destPath">The destination file path.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <exception cref="IOException">Thrown if copy fails after all retries.</exception>
    public static void SafeCopyFile(string sourcePath, string destPath, int maxRetries = 5)
    {
        // If source and destination are the same file, no copy needed
        if (string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase))
        {
            return; // File is already in the correct location
        }

        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                // If destination exists, try to delete it first (in case it's locked)
                if (File.Exists(destPath))
                {
                    // Try to delete with retry
                    int deleteRetries = 3;
                    for (int i = 0; i < deleteRetries; i++)
                    {
                        try
                        {
                            File.Delete(destPath);
                            break;
                        }
                        catch (IOException) when (i < deleteRetries - 1)
                        {
                            Thread.Sleep(100 * (i + 1)); // Exponential backoff
                        }
                    }
                }

                File.Copy(sourcePath, destPath, false);
                return; // Success
            }
            catch (IOException) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                // Exponential backoff: 200ms, 400ms, 800ms, 1600ms, 3200ms
                int delay = 200 * (int)Math.Pow(2, retryCount - 1);
                Thread.Sleep(delay);

                // Force garbage collection to release any lingering file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // If we get here, all retries failed
        throw new IOException($"Failed to copy file after {maxRetries} attempts: {Path.GetFileName(sourcePath)}");
    }
}


