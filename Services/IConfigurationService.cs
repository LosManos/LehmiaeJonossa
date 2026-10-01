using System.Threading.Tasks;
using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public interface IConfigurationService
{
    string ConfigFilePath { get; }
    AppSettings LoadSettings();
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    bool OpenConfigFile();
}
