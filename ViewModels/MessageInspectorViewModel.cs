using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzureDeadLetterMonitor.Models;
using AzureDeadLetterMonitor.Services;

namespace AzureDeadLetterMonitor.ViewModels;

public partial class MessageInspectorViewModel : ViewModelBase
{
    private readonly IServiceBusMonitorService _monitorService;

    [ObservableProperty]
    private string _entityName = string.Empty;

    [ObservableProperty]
    private string _entityDisplayName = string.Empty;

    [ObservableProperty]
    private string _entityKind = string.Empty;

    [ObservableProperty]
    private long _deadLetterCount;

    [ObservableProperty]
    private bool _isOpen = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DeadLetterMessageDetail? _selectedMessage;

    [ObservableProperty]
    private string _copyStatusText = "Copy Body";

    public ObservableCollection<DeadLetterMessageDetail> Messages { get; } = new();

    public event Action? CloseRequested;

    public MessageInspectorViewModel(IServiceBusMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public async Task OpenForEntityAsync(ServiceBusEntityMetric entity)
    {
        EntityName = entity.Name;
        EntityDisplayName = entity.DisplayName;
        EntityKind = entity.KindDisplay;
        DeadLetterCount = entity.DeadLetterMessageCount;
        IsOpen = true;
        ErrorMessage = null;
        CopyStatusText = "Copy Body";

        await LoadMessagesAsync(entity.Name, entity.SubscriptionName);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(EntityName)) return;
        var subName = EntityDisplayName.Contains(" / ") ? EntityDisplayName.Split(" / ").Last().Trim() : null;
        await LoadMessagesAsync(EntityName, subName);
    }

    private async Task LoadMessagesAsync(string entityName, string? subName)
    {
        IsLoading = true;
        ErrorMessage = null;
        Messages.Clear();
        SelectedMessage = null;

        try
        {
            var results = await _monitorService.PeekDeadLetterMessagesAsync(entityName, subName, 20);
            foreach (var msg in results)
            {
                Messages.Add(msg);
            }

            SelectedMessage = Messages.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to peek messages: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    public async Task CopyBodyAsync()
    {
        if (SelectedMessage == null || string.IsNullOrEmpty(SelectedMessage.FormattedBody)) return;

        try
        {
            var clipboard = App.GetClipboard();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(SelectedMessage.FormattedBody);
                CopyStatusText = "Copied!";
                await Task.Delay(1500);
                CopyStatusText = "Copy Body";
            }
        }
        catch
        {
            // Clipboard error fallback
        }
    }
}
