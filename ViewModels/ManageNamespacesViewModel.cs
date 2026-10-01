using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LehmiaeJonossa.Models;
using LehmiaeJonossa.Services;

namespace LehmiaeJonossa.ViewModels;

public partial class NamespaceItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isActive;

    public NamespaceItemViewModel(string name, bool isActive)
    {
        _name = name;
        _isActive = isActive;
    }
}

public partial class ManageNamespacesViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _underlyingNamespaces;
    private readonly IConfigurationService _configService;
    private readonly AppSettings _settings;
    private readonly IServiceBusMonitorService _monitorService;
    private readonly Action<string> _setActiveCallback;

    public ObservableCollection<NamespaceItemViewModel> NamespaceItems { get; } = new();

    [ObservableProperty]
    private NamespaceItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _activeNamespace = string.Empty;

    [ObservableProperty]
    private string _newNamespaceInput = string.Empty;

    [ObservableProperty]
    private bool _isEditing = false;

    [ObservableProperty]
    private string _editingNamespaceOriginal = string.Empty;

    [ObservableProperty]
    private string _editNamespaceInput = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isDiscovering = false;

    [ObservableProperty]
    private bool _showKeyboardHints = true;

    public event Action? CloseRequested;

    public ManageNamespacesViewModel(
        ObservableCollection<string> underlyingNamespaces,
        string activeNamespace,
        IConfigurationService configService,
        AppSettings settings,
        IServiceBusMonitorService monitorService,
        bool showKeyboardHints,
        Action<string> setActiveCallback)
    {
        _underlyingNamespaces = underlyingNamespaces;
        _activeNamespace = activeNamespace;
        _configService = configService;
        _settings = settings;
        _monitorService = monitorService;
        _showKeyboardHints = showKeyboardHints;
        _setActiveCallback = setActiveCallback;

        RefreshItems();
    }

    public void RefreshItems()
    {
        NamespaceItems.Clear();
        foreach (var ns in _underlyingNamespaces)
        {
            bool isActive = ns.Equals(ActiveNamespace, StringComparison.OrdinalIgnoreCase);
            NamespaceItems.Add(new NamespaceItemViewModel(ns, isActive));
        }

        SelectedItem = NamespaceItems.FirstOrDefault(i => i.IsActive) ?? NamespaceItems.FirstOrDefault();
    }

    [RelayCommand]
    public async Task AddNamespaceAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewNamespaceInput))
        {
            ErrorMessage = "Please enter a namespace name.";
            return;
        }

        var trimmed = NewNamespaceInput.Trim();
        if (_underlyingNamespaces.Any(n => n.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorMessage = $"Namespace '{trimmed}' already exists.";
            return;
        }

        _underlyingNamespaces.Add(trimmed);
        var newItem = new NamespaceItemViewModel(trimmed, false);
        NamespaceItems.Add(newItem);
        SelectedItem = newItem;

        _settings.ConfiguredNamespaces = _underlyingNamespaces.ToList();
        await _configService.SaveSettingsAsync(_settings);

        NewNamespaceInput = string.Empty;
        StatusMessage = $"Added '{trimmed}'.";
    }

    [RelayCommand]
    public void StartEdit()
    {
        if (SelectedItem == null)
        {
            ErrorMessage = "Please select a namespace to edit.";
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        EditingNamespaceOriginal = SelectedItem.Name;
        EditNamespaceInput = SelectedItem.Name;
        IsEditing = true;
    }

    [RelayCommand]
    public async Task SaveEditAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(EditNamespaceInput))
        {
            ErrorMessage = "Namespace name cannot be empty.";
            return;
        }

        var updated = EditNamespaceInput.Trim();
        if (!updated.Equals(EditingNamespaceOriginal, StringComparison.OrdinalIgnoreCase) &&
            _underlyingNamespaces.Any(n => n.Equals(updated, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorMessage = $"Namespace '{updated}' already exists.";
            return;
        }

        int underlyingIndex = _underlyingNamespaces.IndexOf(EditingNamespaceOriginal);
        if (underlyingIndex >= 0)
        {
            bool wasActive = ActiveNamespace.Equals(EditingNamespaceOriginal, StringComparison.OrdinalIgnoreCase);
            _underlyingNamespaces[underlyingIndex] = updated;

            if (SelectedItem != null && SelectedItem.Name.Equals(EditingNamespaceOriginal, StringComparison.OrdinalIgnoreCase))
            {
                SelectedItem.Name = updated;
            }

            if (wasActive)
            {
                ActiveNamespace = updated;
                _setActiveCallback(updated);
                _settings.SelectedNamespace = updated;
                UpdateActiveBadges();
            }

            _settings.ConfiguredNamespaces = _underlyingNamespaces.ToList();
            await _configService.SaveSettingsAsync(_settings);

            StatusMessage = $"Updated to '{updated}'.";
        }

        IsEditing = false;
        EditingNamespaceOriginal = string.Empty;
        EditNamespaceInput = string.Empty;
    }

    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        EditingNamespaceOriginal = string.Empty;
        EditNamespaceInput = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task DeleteNamespaceAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (SelectedItem == null)
        {
            ErrorMessage = "Please select a namespace to remove.";
            return;
        }

        if (_underlyingNamespaces.Count <= 1)
        {
            ErrorMessage = "Cannot remove the only configured namespace.";
            return;
        }

        var toRemove = SelectedItem.Name;
        int index = NamespaceItems.IndexOf(SelectedItem);
        bool wasActive = ActiveNamespace.Equals(toRemove, StringComparison.OrdinalIgnoreCase);

        _underlyingNamespaces.Remove(toRemove);
        NamespaceItems.Remove(SelectedItem);

        if (wasActive)
        {
            var fallback = _underlyingNamespaces.FirstOrDefault() ?? string.Empty;
            ActiveNamespace = fallback;
            _setActiveCallback(fallback);
            _settings.SelectedNamespace = fallback;
            UpdateActiveBadges();
        }

        _settings.ConfiguredNamespaces = _underlyingNamespaces.ToList();
        await _configService.SaveSettingsAsync(_settings);

        if (NamespaceItems.Count > 0)
        {
            int newIndex = Math.Clamp(index, 0, NamespaceItems.Count - 1);
            SelectedItem = NamespaceItems[newIndex];
        }
        else
        {
            SelectedItem = null;
        }

        StatusMessage = $"Removed '{toRemove}'.";
    }

    [RelayCommand]
    public async Task SetActiveNamespaceAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (SelectedItem == null)
        {
            ErrorMessage = "Please select a namespace to activate.";
            return;
        }

        ActiveNamespace = SelectedItem.Name;
        _setActiveCallback(ActiveNamespace);
        _settings.SelectedNamespace = ActiveNamespace;
        await _configService.SaveSettingsAsync(_settings);

        UpdateActiveBadges();
        StatusMessage = $"'{ActiveNamespace}' is now the active namespace.";
    }

    private void UpdateActiveBadges()
    {
        foreach (var item in NamespaceItems)
        {
            item.IsActive = item.Name.Equals(ActiveNamespace, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    public async Task DiscoverNamespacesAsync()
    {
        ErrorMessage = null;
        StatusMessage = "Discovering namespaces from Azure CLI...";
        IsDiscovering = true;

        try
        {
            var discovered = await _monitorService.DiscoverNamespacesAsync();
            int addedCount = 0;
            foreach (var ns in discovered)
            {
                if (!_underlyingNamespaces.Contains(ns, StringComparer.OrdinalIgnoreCase))
                {
                    _underlyingNamespaces.Add(ns);
                    NamespaceItems.Add(new NamespaceItemViewModel(ns, false));
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                _settings.ConfiguredNamespaces = _underlyingNamespaces.ToList();
                await _configService.SaveSettingsAsync(_settings);
                StatusMessage = $"Discovered and added {addedCount} namespace(s).";
            }
            else
            {
                StatusMessage = discovered.Any()
                    ? "No new namespaces discovered (all already configured)."
                    : "No Service Bus namespaces found in current Azure subscription.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error during discovery: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    public string ConfigFilePath => _configService.ConfigFilePath;

    [RelayCommand]
    public void OpenConfigFile()
    {
        ErrorMessage = null;
        try
        {
            bool success = _configService.OpenConfigFile();
            if (success)
            {
                StatusMessage = $"Opened configuration file: {ConfigFilePath}";
            }
            else
            {
                ErrorMessage = $"Could not open configuration file at: {ConfigFilePath}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening config file: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }
}
