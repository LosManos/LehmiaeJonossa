using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AzureDeadLetterMonitor.Models;

namespace AzureDeadLetterMonitor.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ConfigurationService()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _configDirectory = Path.Combine(userHome, ".azure-dead-letter-monitor");
        _configFilePath = Path.Combine(_configDirectory, "config.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings from {_configFilePath}: {ex.Message}");
        }

        var defaultSettings = new AppSettings();
        await SaveSettingsAsync(defaultSettings);
        return defaultSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings to {_configFilePath}: {ex.Message}");
        }
    }
}
