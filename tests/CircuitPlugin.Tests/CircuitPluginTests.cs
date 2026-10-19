using System.Linq;
using System.Threading.Tasks;
using FryCircuit.Plugin;
using FryWorld.Sdk.Testing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

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

        var shortcuts = host.Context.GetRegisteredShortcuts();
        Assert.Contains(shortcuts, s => s.Id == "circuit.tool.wire" && s.KeyGesture == "W" && s.Scope == "com.fryworld.circuit");
        Assert.Contains(shortcuts, s => s.Id == "circuit.tool.select" && s.KeyGesture == "V");
        Assert.Contains(shortcuts, s => s.Id == "circuit.device.rotate" && s.KeyGesture == "R");
        Assert.Contains(shortcuts, s => s.Id == "circuit.edit.delete" && s.KeyGesture == "Delete");
        Assert.Contains(shortcuts, s => s.Id == "circuit.edit.undo" && s.KeyGesture == "Mod+Z");
        Assert.Contains(shortcuts, s => s.Id == "circuit.edit.redo" && s.KeyGesture == "Mod+Shift+Z");

        var bindings = host.Context.Shortcuts.GetRegisteredBindings();
        Assert.Contains(bindings, b => b.Id == "circuit.tool.wire" && b.Scope == "com.fryworld.circuit");

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
        Assert.Equal(39, vm.AllPaletteItems.Count);
        Assert.Equal(39, vm.FilteredPaletteItems.Count);

        // Filter by category: Passives
        vm.SelectPaletteCategoryCommand.Execute("Passives");
        Assert.Equal("Passives", vm.SelectedPaletteCategory);
        Assert.Equal(6, vm.FilteredPaletteItems.Count); // resistor, capacitor, inductor, potentiometer, fuse, lamp
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
        Assert.Equal(39, vm.FilteredPaletteItems.Count);

        vm.Dispose();
    }

    [Fact]
    public void MainWindowViewModel_InspectorPanel_CollapsesAndExpands()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        Assert.True(vm.IsInspectorOpen);
        Assert.Equal(315, vm.InspectorColumnWidth.Value);

        // Collapse Inspector
        vm.ToggleInspectorCommand.Execute(null);
        Assert.False(vm.IsInspectorOpen);
        Assert.Equal(0, vm.InspectorColumnWidth.Value);

        // Re-open Inspector
        vm.ToggleInspectorCommand.Execute(null);
        Assert.True(vm.IsInspectorOpen);
        Assert.Equal(315, vm.InspectorColumnWidth.Value);

        // Direct property change also updates column width
        vm.IsInspectorOpen = false;
        Assert.Equal(0, vm.InspectorColumnWidth.Value);

        vm.Dispose();
    }

    [Fact]
    public void MainWindowViewModel_Oscilloscope_CollapsesExpandsAndMaximizes()
    {
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel();
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.False(vm.IsOscilloscopeMaximized);
        Assert.Equal(185, vm.ScopeRowHeight.Value);
        Assert.True(vm.CanvasRowHeight.IsStar);
        Assert.Equal("Maximize", vm.MaximizeOscilloscopeLabel);

        // 1. Collapse Oscilloscope
        vm.ToggleOscilloscopeCommand.Execute(null);
        Assert.False(vm.IsOscilloscopeOpen);
        Assert.Equal(0, vm.ScopeRowHeight.Value);
        Assert.True(vm.CanvasRowHeight.IsStar);

        // 2. Expand Oscilloscope
        vm.ToggleOscilloscopeCommand.Execute(null);
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.False(vm.IsOscilloscopeMaximized);
        Assert.Equal(185, vm.ScopeRowHeight.Value);
        Assert.True(vm.CanvasRowHeight.IsStar);

        // 3. Maximize Oscilloscope
        vm.ToggleMaximizeOscilloscopeCommand.Execute(null);
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.True(vm.IsOscilloscopeMaximized);
        Assert.Equal(0, vm.CanvasRowHeight.Value);
        Assert.True(vm.ScopeRowHeight.IsStar);
        Assert.Equal("Restore", vm.MaximizeOscilloscopeLabel);

        // 4. Restore Oscilloscope from maximized state
        vm.ToggleMaximizeOscilloscopeCommand.Execute(null);
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.False(vm.IsOscilloscopeMaximized);
        Assert.True(vm.CanvasRowHeight.IsStar);
        Assert.Equal(185, vm.ScopeRowHeight.Value);
        Assert.Equal("Maximize", vm.MaximizeOscilloscopeLabel);

        // 5. Maximize, then toggle collapse drops maximize
        vm.ToggleMaximizeOscilloscopeCommand.Execute(null);
        Assert.True(vm.IsOscilloscopeMaximized);
        vm.ToggleOscilloscopeCommand.Execute(null);
        Assert.False(vm.IsOscilloscopeMaximized);
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.Equal(185, vm.ScopeRowHeight.Value);

        vm.Dispose();
    }

    [Fact]
    public async Task CircuitShortcuts_ExecuteCommands_WhenScopeIsActive()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        // Instantiate ViewModel with plugin context (wires IShortcutManager command handlers)
        var vm = new FryCircuit.UI.ViewModels.MainWindowViewModel(host.Context);

        var shortcutManager = host.Context.GetService<FryWorld.Sdk.Shortcuts.IShortcutManager>();
        shortcutManager.SetActiveScope("com.fryworld.circuit");

        // Initially in Select mode
        Assert.Equal(FryCircuit.UI.Controls.CircuitToolMode.Select, vm.ActiveToolMode);

        // Press 'W' -> switches to Wire mode
        var keyArgs = new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.W,
            KeyModifiers = Avalonia.Input.KeyModifiers.None
        };
        bool handled = shortcutManager.ProcessKeyDown(keyArgs);
        Assert.True(handled);
        Assert.True(keyArgs.Handled);
        Assert.Equal(FryCircuit.UI.Controls.CircuitToolMode.Wire, vm.ActiveToolMode);

        // Press 'V' -> switches to Select mode
        var keyArgs2 = new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.V,
            KeyModifiers = Avalonia.Input.KeyModifiers.None
        };
        bool handled2 = shortcutManager.ProcessKeyDown(keyArgs2);
        Assert.True(handled2);
        Assert.Equal(FryCircuit.UI.Controls.CircuitToolMode.Select, vm.ActiveToolMode);

        vm.Dispose();
        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task MountAsync_RegistersAiContextProviderToolsAndPrompts()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        var contextProviders = host.Context.GetRegisteredAiContextProviders();
        Assert.Contains(contextProviders, p => p.PluginId == "com.fryworld.circuit");

        var tools = host.Context.GetRegisteredAiTools();
        Assert.Contains(tools, t => t.Name == "circuit_get_schematic");
        Assert.Contains(tools, t => t.Name == "circuit_load_schematic");
        Assert.Contains(tools, t => t.Name == "circuit_step_simulation");
        Assert.Contains(tools, t => t.Name == "circuit_get_readings");

        var prompts = host.Context.GetRegisteredAiPrompts();
        Assert.Contains(prompts, p => p.Id == "circuit.design_opamp");
        Assert.Contains(prompts, p => p.Id == "circuit.design_lowpass");
        Assert.Contains(prompts, p => p.Id == "circuit.analyze_simulation");

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task AiTools_CanQuerySchematicAndReadings()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        var tools = host.Context.GetRegisteredAiTools();
        var getSchematic = tools.First(t => t.Name == "circuit_get_schematic");
        var result = await getSchematic.Function.InvokeAsync();
        Assert.NotNull(result);

        var getReadings = tools.First(t => t.Name == "circuit_get_readings");
        var readings = await getReadings.Function.InvokeAsync();
        Assert.NotNull(readings);

        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task AiTools_CanLoadAndSimulateCustomCircuit()
    {
        await using var host = await FryPluginTestHost.MountAsync<CircuitPlugin>();

        var tools = host.Context.GetRegisteredAiTools();
        var loadTool = tools.First(t => t.Name == "circuit_load_schematic");
        var stepTool = tools.First(t => t.Name == "circuit_step_simulation");
        var readingsTool = tools.First(t => t.Name == "circuit_get_readings");
        var getSchematicTool = tools.First(t => t.Name == "circuit_get_schematic");

        // 1. Create a custom RC filter circuit via JSON
        var customCircuitJson = """
        {
          "name": "Custom Test Filter",
          "components": [
            { "id": "V1", "type": "vsource", "value": "5V", "parameters": { "waveform": "dc", "amplitude": "5V" }, "pins": { "+": "in", "-": "gnd" } },
            { "id": "R1", "type": "resistor", "value": "1kΩ", "nodes": [ "in", "out" ] },
            { "id": "C1", "type": "capacitor", "value": "100nF", "nodes": [ "out", "gnd" ] },
            { "id": "G1", "type": "ground", "pins": { "gnd": "gnd" } }
          ]
        }
        """;

        var loadArgs = new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
        {
            ["fcircuitJson"] = customCircuitJson
        });

        var loadResult = await loadTool.Function.InvokeAsync(loadArgs);
        Assert.NotNull(loadResult);
        Assert.Contains("Successfully loaded", loadResult.ToString());

        // 2. Step the simulation forward
        var stepArgs = new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
        {
            ["steps"] = 25
        });

        var stepResult = await stepTool.Function.InvokeAsync(stepArgs);
        Assert.NotNull(stepResult);
        Assert.Contains("Simulated 25 steps", stepResult.ToString());

        // 3. Inspect readings
        var readingsResult = await readingsTool.Function.InvokeAsync();
        Assert.NotNull(readingsResult);
        var readingsText = readingsResult.ToString()!;
        Assert.Contains("Custom Test Filter", readingsText);
        Assert.Contains("resistor", readingsText);
        Assert.Contains("capacitor", readingsText);

        // 4. Verify exported schematic matches
        var schematicResult = await getSchematicTool.Function.InvokeAsync();
        Assert.NotNull(schematicResult);
        Assert.Contains("Custom Test Filter", schematicResult.ToString());

        await host.AssertCleanUnmountAsync();
    }
}

