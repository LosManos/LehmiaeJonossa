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
    private bool _isShiftSpaceHandled = false;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnMainWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnMainWindowKeyUp, RoutingStrategies.Tunnel);
        Loaded += OnWindowLoaded;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                       !e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                       !e.KeyModifiers.HasFlag(KeyModifiers.Meta) &&
                       !e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // Shortcut to activate/toggle hamburger menu: Shift + Space
        if (isShift && e.Key == Key.Space)
        {
            _isShiftSpaceHandled = true;
            e.Handled = true;

            if (DataContext is MainWindowViewModel vm)
            {
                vm.ToggleMenu();
            }
            return;
        }

        // Close menu on Escape if open
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainWindowViewModel vm && vm.IsMenuOpen)
            {
                vm.CloseMenu();
                e.Handled = true;
                return;
            }
        }

        // Keyboard navigation when hamburger menu is open
        if (DataContext is MainWindowViewModel vmMenu && vmMenu.IsMenuOpen)
        {
            if (e.Key is Key.Down or Key.Up)
            {
                if (TryNavigateMenu(e.Key == Key.Down ? 1 : -1))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Right)
            {
                var focused = FocusManager?.GetFocusedElement() as Visual;
                if (focused is Button btn && btn.Name == "MenuThemeButton")
                {
                    if (!vmMenu.IsThemeSubMenuOpen)
                    {
                        vmMenu.IsThemeSubMenuOpen = true;
                    }
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var lightBtn = this.FindControl<Button>("MenuThemeLightButton");
                        lightBtn?.Focus();
                    });
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Left)
            {
                var focused = FocusManager?.GetFocusedElement() as Visual;
                if (focused is Button btn && (btn.Name?.StartsWith("MenuTheme") == true && btn.Name != "MenuThemeButton"))
                {
                    vmMenu.IsThemeSubMenuOpen = false;
                    var themeBtn = this.FindControl<Button>("MenuThemeButton");
                    themeBtn?.Focus();
                    e.Handled = true;
                    return;
                }
            }

            // Demo mode toggle with Space when MenuDemoButton is focused
            if (e.Key == Key.Space)
            {
                var focused = FocusManager?.GetFocusedElement() as Visual;
                if (focused is Button btn && btn.Name == "MenuDemoButton")
                {
                    vmMenu.ToggleDemoMode();
                    e.Handled = true;
                    return;
                }
            }
        }

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

    private bool TryNavigateMenu(int direction)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsMenuOpen)
            return false;

        var menuButtons = new System.Collections.Generic.List<Button>();

        void AddIfVisible(string buttonName)
        {
            var btn = this.FindControl<Button>(buttonName);
            if (btn != null && btn.IsVisible)
                menuButtons.Add(btn);
        }

        AddIfVisible("MenuAccountButton");
        AddIfVisible("MenuDemoButton");
        AddIfVisible("MenuThemeButton");

        if (vm.IsThemeSubMenuOpen)
        {
            AddIfVisible("MenuThemeLightButton");
            AddIfVisible("MenuThemeDarkButton");
            AddIfVisible("MenuThemeLegoButton");
            AddIfVisible("MenuThemeBarbieButton");
        }

        AddIfVisible("MenuCloseButton");

        if (menuButtons.Count == 0) return false;

        var focusManager = FocusManager;
        var focused = focusManager?.GetFocusedElement() as Visual;

        int currentIndex = -1;
        if (focused is Button focusedBtn)
        {
            currentIndex = menuButtons.IndexOf(focusedBtn);
        }

        int nextIndex;
        if (currentIndex == -1)
        {
            nextIndex = direction > 0 ? 0 : menuButtons.Count - 1;
        }
        else
        {
            nextIndex = (currentIndex + direction + menuButtons.Count) % menuButtons.Count;
        }

        var targetBtn = menuButtons[nextIndex];
        targetBtn.Focus();
        targetBtn.BringIntoView();
        return true;
    }

    private void OnMainWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (_isShiftSpaceHandled || e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _isShiftSpaceHandled = false;
            e.Handled = true;
            return;
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
            vm.OpenAccountDialogRequested += OnOpenAccountDialogRequested;
            vm.SettingsLoaded += OnSettingsLoaded;

            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsMenuOpen) && vm.IsMenuOpen)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var accountBtn = this.FindControl<Button>("MenuAccountButton");
                        accountBtn?.Focus();
                    });
                }
            };

            MessageDetailWindow.SaveSettingsCallback = (w, h, x, y) =>
            {
                vm.UpdateMessageDetailWindowBounds(w, h, x, y);
            };
        }
    }

    private void OnOpenAccountDialogRequested()
    {
        var dialog = new AccountDialogWindow
        {
            DataContext = this.DataContext
        };
        dialog.ShowDialog(this);
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