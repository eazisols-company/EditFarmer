using Microsoft.Win32;

namespace CarrotDownload.Maui.Helpers;

public static class PathHelper
{
    private const string PreferenceKey = "EditFarmerPath";
    private const string RegistryPath = @"Software\Const and Props LLC\Edit Farmer";
    private const string RegistryValueName = "StoragePath";

    public static string GetBaseStoragePath()
    {
        // 1. Check Registry (Set by Inno Setup Installer) - Highest priority to respect installer selection
#if WINDOWS
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key != null)
            {
                var registryPath = key.GetValue(RegistryValueName) as string;
                if (!string.IsNullOrEmpty(registryPath))
                {
                    if (!Directory.Exists(registryPath))
                    {
                        Directory.CreateDirectory(registryPath);
                    }
                    
                    // Sync with preferences for cross-platform consistency if needed
                    Preferences.Set(PreferenceKey, registryPath);
                    return registryPath;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PathHelper] Registry read error: {ex.Message}");
        }
#endif

        // 2. Check Preferences (Fallback or for non-Windows/Dev environments)
        var storedPath = Preferences.Get(PreferenceKey, string.Empty);
        if (!string.IsNullOrEmpty(storedPath) && Directory.Exists(storedPath))
        {
            return storedPath;
        }

        // 3. Default Fallback
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Edit Farmer"
        );

        if (!Directory.Exists(defaultPath))
        {
            Directory.CreateDirectory(defaultPath);
        }

        return defaultPath;
    }

    public static string GetProjectsPath()
    {
        var path = Path.Combine(GetBaseStoragePath(), "Projects");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public static string GetProgrammingPath()
    {
        var path = Path.Combine(GetBaseStoragePath(), "Programming");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public static string GetProcessedFilesPath()
    {
        var path = Path.Combine(GetBaseStoragePath(), "Processed Files");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }
}
