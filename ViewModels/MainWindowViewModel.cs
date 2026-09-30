using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LehmiaeJonossa.Models;
using LehmiaeJonossa.Services;

namespace LehmiaeJonossa.ViewModels;

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
    private string? _accountIdentity;

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

    [ObservableProperty]
    private bool _isMenuOpen = false;

    [ObservableProperty]
    private bool _isThemeSubMenuOpen = false;

    [ObservableProperty]
    private bool _showKeyboardHints = true;

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
        StatusMessage = "Loading settings & discovering Azure namespaces...";

        try
        {
            _settings = await _configService.LoadSettingsAsync();
            SettingsLoaded?.Invoke(_settings);

            SelectedTheme = _settings.Theme;
            _themeService.ApplyTheme(SelectedTheme);

            SelectedAuthMode = _settings.AuthMode;
            UserAssignedClientId = _settings.UserAssignedClientId;
            IsAutoRefreshEnabled = _settings.AutoRefreshEnabled;
            ShowKeyboardHints = _settings.ShowKeyboardHints;
            Inspector.ShowKeyboardHints = ShowKeyboardHints;
            _countdownSeconds = _settings.AutoRefreshSeconds;
            AutoRefreshCountdownText = $"{_countdownSeconds}s";
            IsDemoMode = _settings.IsDemoMode;
            OnPropertyChanged(nameof(DemoModeStatusText));

            if (IsAutoRefreshEnabled)
            {
                _refreshTimer.Start();
            }

            // Populate configured namespaces (cleaning out old placeholder domains)
            var cleanConfigured = _settings.ConfiguredNamespaces
                .Where(n => !n.Equals("sb-production.servicebus.windows.net", StringComparison.OrdinalIgnoreCase) &&
                            !n.Equals("sb-staging.servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Auto-discover live Azure Service Bus namespaces if not in demo mode
            if (!IsDemoMode)
            {
                var discovered = await _monitorService.DiscoverNamespacesAsync();
                foreach (var ns in discovered)
                {
                    if (!cleanConfigured.Contains(ns, StringComparer.OrdinalIgnoreCase))
                    {
                        cleanConfigured.Add(ns);
                    }
                }
            }

            Namespaces.Clear();
            foreach (var ns in cleanConfigured)
            {
                Namespaces.Add(ns);
            }

            if (!string.IsNullOrWhiteSpace(_settings.SelectedNamespace) &&
                Namespaces.Contains(_settings.SelectedNamespace) &&
                !_settings.SelectedNamespace.Equals("sb-production.servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                SelectedNamespace = _settings.SelectedNamespace;
            }
            else
            {
                SelectedNamespace = Namespaces.FirstOrDefault() ?? string.Empty;
            }

            _settings.ConfiguredNamespaces = Namespaces.ToList();
            _settings.SelectedNamespace = SelectedNamespace;
            _ = _configService.SaveSettingsAsync(_settings);

            if (IsDemoMode)
            {
                _monitorService.SetDemoMode(true);
                AuthStatus = _monitorService.AuthStatusMessage;
                AccountIdentity = _monitorService.CurrentAccountIdentity;
                await RefreshMetricsAsync();
            }
            else if (!string.IsNullOrWhiteSpace(SelectedNamespace))
            {
                await ConnectAndRefreshAsync();
            }
            else
            {
                _monitorService.SetDemoMode(true);
                IsDemoMode = true;
                _settings.IsDemoMode = true;
                _ = _configService.SaveSettingsAsync(_settings);
                AuthStatus = "No namespace configured (Demo Mode)";
                AccountIdentity = _monitorService.CurrentAccountIdentity;
                OnPropertyChanged(nameof(DemoModeStatusText));
                await RefreshMetricsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init warning: {ex.Message}";
            _monitorService.SetDemoMode(true);
            IsDemoMode = true;
            _settings.IsDemoMode = true;
            _ = _configService.SaveSettingsAsync(_settings);
            OnPropertyChanged(nameof(DemoModeStatusText));
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
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(IsLegoThemeSelected));
        OnPropertyChanged(nameof(IsBarbieThemeSelected));
    }

    partial void OnSelectedAuthModeChanged(AuthMode value)
    {
        _settings.AuthMode = value;
        _ = _configService.SaveSettingsAsync(_settings);
        _ = ConnectAndRefreshAsync();
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
            AccountIdentity = _monitorService.CurrentAccountIdentity;
            IsConnected = _monitorService.IsConnected;
            IsDemoMode = _monitorService.IsDemoMode;
            _settings.IsDemoMode = IsDemoMode;
            _ = _configService.SaveSettingsAsync(_settings);
            OnPropertyChanged(nameof(DemoModeStatusText));
            await RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            AuthStatus = $"Connection error: {ex.Message}";
            IsConnected = false;
            _rawEntities.Clear();
            FilteredEntities.Clear();
            HotlistEntities.Clear();
            TotalDeadLetterCount = 0;
            TotalActiveMessagesCount = 0;
            EntitiesRequiringAttentionCount = 0;
            HealthyEntitiesCount = 0;
            HasDeadLettersDetected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DiscoverNamespacesAsync()
    {
        IsBusy = true;
        StatusMessage = "Discovering Azure Service Bus namespaces...";

        try
        {
            var discovered = await _monitorService.DiscoverNamespacesAsync();
            int addedCount = 0;
            foreach (var ns in discovered)
            {
                if (!Namespaces.Contains(ns, StringComparer.OrdinalIgnoreCase))
                {
                    Namespaces.Add(ns);
                    addedCount++;
                }
            }

            if (string.IsNullOrWhiteSpace(SelectedNamespace) && Namespaces.Any())
            {
                SelectedNamespace = Namespaces.First();
            }

            _settings.ConfiguredNamespaces = Namespaces.ToList();
            await _configService.SaveSettingsAsync(_settings);

            StatusMessage = addedCount > 0
                ? $"Discovered and added {addedCount} Azure namespace(s)."
                : $"Discovered {discovered.Count} namespace(s) (all already present).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery failed: {ex.Message}";
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
            int hotlistIndex = 1;
            foreach (var dlq in _rawEntities.Where(e => e.DeadLetterMessageCount > 0))
            {
                dlq.HotlistIndex = hotlistIndex++;
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

    public event Action<AppSettings>? SettingsLoaded;
    public event Action<MessageInspectorViewModel>? OpenInspectorWindowRequested;
    public event Action? OpenAccountDialogRequested;

    public bool IsLightThemeSelected => SelectedTheme == AppTheme.Light;
    public bool IsDarkThemeSelected => SelectedTheme == AppTheme.Dark;
    public bool IsLegoThemeSelected => SelectedTheme == AppTheme.Lego;
    public bool IsBarbieThemeSelected => SelectedTheme == AppTheme.Barbie;
    public string DemoModeStatusText => IsDemoMode ? "ON" : "OFF";
    public string KeyboardHintsStatusText => ShowKeyboardHints ? "ON" : "OFF";

    public void UpdateMessageDetailWindowBounds(double width, double height, int x, int y)
    {
        _settings.MessageDetailWindowWidth = width;
        _settings.MessageDetailWindowHeight = height;
        _settings.MessageDetailWindowX = x;
        _settings.MessageDetailWindowY = y;
        _ = _configService.SaveSettingsAsync(_settings);
    }

    [RelayCommand]
    public async Task InspectEntityAsync(ServiceBusEntityMetric? entity)
    {
        if (entity == null) return;
        Inspector.ShowKeyboardHints = ShowKeyboardHints;
        var openTask = Inspector.OpenForEntityAsync(entity);
        OpenInspectorWindowRequested?.Invoke(Inspector);
        await openTask;
    }

    [RelayCommand]
    public void ToggleKeyboardHints()
    {
        ShowKeyboardHints = !ShowKeyboardHints;
        OnPropertyChanged(nameof(KeyboardHintsStatusText));
        Inspector.ShowKeyboardHints = ShowKeyboardHints;
        _settings.ShowKeyboardHints = ShowKeyboardHints;
        _ = _configService.SaveSettingsAsync(_settings);
    }

    [RelayCommand]
    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    [RelayCommand]
    public void CloseMenu()
    {
        IsMenuOpen = false;
        IsThemeSubMenuOpen = false;
    }

    [RelayCommand]
    public void ToggleThemeSubMenu()
    {
        IsThemeSubMenuOpen = !IsThemeSubMenuOpen;
    }

    [RelayCommand]
    public void OpenAccountDialog()
    {
        CloseMenu();
        OpenAccountDialogRequested?.Invoke();
    }

    [RelayCommand]
    public void SetTheme(AppTheme theme)
    {
        SelectedTheme = theme;
    }

    [RelayCommand]
    public void ToggleDemoMode()
    {
        IsDemoMode = !IsDemoMode;
        OnPropertyChanged(nameof(DemoModeStatusText));
        _settings.IsDemoMode = IsDemoMode;
        _ = _configService.SaveSettingsAsync(_settings);
        _monitorService.SetDemoMode(IsDemoMode);
        AuthStatus = _monitorService.AuthStatusMessage;
        AccountIdentity = _monitorService.CurrentAccountIdentity;
        if (!IsDemoMode && !string.IsNullOrWhiteSpace(SelectedNamespace))
        {
            _ = ConnectAndRefreshAsync();
        }
        else
        {
            _ = RefreshMetricsAsync();
        }
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
