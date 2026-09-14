using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Views;

public partial class MainWindow : Window
{
    private DeadLetterInspectorWindow? _activeInspectorWindow;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
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