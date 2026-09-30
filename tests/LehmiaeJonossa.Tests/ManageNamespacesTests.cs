using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using LehmiaeJonossa.Models;
using LehmiaeJonossa.Services;
using LehmiaeJonossa.ViewModels;

namespace LehmiaeJonossa.Tests;

public class ManageNamespacesTests
{
    [Fact]
    public void MainWindowViewModel_OpenNamespacesDialogCommand_ClosesMenuAndRaisesEvent()
    {
        var vm = new MainWindowViewModel(new AzureServiceBusMonitorService(), new MockConfigurationService(), new ThemeService());
        vm.IsMenuOpen = true;

        bool eventRaised = false;
        vm.OpenNamespacesDialogRequested += () => eventRaised = true;

        vm.OpenNamespacesDialogCommand.Execute(null);

        Assert.False(vm.IsMenuOpen, "Menu should close when opening Namespaces dialog");
        Assert.True(eventRaised, "OpenNamespacesDialogRequested event should be fired");
    }

    [Fact]
    public void ManageNamespacesViewModel_Initializes_ItemsAndActiveBadgeCorrectly()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta", "sb-gamma" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-beta" };
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-beta",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        Assert.Equal(3, vm.NamespaceItems.Count);
        Assert.Equal("sb-alpha", vm.NamespaceItems[0].Name);
        Assert.False(vm.NamespaceItems[0].IsActive);

        Assert.Equal("sb-beta", vm.NamespaceItems[1].Name);
        Assert.True(vm.NamespaceItems[1].IsActive);

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal("sb-beta", vm.SelectedItem.Name);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_AddNamespace_AddsItem_AndSavesSettings()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        vm.NewNamespaceInput = "sb-new.servicebus.windows.net";
        await vm.AddNamespaceCommand.ExecuteAsync(null);

        Assert.Equal(2, namespaces.Count);
        Assert.Contains("sb-new.servicebus.windows.net", namespaces);
        Assert.Equal(2, vm.NamespaceItems.Count);
        Assert.Equal(string.Empty, vm.NewNamespaceInput);
        Assert.Contains("Added", vm.StatusMessage);
        Assert.Null(vm.ErrorMessage);

        Assert.Equal(2, configService.SavedSettings.ConfiguredNamespaces.Count);
        Assert.Contains("sb-new.servicebus.windows.net", configService.SavedSettings.ConfiguredNamespaces);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_AddNamespace_RejectsEmptyOrDuplicate()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        // 1. Empty string
        vm.NewNamespaceInput = "   ";
        await vm.AddNamespaceCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Single(namespaces);

        // 2. Duplicate string
        vm.NewNamespaceInput = "SB-ALPHA";
        await vm.AddNamespaceCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("already exists", vm.ErrorMessage);
        Assert.Single(namespaces);
    }

    [Fact]
    public void ManageNamespacesViewModel_StartEdit_PopulatesEditField()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings();
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        vm.SelectedItem = vm.NamespaceItems[1]; // sb-beta
        vm.StartEditCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Equal("sb-beta", vm.EditingNamespaceOriginal);
        Assert.Equal("sb-beta", vm.EditNamespaceInput);

        vm.CancelEditCommand.Execute(null);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_SaveEdit_RenamesItem_UpdatesActiveAndSettings()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        string? activatedNamespace = null;
        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            ns => activatedNamespace = ns
        );

        // Edit active namespace
        vm.SelectedItem = vm.NamespaceItems[0];
        vm.StartEditCommand.Execute(null);

        vm.EditNamespaceInput = "sb-alpha-renamed";
        await vm.SaveEditCommand.ExecuteAsync(null);

        Assert.False(vm.IsEditing);
        Assert.Equal("sb-alpha-renamed", namespaces[0]);
        Assert.Equal("sb-alpha-renamed", vm.NamespaceItems[0].Name);
        Assert.True(vm.NamespaceItems[0].IsActive);
        Assert.Equal("sb-alpha-renamed", activatedNamespace);
        Assert.Equal("sb-alpha-renamed", configService.SavedSettings.SelectedNamespace);
        Assert.Contains("sb-alpha-renamed", configService.SavedSettings.ConfiguredNamespaces);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_SaveEdit_RejectsDuplicateOrEmpty()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings();
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        vm.SelectedItem = vm.NamespaceItems[0];
        vm.StartEditCommand.Execute(null);

        // Empty
        vm.EditNamespaceInput = " ";
        await vm.SaveEditCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.IsEditing);

        // Duplicate
        vm.EditNamespaceInput = "sb-beta";
        await vm.SaveEditCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("already exists", vm.ErrorMessage);
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_DeleteNamespace_RemovesItem_AndUpdatesActiveWhenDeleted()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        string? activatedNamespace = null;
        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            ns => activatedNamespace = ns
        );

        // Delete active item sb-alpha
        vm.SelectedItem = vm.NamespaceItems[0];
        await vm.DeleteNamespaceCommand.ExecuteAsync(null);

        Assert.Single(namespaces);
        Assert.Equal("sb-beta", namespaces[0]);
        Assert.Single(vm.NamespaceItems);
        Assert.Equal("sb-beta", vm.NamespaceItems[0].Name);
        Assert.True(vm.NamespaceItems[0].IsActive);
        Assert.Equal("sb-beta", activatedNamespace);
        Assert.Equal("sb-beta", configService.SavedSettings.SelectedNamespace);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_DeleteNamespace_RejectsWhenSingleNamespace()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            _ => { }
        );

        vm.SelectedItem = vm.NamespaceItems[0];
        await vm.DeleteNamespaceCommand.ExecuteAsync(null);

        Assert.Single(namespaces);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Cannot remove", vm.ErrorMessage);
    }

    [Fact]
    public async Task ManageNamespacesViewModel_SetActiveNamespace_UpdatesActiveCallbackAndSettings()
    {
        var namespaces = new ObservableCollection<string> { "sb-alpha", "sb-beta" };
        var configService = new MockConfigurationService();
        var settings = new AppSettings { ConfiguredNamespaces = namespaces.ToList(), SelectedNamespace = "sb-alpha" };
        var monitorService = new AzureServiceBusMonitorService();

        string? activatedNamespace = null;
        var vm = new ManageNamespacesViewModel(
            namespaces,
            "sb-alpha",
            configService,
            settings,
            monitorService,
            true,
            ns => activatedNamespace = ns
        );

        vm.SelectedItem = vm.NamespaceItems[1]; // sb-beta
        await vm.SetActiveNamespaceCommand.ExecuteAsync(null);

        Assert.Equal("sb-beta", activatedNamespace);
        Assert.Equal("sb-beta", vm.ActiveNamespace);
        Assert.False(vm.NamespaceItems[0].IsActive);
        Assert.True(vm.NamespaceItems[1].IsActive);
        Assert.Equal("sb-beta", configService.SavedSettings.SelectedNamespace);
    }
}
