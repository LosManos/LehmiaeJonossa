using System.Threading.Tasks;
using AzureDeadLetterMonitor.Models;

namespace AzureDeadLetterMonitor.Services;

public interface IConfigurationService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
