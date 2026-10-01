using System.Threading.Tasks;
using LehmiaeJonossa.Models;
using LehmiaeJonossa.Services;

namespace LehmiaeJonossa.Tests;

public class MockConfigurationService : IConfigurationService
{
    public string ConfigFilePath { get; set; } = "/mock/path/config.json";
    public AppSettings SavedSettings { get; set; } = new();
    public bool OpenConfigFileCalled { get; private set; }
    public bool OpenConfigFileReturnValue { get; set; } = true;

    public AppSettings LoadSettings() => SavedSettings;

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(SavedSettings);

    public Task SaveSettingsAsync(AppSettings settings)
    {
        SavedSettings = settings;
        return Task.CompletedTask;
    }

    public bool OpenConfigFile()
    {
        OpenConfigFileCalled = true;
        return OpenConfigFileReturnValue;
    }
}
