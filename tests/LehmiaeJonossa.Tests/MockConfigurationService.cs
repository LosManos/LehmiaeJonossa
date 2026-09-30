using System.Threading.Tasks;
using LehmiaeJonossa.Models;
using LehmiaeJonossa.Services;

namespace LehmiaeJonossa.Tests;

public class MockConfigurationService : IConfigurationService
{
    public AppSettings SavedSettings { get; set; } = new();

    public AppSettings LoadSettings() => SavedSettings;

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(SavedSettings);

    public Task SaveSettingsAsync(AppSettings settings)
    {
        SavedSettings = settings;
        return Task.CompletedTask;
    }
}
