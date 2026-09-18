using System;
using System.Collections.Generic;
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
    private readonly List<long?> _pageStartSequenceNumbers = new() { null };

    [ObservableProperty]
    private string _entityName = string.Empty;

    [ObservableProperty]
    private string _entityDisplayName = string.Empty;

    [ObservableProperty]
    private string _entityKind = string.Empty;

    [ObservableProperty]
    private string? _subscriptionName;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(PageInfoDisplay))]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 20;

    [ObservableProperty]
    private bool _hasNextPage = false;

    [ObservableProperty]
    private bool _showKeyboardHints = true;

    public bool HasPreviousPage => CurrentPage > 1;

    public string PageInfoDisplay => $"Page {CurrentPage} • {Messages.Count} message{(Messages.Count == 1 ? "" : "s")} displayed";

    public ObservableCollection<DeadLetterMessageDetail> Messages { get; } = new();

    public event Action? CloseRequested;
    public event Action<DeadLetterMessageDetail, IReadOnlyList<DeadLetterMessageDetail>, int>? OpenMessageDetailRequested;

    public MessageInspectorViewModel(IServiceBusMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public async Task OpenForEntityAsync(ServiceBusEntityMetric entity)
    {
        EntityName = entity.Name;
        EntityDisplayName = entity.DisplayName;
        EntityKind = entity.KindDisplay;
        SubscriptionName = entity.SubscriptionName;
        DeadLetterCount = entity.DeadLetterMessageCount;
        IsOpen = true;
        ErrorMessage = null;
        CopyStatusText = "Copy Body";

        CurrentPage = 1;
        _pageStartSequenceNumbers.Clear();
        _pageStartSequenceNumbers.Add(null); // Page 1 starts from the beginning

        await LoadMessagesForCurrentPageAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(EntityName)) return;
        CurrentPage = 1;
        _pageStartSequenceNumbers.Clear();
        _pageStartSequenceNumbers.Add(null);
        await LoadMessagesForCurrentPageAsync();
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (!HasNextPage || Messages.Count == 0) return;

        var lastMessage = Messages.Last();
        long nextSequence = lastMessage.SequenceNumber + 1;

        if (_pageStartSequenceNumbers.Count <= CurrentPage)
        {
            _pageStartSequenceNumbers.Add(nextSequence);
        }
        else
        {
            _pageStartSequenceNumbers[CurrentPage] = nextSequence;
        }

        CurrentPage++;
        await LoadMessagesForCurrentPageAsync();
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (!HasPreviousPage) return;

        CurrentPage--;
        await LoadMessagesForCurrentPageAsync();
    }

    private async Task LoadMessagesForCurrentPageAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        Messages.Clear();
        SelectedMessage = null;

        try
        {
            long? fromSeq = (_pageStartSequenceNumbers.Count >= CurrentPage)
                ? _pageStartSequenceNumbers[CurrentPage - 1]
                : null;

            var results = await _monitorService.PeekDeadLetterMessagesAsync(
                EntityName, 
                SubscriptionName, 
                fromSequenceNumber: fromSeq, 
                maxMessages: PageSize);

            foreach (var msg in results)
            {
                Messages.Add(msg);
            }

            SelectedMessage = Messages.FirstOrDefault();
            HasNextPage = results.Count == PageSize;
            OnPropertyChanged(nameof(PageInfoDisplay));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to peek messages: {ex.Message}";
            HasNextPage = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenDetail(DeadLetterMessageDetail? message)
    {
        var targetMessage = message ?? SelectedMessage;
        if (targetMessage == null) return;

        var index = Messages.IndexOf(targetMessage);
        if (index < 0) index = 0;

        OpenMessageDetailRequested?.Invoke(targetMessage, Messages.ToList(), index);
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
