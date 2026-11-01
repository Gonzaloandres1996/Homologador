using System.Text.Json;

namespace Homologador.WinForms.Services;

public static class AppConfigService
{
    private class AppSettings
    {
        public string? OutputDirectory { get; set; }
    }

    private static readonly string ConfigFilePath = GetConfigPath();

    private static string GetConfigPath()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var localFile = Path.Combine(baseDir, "homologador-config.json");
            // Test if we can write here
            var testFile = Path.Combine(baseDir, ".write-test");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return localFile;
        }
        catch
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "HomologadorDDC");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "homologador-config.json");
        }
    }

    public static string GetDefaultOutputDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        if (Directory.Exists(downloads))
        {
            return downloads;
        }

        try
        {
            Directory.CreateDirectory(downloads);
            return downloads;
        }
        catch
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    public static string GetOutputDirectory()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (!string.IsNullOrWhiteSpace(settings?.OutputDirectory))
                {
                    return settings.OutputDirectory.Trim();
                }
            }
        }
        catch
        {
            // Ignore error and fallback to default
        }

        return GetDefaultOutputDirectory();
    }

    public static void SetOutputDirectory(string path)
    {
        var dir = path.Trim();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var settings = new AppSettings { OutputDirectory = dir };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public static void ResetToDefault()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                File.Delete(ConfigFilePath);
            }
        }
        catch
        {
            // Ignore error
        }
    }
}
