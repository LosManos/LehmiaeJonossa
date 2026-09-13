using System.Collections.Generic;

namespace AzureDeadLetterMonitor.Models;

public enum AuthMode
{
    DefaultAzureCredential,
    SystemAssignedManagedIdentity,
    UserAssignedManagedIdentity
}

public class AppSettings
{
    public List<string> ConfiguredNamespaces { get; set; } = new();
    public string SelectedNamespace { get; set; } = string.Empty;
    public AppTheme Theme { get; set; } = AppTheme.Light;
    public AuthMode AuthMode { get; set; } = AuthMode.DefaultAzureCredential;
    public string? UserAssignedClientId { get; set; }
    public int AutoRefreshSeconds { get; set; } = 60;
    public bool AutoRefreshEnabled { get; set; } = true;
}
