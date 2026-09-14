using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Views;

public partial class MessageDetailWindow : Window
{
    public static double? SavedWidth { get; set; }
    public static double? SavedHeight { get; set; }
    public static PixelPoint? SavedPosition { get; set; }
    public static Action<double, double, int, int>? SaveSettingsCallback { get; set; }

    private PixelPoint? _lastValidPosition;
    private bool _isClosing = false;

    public MessageDetailWindow()
    {
        InitializeComponent();
        RestoreSavedBounds();
        Opened += OnWindowOpened;
        PositionChanged += OnPositionChanged;
        Loaded += OnWindowLoaded;
    }

    private void RestoreSavedBounds()
    {
        if (SavedWidth.HasValue && SavedWidth.Value > 100)
        {
            Width = SavedWidth.Value;
        }

        if (SavedHeight.HasValue && SavedHeight.Value > 100)
        {
            Height = SavedHeight.Value;
        }

        if (SavedPosition.HasValue && (SavedPosition.Value.X != 0 || SavedPosition.Value.Y != 0))
        {
            try
            {
                var screen = Screens?.ScreenFromPoint(SavedPosition.Value);
                if (screen != null)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Position = SavedPosition.Value;
                    _lastValidPosition = SavedPosition.Value;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
            }
            catch
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = SavedPosition.Value;
                _lastValidPosition = SavedPosition.Value;
            }
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (SavedPosition.HasValue && (SavedPosition.Value.X != 0 || SavedPosition.Value.Y != 0))
        {
            if (Position != SavedPosition.Value)
            {
                Position = SavedPosition.Value;
            }
        }

        if (Position.X != 0 || Position.Y != 0)
        {
            _lastValidPosition = Position;
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (!_isClosing && WindowState == WindowState.Normal)
        {
            if (e.Point.X != 0 || e.Point.Y != 0)
            {
                _lastValidPosition = e.Point;
            }
        }
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageDetailViewModel vm)
        {
            vm.CloseRequested += OnCloseRequested;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _isClosing = true;
        SaveCurrentBounds();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Do NOT call SaveCurrentBounds() here: in OnClosed the native window is detached and Position resets to (0,0).
        if (DataContext is MessageDetailViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }

    private void SaveCurrentBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            var w = Bounds.Width > 0 ? Bounds.Width : Width;
            var h = Bounds.Height > 0 ? Bounds.Height : Height;
            var pos = _lastValidPosition ?? Position;

            if (!double.IsNaN(w) && w > 100 && !double.IsNaN(h) && h > 100)
            {
                SavedWidth = w;
                SavedHeight = h;

                if (pos.X != 0 || pos.Y != 0)
                {
                    SavedPosition = pos;
                    SaveSettingsCallback?.Invoke(w, h, pos.X, pos.Y);
                }
                else if (SavedPosition.HasValue)
                {
                    SaveSettingsCallback?.Invoke(w, h, SavedPosition.Value.X, SavedPosition.Value.Y);
                }
            }
        }
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
}
