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
        AddHandler(InputElement.KeyDownEvent, OnInspectorKeyDown, RoutingStrategies.Tunnel);
        MessagesGrid.AddHandler(InputElement.KeyDownEvent, OnMessagesGridKeyDown, RoutingStrategies.Tunnel);
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            vm.CloseRequested += OnCloseRequested;
            vm.OpenMessageDetailRequested += OnOpenMessageDetailRequested;
            vm.PropertyChanged += OnViewModelPropertyChanged;

            SyncSelection();
        }
        _isUserSelection = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            MessagesGrid.Focus();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;
            vm.OpenMessageDetailRequested -= OnOpenMessageDetailRequested;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _activeDetailWindow?.Close();
        _activeDetailWindow = null;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageInspectorViewModel.SelectedMessage) ||
            e.PropertyName == nameof(MessageInspectorViewModel.IsLoading))
        {
            SyncSelection();
        }
    }

    private void SyncSelection()
    {
        if (DataContext is MessageInspectorViewModel vm && vm.Messages.Count > 0)
        {
            if (vm.SelectedMessage == null)
            {
                vm.SelectedMessage = vm.Messages[0];
            }
            if (!Equals(MessagesGrid.SelectedItem, vm.SelectedMessage))
            {
                MessagesGrid.SelectedItem = vm.SelectedMessage;
            }
            var idx = vm.Messages.IndexOf(vm.SelectedMessage);
            if (idx >= 0 && MessagesGrid.SelectedIndex != idx)
            {
                MessagesGrid.SelectedIndex = idx;
            }
        }
    }

    private void OnMessagesGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OpenSelectedMessageDetail();
        }
    }

    private void OnInspectorKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = DataContext as MessageInspectorViewModel;
        if (vm == null) return;

        bool isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // 1. Refresh / Reset: F5, Cmd+R, Ctrl+R, Alt+R
        if (e.Key == Key.F5 || (isCmdOrCtrl && e.Key == Key.R) || (isAlt && e.Key == Key.R))
        {
            if (vm.RefreshCommand.CanExecute(null))
            {
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // 2. Escape or Alt+C: Close
        if (e.Key == Key.Escape || (isAlt && e.Key == Key.C))
        {
            Close();
            e.Handled = true;
            return;
        }

        // 3. Page Navigation: Cmd+Left (Prev Page) / Cmd+Right (Next Page), Alt+P / Alt+N
        // Previous Page: Cmd+Left or Alt+P
        if ((isCmdOrCtrl && e.Key == Key.Left) || (isAlt && e.Key == Key.P))
        {
            if (vm.HasPreviousPage && vm.PreviousPageCommand.CanExecute(null))
            {
                vm.PreviousPageCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Next Page: Cmd+Right or Alt+N
        if ((isCmdOrCtrl && e.Key == Key.Right) || (isAlt && e.Key == Key.N))
        {
            if (vm.HasNextPage && vm.NextPageCommand.CanExecute(null))
            {
                vm.NextPageCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // 4. Up / Down (or Cmd+Up / Cmd+Down) message navigation when grid is not focused
        if ((isCmdOrCtrl && (e.Key == Key.Up || e.Key == Key.Down)) || (!MessagesGrid.IsFocused && (e.Key == Key.Up || e.Key == Key.Down)))
        {
            if (vm.Messages.Count > 0)
            {
                int nextIdx = MessagesGrid.SelectedIndex;
                if (e.Key == Key.Down)
                {
                    nextIdx = Math.Min(nextIdx + 1, vm.Messages.Count - 1);
                }
                else
                {
                    nextIdx = Math.Max(nextIdx - 1, 0);
                }

                MessagesGrid.SelectedIndex = nextIdx;
                vm.SelectedMessage = vm.Messages[nextIdx];
                MessagesGrid.SelectedItem = vm.SelectedMessage;
                MessagesGrid.ScrollIntoView(vm.SelectedMessage, null);
                MessagesGrid.Focus();
                e.Handled = true;
                return;
            }
        }

        // 5. Enter or Space or Alt+D: Open details for selected message
        if (e.Key == Key.Enter || (e.Key == Key.Space && MessagesGrid.IsFocused) || (isAlt && e.Key == Key.D))
        {
            e.Handled = true;
            OpenSelectedMessageDetail();
            return;
        }
    }

    private void OpenSelectedMessageDetail()
    {
        var vm = DataContext as MessageInspectorViewModel;
        if (vm == null || vm.Messages.Count == 0) return;

        var target = vm.SelectedMessage
            ?? MessagesGrid.SelectedItem as DeadLetterMessageDetail
            ?? (MessagesGrid.SelectedIndex >= 0 && MessagesGrid.SelectedIndex < vm.Messages.Count ? vm.Messages[MessagesGrid.SelectedIndex] : null)
            ?? vm.Messages.FirstOrDefault();

        if (target != null)
        {
            vm.SelectedMessage = target;
            MessagesGrid.SelectedItem = target;
            var index = vm.Messages.IndexOf(target);
            OpenDetailWindow(vm.Messages.ToList(), index >= 0 ? index : 0);
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
            vm.SelectedMessage = msg;
            MessagesGrid.SelectedItem = msg;
            var index = vm.Messages.IndexOf(msg);
            OpenDetailWindow(vm.Messages.ToList(), index >= 0 ? index : 0);
        }
        else
        {
            OpenSelectedMessageDetail();
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        OpenSelectedMessageDetail();
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MessageInspectorViewModel vm)
        {
            if (MessagesGrid.SelectedItem is DeadLetterMessageDetail selected)
            {
                vm.SelectedMessage = selected;
            }
            else if (e.AddedItems.Count > 0 && e.AddedItems[0] is DeadLetterMessageDetail added)
            {
                vm.SelectedMessage = added;
            }

            if (_suppressSelectionEvent || !_isUserSelection) return;

            // If detail window is open, update it in-place
            if (_activeDetailWindow != null && vm.SelectedMessage != null && _activeDetailWindow.DataContext is MessageDetailViewModel detailVm)
            {
                var index = vm.Messages.IndexOf(vm.SelectedMessage);
                if (index >= 0)
                {
                    detailVm.CurrentIndex = Math.Clamp(index, 0, vm.Messages.Count - 1);
                    detailVm.CurrentMessage = vm.Messages[detailVm.CurrentIndex];
                }
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
        if (DataContext is MessageInspectorViewModel inspectorVm)
        {
            detailVm.ShowKeyboardHints = inspectorVm.ShowKeyboardHints;
        }

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
