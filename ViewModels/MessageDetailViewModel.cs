using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureDeadLetterMonitor.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzureDeadLetterMonitor.ViewModels;

public partial class MessageDetailViewModel : ViewModelBase
{
    private readonly IReadOnlyList<DeadLetterMessageDetail> _messages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionDisplay))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _currentIndex;

    [ObservableProperty]
    private DeadLetterMessageDetail? _currentMessage;

    [ObservableProperty]
    private string _copyStatusText = "Copy Body";

    public int TotalCount => _messages.Count;

    public string PositionDisplay => _messages.Count > 0 
        ? $"Message {CurrentIndex + 1} of {_messages.Count}" 
        : "No messages";

    public bool CanGoPrevious => CurrentIndex > 0;

    public bool CanGoNext => CurrentIndex < _messages.Count - 1;

    public event Action? CloseRequested;
    public event Action<DeadLetterMessageDetail>? CurrentMessageChanged;

    public MessageDetailViewModel(IReadOnlyList<DeadLetterMessageDetail> messages, int initialIndex = 0)
    {
        _messages = messages ?? Array.Empty<DeadLetterMessageDetail>();
        if (_messages.Count > 0)
        {
            _currentIndex = Math.Clamp(initialIndex, 0, _messages.Count - 1);
            _currentMessage = _messages[_currentIndex];
        }
    }

    [RelayCommand]
    public void Previous()
    {
        if (!CanGoPrevious) return;
        CurrentIndex--;
        CurrentMessage = _messages[CurrentIndex];
        CurrentMessageChanged?.Invoke(CurrentMessage);
    }

    [RelayCommand]
    public void Next()
    {
        if (!CanGoNext) return;
        CurrentIndex++;
        CurrentMessage = _messages[CurrentIndex];
        CurrentMessageChanged?.Invoke(CurrentMessage);
    }

    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    public async Task CopyBodyAsync()
    {
        if (CurrentMessage == null || string.IsNullOrEmpty(CurrentMessage.FormattedBody)) return;

        try
        {
            var clipboard = App.GetClipboard();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(CurrentMessage.FormattedBody);
                CopyStatusText = "Copied!";
                await Task.Delay(1500);
                CopyStatusText = "Copy Body";
            }
        }
        catch
        {
            // Clipboard fallback
        }
    }
}
