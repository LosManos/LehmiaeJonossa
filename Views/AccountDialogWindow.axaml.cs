using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LehmiaeJonossa.Views;

public partial class AccountDialogWindow : Window
{
    public AccountDialogWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnAccountDialogKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnAccountDialogKeyDown(object? sender, KeyEventArgs e)
    {
        bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (e.Key == Key.Escape || e.Key == Key.Enter || (isAlt && e.Key == Key.D))
        {
            Close();
            e.Handled = true;
            return;
        }

        if (isAlt && e.Key == Key.A)
        {
            var combo = this.FindControl<ComboBox>("AuthModeComboBox");
            combo?.Focus();
            e.Handled = true;
            return;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
