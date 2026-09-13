using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzureDeadLetterMonitor.Models;
using AzureDeadLetterMonitor.Services;

namespace AzureDeadLetterMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceBusMonitorService _monitorService;
    private readonly IConfigurationService _configService;
    private readonly IThemeService _themeService;
    private readonly DispatcherTimer _refreshTimer;

    private List<ServiceBusEntityMetric> _rawEntities = new();
    private AppSettings _settings = new();
    private int _countdownSeconds = 60;

    [ObservableProperty]
    private string _selectedNamespace = string.Empty;

    [ObservableProperty]
    private string _newNamespaceInput = string.Empty;

    [ObservableProperty]
    private bool _isAddingNamespace = false;

    [ObservableProperty]
    private AppTheme _selectedTheme = AppTheme.Light;

    [ObservableProperty]
    private AuthMode _selectedAuthMode = AuthMode.DefaultAzureCredential;

    [ObservableProperty]
    private string? _userAssignedClientId;

    [ObservableProperty]
    private string _authStatus = "Checking authentication...";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    [ObservableProperty]
    private string _autoRefreshCountdownText = "60s";

    [ObservableProperty]
    private bool _isDemoMode = false;

    // KPI Summary properties
    [ObservableProperty]
    private long _totalDeadLetterCount = 0;

    [ObservableProperty]
    private int _entitiesRequiringAttentionCount = 0;

    [ObservableProperty]
    private int _healthyEntitiesCount = 0;

    [ObservableProperty]
    private long _totalActiveMessagesCount = 0;

    [ObservableProperty]
    private bool _hasDeadLettersDetected = false;

    // Filter and Search
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    public ObservableCollection<string> Namespaces { get; } = new();
    public ObservableCollection<ServiceBusEntityMetric> HotlistEntities { get; } = new();
    public ObservableCollection<ServiceBusEntityMetric> FilteredEntities { get; } = new();

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = Enum.GetValues<AppTheme>();
    public IReadOnlyList<AuthMode> AvailableAuthModes { get; } = Enum.GetValues<AuthMode>();
    public IReadOnlyList<string> AvailableFilters { get; } = new[] { "All", "Dead Letters Only", "Healthy Only" };

    public MessageInspectorViewModel Inspector { get; }

    public MainWindowViewModel() : this(
        new AzureServiceBusMonitorService(),
        new ConfigurationService(),
        new ThemeService())
    {
    }

    public MainWindowViewModel(
        IServiceBusMonitorService monitorService,
        IConfigurationService configService,
        IThemeService themeService)
    {
        _monitorService = monitorService;
        _configService = configService;
        _themeService = themeService;

        Inspector = new MessageInspectorViewModel(_monitorService);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // Initialize async
        Dispatcher.UIThread.Post(async () => await InitializeAsync());
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading settings...";

        try
        {
            _settings = await _configService.LoadSettingsAsync();

            Namespaces.Clear();
            foreach (var ns in _settings.ConfiguredNamespaces)
            {
                Namespaces.Add(ns);
            }

            SelectedNamespace = Namespaces.Contains(_settings.SelectedNamespace)
                ? _settings.SelectedNamespace
                : Namespaces.FirstOrDefault() ?? "sb-production.servicebus.windows.net";

            SelectedTheme = _settings.Theme;
            _themeService.ApplyTheme(SelectedTheme);

            SelectedAuthMode = _settings.AuthMode;
            UserAssignedClientId = _settings.UserAssignedClientId;
            IsAutoRefreshEnabled = _settings.AutoRefreshEnabled;
            _countdownSeconds = _settings.AutoRefreshSeconds;
            AutoRefreshCountdownText = $"{_countdownSeconds}s";

            if (IsAutoRefreshEnabled)
            {
                _refreshTimer.Start();
            }

            // Attempt initial connection or fallback to demo
            await ConnectAndRefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init warning: {ex.Message}";
            // Default to demo mode if Azure credentials aren't available locally
            _monitorService.SetDemoMode(true);
            IsDemoMode = true;
            await RefreshMetricsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _themeService.ApplyTheme(value);
        _settings.Theme = value;
        _ = _configService.SaveSettingsAsync(_settings);
    }

    partial void OnSelectedNamespaceChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _settings.SelectedNamespace = value;
        _ = _configService.SaveSettingsAsync(_settings);
        _ = ConnectAndRefreshAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyEntityFilters();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyEntityFilters();
    }

    private async Task ConnectAndRefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedNamespace)) return;

        IsBusy = true;
        StatusMessage = $"Connecting to {SelectedNamespace}...";

        try
        {
            await _monitorService.ConnectAsync(SelectedNamespace, SelectedAuthMode, UserAssignedClientId);
            AuthStatus = _monitorService.AuthStatusMessage;
            IsConnected = _monitorService.IsConnected;
            IsDemoMode = _monitorService.IsDemoMode;
            await RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}. Falling back to Demo Mode.";
            AuthStatus = "Azure auth failed (using Demo mode)";
            _monitorService.SetDemoMode(true);
            IsDemoMode = true;
            IsConnected = false;
            await RefreshMetricsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        _countdownSeconds = _settings.AutoRefreshSeconds > 0 ? _settings.AutoRefreshSeconds : 60;
        AutoRefreshCountdownText = $"{_countdownSeconds}s";
        await RefreshMetricsAsync();
    }

    private async Task RefreshMetricsAsync()
    {
        IsBusy = true;
        StatusMessage = "Fetching entity metrics...";

        try
        {
            var entities = await _monitorService.GetEntityMetricsAsync();
            _rawEntities = entities.ToList();

            // Calculate KPIs
            TotalDeadLetterCount = _rawEntities.Sum(e => e.DeadLetterMessageCount);
            EntitiesRequiringAttentionCount = _rawEntities.Count(e => e.DeadLetterMessageCount > 0);
            HealthyEntitiesCount = _rawEntities.Count(e => e.DeadLetterMessageCount == 0);
            TotalActiveMessagesCount = _rawEntities.Sum(e => e.ActiveMessageCount);
            HasDeadLettersDetected = TotalDeadLetterCount > 0;

            // Populate Hotlist (only items with DeadLetterCount > 0)
            HotlistEntities.Clear();
            foreach (var dlq in _rawEntities.Where(e => e.DeadLetterMessageCount > 0))
            {
                HotlistEntities.Add(dlq);
            }

            ApplyEntityFilters();

            StatusMessage = $"Updated at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyEntityFilters()
    {
        FilteredEntities.Clear();

        var query = _rawEntities.AsEnumerable();

        // Search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(e => 
                e.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.KindDisplay.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Status Filter
        query = SelectedFilter switch
        {
            "Dead Letters Only" => query.Where(e => e.DeadLetterMessageCount > 0),
            "Healthy Only" => query.Where(e => e.DeadLetterMessageCount == 0),
            _ => query
        };

        foreach (var entity in query)
        {
            FilteredEntities.Add(entity);
        }
    }

    [RelayCommand]
    public async Task InspectEntityAsync(ServiceBusEntityMetric? entity)
    {
        if (entity == null) return;
        await Inspector.OpenForEntityAsync(entity);
    }

    [RelayCommand]
    public void ToggleDemoMode()
    {
        IsDemoMode = !IsDemoMode;
        _monitorService.SetDemoMode(IsDemoMode);
        AuthStatus = _monitorService.AuthStatusMessage;
        _ = RefreshMetricsAsync();
    }

    [RelayCommand]
    public void ShowAddNamespace()
    {
        IsAddingNamespace = true;
        NewNamespaceInput = string.Empty;
    }

    [RelayCommand]
    public void CancelAddNamespace()
    {
        IsAddingNamespace = false;
        NewNamespaceInput = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmAddNamespaceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNamespaceInput)) return;

        var ns = NewNamespaceInput.Trim();
        if (!Namespaces.Contains(ns))
        {
            Namespaces.Add(ns);
            _settings.ConfiguredNamespaces = Namespaces.ToList();
            await _configService.SaveSettingsAsync(_settings);
        }

        SelectedNamespace = ns;
        IsAddingNamespace = false;
        NewNamespaceInput = string.Empty;
    }

    [RelayCommand]
    public async Task RemoveSelectedNamespaceAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedNamespace) || Namespaces.Count <= 1) return;

        var toRemove = SelectedNamespace;
        Namespaces.Remove(toRemove);
        SelectedNamespace = Namespaces.FirstOrDefault() ?? string.Empty;

        _settings.ConfiguredNamespaces = Namespaces.ToList();
        _settings.SelectedNamespace = SelectedNamespace;
        await _configService.SaveSettingsAsync(_settings);
    }

    [RelayCommand]
    public void ToggleAutoRefresh()
    {
        IsAutoRefreshEnabled = !IsAutoRefreshEnabled;
        _settings.AutoRefreshEnabled = IsAutoRefreshEnabled;
        _ = _configService.SaveSettingsAsync(_settings);

        if (IsAutoRefreshEnabled)
        {
            _countdownSeconds = _settings.AutoRefreshSeconds > 0 ? _settings.AutoRefreshSeconds : 60;
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
            AutoRefreshCountdownText = "Paused";
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (!IsAutoRefreshEnabled || IsBusy) return;

        _countdownSeconds--;
        if (_countdownSeconds <= 0)
        {
            _countdownSeconds = _settings.AutoRefreshSeconds > 0 ? _settings.AutoRefreshSeconds : 60;
            _ = RefreshMetricsAsync();
        }

        AutoRefreshCountdownText = $"{_countdownSeconds}s";
    }
}
