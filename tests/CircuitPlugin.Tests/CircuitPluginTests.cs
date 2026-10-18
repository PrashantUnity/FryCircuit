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
        Assert.Contains(commands, c => c.Id == "cmd.circuit.new");
        Assert.Contains(commands, c => c.Id == "cmd.circuit.file.open");
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
    public async Task NewCommand_NavigatesToCircuitStudio()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        await host.RunCommandAsync("cmd.circuit.new");
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

    [Fact]
    public void MainWindowViewModel_NewCircuit_ResetsStateAndSetsUntitledFrycircuit()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        Assert.NotNull(vm.CurrentCircuit);

        vm.NewCircuitCommand.Execute(null);

        Assert.NotNull(vm.CurrentCircuit);
        Assert.Equal("New Circuit", vm.CurrentCircuit.Name);
        Assert.Null(vm.CurrentFilePath);
        Assert.Equal("Untitled.frycircuit", vm.CurrentFileName);
        Assert.False(vm.IsDirty);
        Assert.Equal("FryCircuit - New Circuit", vm.CircuitTitle);
        Assert.Contains("Created new blank circuit", vm.StatusText);

        vm.Dispose();
    }

    [Fact]
    public void CircuitCanvas_FindDeviceAt_TightHitTestingAllowsAdjacentClicks()
    {
        var canvas = new FryCircuit.UI.Controls.CircuitCanvas();
        var circuit = new FryCircuit.Core.Models.Circuit();
        var resistor = new FryCircuit.Core.Devices.ResistorDevice(1, 10, 10, 1000);
        circuit.AddDevice(resistor);
        canvas.Circuit = circuit;

        // Click directly on resistor center/body (10, 10) -> Hit!
        var hitDirect = canvas.FindDeviceAt(10.0, 10.0);
        Assert.Same(resistor, hitDirect);

        // Click close to resistor zigzag (10.2, 10.0) -> Hit!
        var hitNear = canvas.FindDeviceAt(10.2, 10.0);
        Assert.Same(resistor, hitNear);

        // Click 0.8 units away horizontally (10.8, 10.0) where adjacent wires run -> Miss!
        var hitAway = canvas.FindDeviceAt(10.8, 10.0);
        Assert.Null(hitAway);

        // Click 1.0 unit away horizontally (11.0, 10.0) -> Miss!
        var hitColumn = canvas.FindDeviceAt(11.0, 10.0);
        Assert.Null(hitColumn);
    }

    [Fact]
    public void MainWindowViewModel_LeftPanel_TogglesOpenState()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        Assert.True(vm.IsLeftPanelOpen);

        vm.ToggleLeftPanelCommand.Execute(null);
        Assert.False(vm.IsLeftPanelOpen);

        vm.ToggleLeftPanelCommand.Execute(null);
        Assert.True(vm.IsLeftPanelOpen);

        vm.Dispose();
    }

    [Fact]
    public void MainWindowViewModel_PaletteCatalog_FiltersByCategoryAndSearchQuery()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        Assert.Equal(37, vm.AllPaletteItems.Count);
        Assert.Equal(37, vm.FilteredPaletteItems.Count);

        // Filter by category: Passives
        vm.SelectPaletteCategoryCommand.Execute("Passives");
        Assert.Equal("Passives", vm.SelectedPaletteCategory);
        Assert.Equal(4, vm.FilteredPaletteItems.Count); // resistor, capacitor, inductor, potentiometer
        Assert.All(vm.FilteredPaletteItems, item => Assert.Equal("Passives", item.Category));

        // Filter by search query: "ind" while in Passives
        vm.ComponentSearchQuery = "ind";
        Assert.Single(vm.FilteredPaletteItems);
        Assert.Equal("inductor", vm.FilteredPaletteItems[0].Code);

        // Reset category to "All", search for "gate"
        vm.SelectPaletteCategoryCommand.Execute("All");
        vm.ComponentSearchQuery = "gate";
        Assert.Equal(6, vm.FilteredPaletteItems.Count); // AND, OR, NOT, NAND, NOR, XOR
        Assert.All(vm.FilteredPaletteItems, item => Assert.Equal("Logic & ICs", item.Category));

        // Clear search
        vm.ComponentSearchQuery = "";
        Assert.Equal(37, vm.FilteredPaletteItems.Count);

        vm.Dispose();
    }
}

