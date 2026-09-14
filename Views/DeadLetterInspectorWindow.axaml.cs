using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AzureDeadLetterMonitor.Models;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Views;

public partial class DeadLetterInspectorWindow : Window
{
    private MessageDetailWindow? _activeDetailWindow;
    private bool _isUserSelection = false;
    private bool _suppressSelectionEvent = false;

    public DeadLetterInspectorWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            vm.CloseRequested += OnCloseRequested;
            vm.OpenMessageDetailRequested += OnOpenMessageDetailRequested;
        }
        _isUserSelection = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;
            vm.OpenMessageDetailRequested -= OnOpenMessageDetailRequested;
        }

        _activeDetailWindow?.Close();
        _activeDetailWindow = null;
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseRequested()
    {
        Close();
    }

    private void OnOpenMessageDetailRequested(DeadLetterMessageDetail message, System.Collections.Generic.IReadOnlyList<DeadLetterMessageDetail> messages, int index)
    {
        OpenDetailWindow(messages, index);
    }

    private void OnDetailsButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DeadLetterMessageDetail msg && DataContext is MessageInspectorViewModel vm)
        {
            var index = vm.Messages.IndexOf(msg);
            OpenDetailWindow(vm.Messages.ToList(), index >= 0 ? index : 0);
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm && vm.SelectedMessage != null)
        {
            var index = vm.Messages.IndexOf(vm.SelectedMessage);
            OpenDetailWindow(vm.Messages.ToList(), index >= 0 ? index : 0);
        }
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvent || !_isUserSelection) return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is DeadLetterMessageDetail selected && DataContext is MessageInspectorViewModel vm)
        {
            var index = vm.Messages.IndexOf(selected);
            if (index >= 0)
            {
                OpenDetailWindow(vm.Messages.ToList(), index);
            }
        }
    }

    private void OpenDetailWindow(System.Collections.Generic.IReadOnlyList<DeadLetterMessageDetail> messages, int index)
    {
        if (messages.Count == 0) return;

        if (_activeDetailWindow != null)
        {
            // Update existing detail window
            if (_activeDetailWindow.DataContext is MessageDetailViewModel existingVm)
            {
                existingVm.CurrentIndex = Math.Clamp(index, 0, messages.Count - 1);
                existingVm.CurrentMessage = messages[existingVm.CurrentIndex];
            }
            _activeDetailWindow.Activate();
            return;
        }

        var detailVm = new MessageDetailViewModel(messages, index);
        var detailWindow = new MessageDetailWindow
        {
            DataContext = detailVm
        };

        detailVm.CurrentMessageChanged += OnDetailMessageChanged;
        detailWindow.Closed += (s, args) =>
        {
            _activeDetailWindow = null;
        };

        _activeDetailWindow = detailWindow;
        detailWindow.Show(this);
    }

    private void OnDetailMessageChanged(DeadLetterMessageDetail message)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            _suppressSelectionEvent = true;
            try
            {
                vm.SelectedMessage = message;
                MessagesGrid.SelectedItem = message;
                MessagesGrid.ScrollIntoView(message, null);
            }
            finally
            {
                _suppressSelectionEvent = false;
            }
        }
    }
}
