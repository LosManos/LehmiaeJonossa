using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LehmiaeJonossa.ViewModels;

namespace LehmiaeJonossa.Views;

public partial class ManageNamespacesWindow : Window
{
    public ManageNamespacesWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnManageNamespacesKeyDown, RoutingStrategies.Tunnel);
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ManageNamespacesViewModel vm)
        {
            vm.CloseRequested += Close;

            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ManageNamespacesViewModel.IsEditing) && vm.IsEditing)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var editBox = this.FindControl<TextBox>("EditNamespaceTextBox");
                        if (editBox != null)
                        {
                            editBox.Focus();
                            editBox.SelectAll();
                        }
                    });
                }
            };

            Dispatcher.UIThread.Post(() =>
            {
                var listBox = this.FindControl<ListBox>("NamespacesListBox");
                listBox?.Focus();
            });
        }
    }

    private void OnManageNamespacesKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ManageNamespacesViewModel vm) return;

        bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var focused = FocusManager?.GetFocusedElement() as Visual;

        // Escape handling
        if (e.Key == Key.Escape)
        {
            if (vm.IsEditing)
            {
                vm.CancelEdit();
            }
            else
            {
                Close();
            }
            e.Handled = true;
            return;
        }

        // Enter key handling
        if (e.Key == Key.Enter)
        {
            if (focused is TextBox tb)
            {
                if (tb.Name == "NewNamespaceTextBox")
                {
                    if (vm.AddNamespaceCommand.CanExecute(null))
                    {
                        vm.AddNamespaceCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
                else if (tb.Name == "EditNamespaceTextBox")
                {
                    if (vm.SaveEditCommand.CanExecute(null))
                    {
                        vm.SaveEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }
            else
            {
                // In list or elsewhere: Enter sets the active namespace
                if (vm.SetActiveNamespaceCommand.CanExecute(null))
                {
                    vm.SetActiveNamespaceCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }

        // F2 key: trigger edit on selected item
        if (e.Key == Key.F2 && !vm.IsEditing)
        {
            if (vm.StartEditCommand.CanExecute(null))
            {
                vm.StartEditCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Delete / Backspace key: trigger delete when list is focused and NOT inside a textbox
        if ((e.Key == Key.Delete || e.Key == Key.Back) && focused is not TextBox && !vm.IsEditing)
        {
            if (vm.DeleteNamespaceCommand.CanExecute(null))
            {
                vm.DeleteNamespaceCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Alt shortcuts
        if (isAlt)
        {
            switch (e.Key)
            {
                case Key.A:
                    var newBox = this.FindControl<TextBox>("NewNamespaceTextBox");
                    newBox?.Focus();
                    e.Handled = true;
                    return;

                case Key.E:
                    if (!vm.IsEditing && vm.StartEditCommand.CanExecute(null))
                    {
                        vm.StartEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Key.R:
                    if (!vm.IsEditing && vm.DeleteNamespaceCommand.CanExecute(null))
                    {
                        vm.DeleteNamespaceCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Key.V:
                    if (vm.SetActiveNamespaceCommand.CanExecute(null))
                    {
                        vm.SetActiveNamespaceCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Key.O:
                    if (vm.DiscoverNamespacesCommand.CanExecute(null))
                    {
                        vm.DiscoverNamespacesCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Key.S when vm.IsEditing:
                    if (vm.SaveEditCommand.CanExecute(null))
                    {
                        vm.SaveEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;

                case Key.C when vm.IsEditing:
                    vm.CancelEdit();
                    e.Handled = true;
                    return;

                case Key.C when !vm.IsEditing:
                case Key.D:
                    Close();
                    e.Handled = true;
                    return;
            }
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
