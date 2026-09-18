using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Views;

public partial class MainWindow : Window
{
    private DeadLetterInspectorWindow? _activeInspectorWindow;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnMainWindowKeyDown, RoutingStrategies.Tunnel);
        Loaded += OnWindowLoaded;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        bool isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (isCmdOrCtrl)
        {
            int? targetNumber = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                Key.D4 or Key.NumPad4 => 4,
                Key.D5 or Key.NumPad5 => 5,
                Key.D6 or Key.NumPad6 => 6,
                Key.D7 or Key.NumPad7 => 7,
                Key.D8 or Key.NumPad8 => 8,
                Key.D9 or Key.NumPad9 => 9,
                _ => null
            };

            if (targetNumber.HasValue)
            {
                if (FocusHotlistInspectButton(targetNumber.Value - 1))
                {
                    e.Handled = true;
                }
                return;
            }
        }
        else if (e.Key is Key.Down or Key.Up)
        {
            if (TryNavigateHotlist(e.Key == Key.Down ? 1 : -1))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private bool FocusHotlistInspectButton(int zeroBasedIndex)
    {
        var hotlistControl = this.FindControl<ItemsControl>("HotlistControl");
        if (hotlistControl == null) return false;

        if (DataContext is MainWindowViewModel vm)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= vm.HotlistEntities.Count)
                return false;
        }

        var container = hotlistControl.ContainerFromIndex(zeroBasedIndex);
        if (container == null)
        {
            hotlistControl.UpdateLayout();
            container = hotlistControl.ContainerFromIndex(zeroBasedIndex);
        }

        if (container != null)
        {
            var button = container.FindDescendantOfType<Button>();
            if (button != null)
            {
                button.Focus();
                button.BringIntoView();
                return true;
            }
        }
        return false;
    }

    private bool TryNavigateHotlist(int direction)
    {
        var hotlistControl = this.FindControl<ItemsControl>("HotlistControl");
        if (hotlistControl == null) return false;

        var focusManager = FocusManager;
        var focused = focusManager?.GetFocusedElement() as Visual;
        if (focused == null) return false;

        if (focused is not Button focusedButton) return false;

        if (DataContext is not MainWindowViewModel vm) return false;

        for (int i = 0; i < vm.HotlistEntities.Count; i++)
        {
            var container = hotlistControl.ContainerFromIndex(i);
            if (container != null)
            {
                var btn = container.FindDescendantOfType<Button>();
                if (btn == focusedButton)
                {
                    int nextIndex = i + direction;
                    if (nextIndex >= 0 && nextIndex < vm.HotlistEntities.Count)
                    {
                        return FocusHotlistInspectButton(nextIndex);
                    }
                    return false;
                }
            }
        }
        return false;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenInspectorWindowRequested += OnOpenInspectorWindowRequested;
            vm.SettingsLoaded += OnSettingsLoaded;

            MessageDetailWindow.SaveSettingsCallback = (w, h, x, y) =>
            {
                vm.UpdateMessageDetailWindowBounds(w, h, x, y);
            };
        }
    }

    private void OnSettingsLoaded(AzureDeadLetterMonitor.Models.AppSettings settings)
    {
        if (settings.MessageDetailWindowWidth.HasValue)
            MessageDetailWindow.SavedWidth = settings.MessageDetailWindowWidth.Value;
        if (settings.MessageDetailWindowHeight.HasValue)
            MessageDetailWindow.SavedHeight = settings.MessageDetailWindowHeight.Value;
        if (settings.MessageDetailWindowX.HasValue && settings.MessageDetailWindowY.HasValue &&
            (settings.MessageDetailWindowX.Value != 0 || settings.MessageDetailWindowY.Value != 0))
        {
            MessageDetailWindow.SavedPosition = new Avalonia.PixelPoint(settings.MessageDetailWindowX.Value, settings.MessageDetailWindowY.Value);
        }
    }

    private void OnOpenInspectorWindowRequested(MessageInspectorViewModel inspectorVm)
    {
        if (_activeInspectorWindow != null)
        {
            _activeInspectorWindow.Activate();
            return;
        }

        var window = new DeadLetterInspectorWindow
        {
            DataContext = inspectorVm
        };

        window.Closed += (s, args) =>
        {
            _activeInspectorWindow = null;
        };

        _activeInspectorWindow = window;
        window.Show(this);
    }
}