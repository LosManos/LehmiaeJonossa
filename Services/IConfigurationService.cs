using System.Threading.Tasks;
using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public interface IConfigurationService
{
    AppSettings LoadSettings();
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
