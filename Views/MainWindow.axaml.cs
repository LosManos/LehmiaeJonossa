using Avalonia.Controls;
using Avalonia.Input;
using AzureDeadLetterMonitor.ViewModels;

namespace AzureDeadLetterMonitor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Inspector.Close();
        }
    }
}