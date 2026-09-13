using AzureDeadLetterMonitor.Models;

namespace AzureDeadLetterMonitor.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void ApplyTheme(AppTheme theme);
}
