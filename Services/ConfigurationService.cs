using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigurationService(string? customConfigDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(customConfigDirectory))
        {
            _configDirectory = customConfigDirectory;
        }
        else if (IsRunningInTestRunner())
        {
            _configDirectory = Path.Combine(Path.GetTempPath(), "lehmiae-jonossa-test-" + Guid.NewGuid().ToString("N"));
        }
        else
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _configDirectory = Path.Combine(userHome, ".lehmiae-jonossa");
        }
        _configFilePath = Path.Combine(_configDirectory, "config.json");
    }

    private static bool IsRunningInTestRunner()
    {
        var domainName = AppDomain.CurrentDomain.FriendlyName;
        if (domainName.Contains("testhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name != null && (name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                                 name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings from {_configFilePath}: {ex.Message}");
            return new AppSettings();
        }

        var defaultSettings = new AppSettings();
        _ = SaveSettingsAsync(defaultSettings);
        return defaultSettings;
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings from {_configFilePath}: {ex.Message}");
            return new AppSettings();
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
