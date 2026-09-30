using LehmiaeJonossa.Models;

namespace LehmiaeJonossa.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void ApplyTheme(AppTheme theme);
}
