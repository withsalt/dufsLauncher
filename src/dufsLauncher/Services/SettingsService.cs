using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dufsLauncher.Services;

public record AppSettings
{
    public string ServePath { get; init; } = string.Empty;
    public int Port { get; init; } = 5000;
    public bool IsAllPermissions { get; init; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext;

public static class SettingsService
{
    private static readonly string SettingsDir = AppDomain.CurrentDomain.BaseDirectory;

    private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.dat");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // silently ignore save failures
        }
    }
}
