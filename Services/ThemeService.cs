using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using AzureDeadLetterMonitor.Models;

namespace AzureDeadLetterMonitor.Services;

public class ThemeService : IThemeService
{
    private static readonly Uri LightUri = new("avares://AzureDeadLetterMonitor/Themes/LightTheme.axaml");
    private static readonly Uri DarkUri = new("avares://AzureDeadLetterMonitor/Themes/DarkTheme.axaml");
    private static readonly Uri LegoUri = new("avares://AzureDeadLetterMonitor/Themes/LegoTheme.axaml");
    private static readonly Uri BarbieUri = new("avares://AzureDeadLetterMonitor/Themes/BarbieTheme.axaml");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        if (Application.Current == null) return;

        var targetUri = theme switch
        {
            AppTheme.Dark => DarkUri,
            AppTheme.Lego => LegoUri,
            AppTheme.Barbie => BarbieUri,
            _ => LightUri
        };

        var dict = new ResourceInclude(targetUri) { Source = targetUri };
        var merged = Application.Current.Resources.MergedDictionaries;

        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i] is ResourceInclude ri && ri.Source != null && ri.Source.OriginalString.Contains("/Themes/"))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Add(dict);
    }
}
