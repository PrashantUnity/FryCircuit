using System.Linq;
using System.Threading.Tasks;
using FryCircuit.Plugin;
using FryWorld.Sdk.Testing;
using Xunit;

namespace FryCircuit.Plugin.Tests;

public sealed class CircuitPluginTests
{
    [Fact]
    public async Task MountAsync_RegistersNavigationItemOverlayAndCommands()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        var navItems = host.Context.GetRegisteredNavigationItems();
        Assert.Contains(navItems, n => n.Id == "com.fryworld.circuit");

        var overlays = host.Context.GetRegisteredOverlays();
        Assert.Contains(overlays, o => o.Id == "com.fryworld.circuit.overlay");

        var commands = host.Context.GetRegisteredCommands();
        Assert.Contains(commands, c => c.Id == "cmd.circuit.open");
        Assert.Contains(commands, c => c.Id == "cmd.circuit.overlay");

        var statusWidgets = host.Context.GetRegisteredStatusBarWidgets();
        Assert.Contains(statusWidgets, s => s.Id == "fryworld.status.circuit");

        var ribbonTabs = host.Context.GetRegisteredRibbonTabs();
        Assert.Contains(ribbonTabs, r => r.Id == "circuit");

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task OpenCommand_NavigatesToCircuitStudio()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        await host.RunCommandAsync("cmd.circuit.open");
        Assert.Equal("com.fryworld.circuit", host.Shell.LastNavigation);

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task OpenOverlayCommand_ShowsOverlay()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        await host.RunCommandAsync("cmd.circuit.overlay");
        Assert.Contains("com.fryworld.circuit.overlay", host.Shell.OpenOverlays);

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task SettingsSchema_ExposesExpectedSettings()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        Assert.NotNull(host.Plugin.SettingsSchema);
        Assert.True(host.Plugin.SettingsSchema.ContainsKey("DefaultPreset"));
        Assert.True(host.Plugin.SettingsSchema.ContainsKey("SimulationSpeed"));
        Assert.True(host.Plugin.SettingsSchema.ContainsKey("ShowCurrentAnnotations"));
        Assert.True(host.Plugin.SettingsSchema.ContainsKey("ShowVoltageAnnotations"));
        Assert.True(host.Plugin.SettingsSchema.ContainsKey("OperatingTemperature"));

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public void MainWindowViewModel_LedSelection_ProvidesColorPresetsAndUpdatesWavelength()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        var led = new FryCircuit.Core.Devices.LedDevice(1, 0, 0, 650.0);
        Assert.NotNull(vm.CurrentCircuit);
        vm.CurrentCircuit.AddDevice(led);
        vm.SelectedDevice = led;

        Assert.True(vm.IsLedSelected);
        Assert.NotNull(vm.SelectedLed);
        Assert.Equal("Red", vm.SelectedLedColorName);
        Assert.Equal("#FF2020", vm.SelectedLedColorHex);
        Assert.Equal(650.0, vm.InspectorValue);

        // Switch to Blue preset via string command parameter
        vm.SetLedColorCommand.Execute("Blue");
        Assert.Equal("Blue", vm.SelectedLedColorName);
        Assert.Equal("#0080FF", vm.SelectedLedColorHex);
        Assert.Equal(470.0, led.Wavelength);
        Assert.Equal(470.0, vm.InspectorValue);

        // Switch to White preset
        vm.SetLedColorCommand.Execute("White");
        Assert.Equal("White", vm.SelectedLedColorName);
        Assert.Equal("#FFFFFF", vm.SelectedLedColorHex);
        Assert.Equal(0.0, led.Wavelength);

        // Continuous tuning via InspectorValue
        vm.InspectorValue = 525.0;
        Assert.Equal(525.0, led.Wavelength);
        Assert.Equal("Green", vm.SelectedLedColorName);
        Assert.Equal("#00E060", vm.SelectedLedColorHex);

        vm.Dispose();
    }
}

