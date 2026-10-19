namespace FryCircuit.UI.AI;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.IO;
using FryCircuit.Core.Models;
using FryCircuit.Core.Presets;
using FryCircuit.UI.Controls;
using FryCircuit.UI.ViewModels;

/// <summary>
/// Autonomous and interactive circuit tools exposed to the in-app AI copilot and external MCP agents.
/// Covers full-lifecycle CAD engineering: schematic creation, loading, editing, simulation, telemetry, and file export.
/// </summary>
public class CircuitAiTools
{
    private readonly Func<MainWindowViewModel?> _getViewModel;
    private readonly Action? _navigateToStudio;

    public CircuitAiTools(Func<MainWindowViewModel?> getViewModel, Action? navigateToStudio = null)
    {
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
        _navigateToStudio = navigateToStudio;
    }

    private MainWindowViewModel? GetVm()
    {
        return MainWindowViewModel.ActiveInstance ?? _getViewModel();
    }

    private static async Task RunOnUiAsync(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    [Description("Returns the complete schematic currently open in Circuit Studio formatted as .fcircuit JSON.")]
    public string GetSchematic()
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";
        return vm.ExportCircuitToLlmJson();
    }

    [Description("Loads or replaces the schematic in Circuit Studio using a .fcircuit JSON string. Automatically performs topological layout and synthesizes wiring.")]
    public async Task<string> LoadSchematic([Description("The complete .fcircuit JSON document.")] string fcircuitJson)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        string result = string.Empty;
        await RunOnUiAsync(() =>
        {
            try
            {
                _navigateToStudio?.Invoke();
                vm = GetVm() ?? vm;
                vm.LoadCircuitFromLlmJson(fcircuitJson);
                result = $"Successfully loaded schematic '{vm.CurrentCircuit?.Name}' with {vm.CurrentCircuit?.Devices.Count} components.";
                vm.ShowAiActivity("AI Schematic Loaded", $"Loaded '{vm.CurrentCircuit?.Name}' ({vm.CurrentCircuit?.Devices.Count} components)", "Auto-routed netlists & placed components", "AutoFixHigh");
            }
            catch (Exception ex)
            {
                result = $"Failed to parse/load .fcircuit JSON: {ex.Message}";
                vm.ShowAiActivity("Schematic Error", $"Failed to load .fcircuit JSON: {ex.Message}", null, "AlertCircle");
            }
        });

        return result;
    }

    [Description("Creates a new blank circuit schematic canvas in Circuit Studio with a default ground reference.")]
    public async Task<string> NewSchematic()
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        await RunOnUiAsync(() =>
        {
            _navigateToStudio?.Invoke();
            vm = GetVm() ?? vm;
            vm.NewCircuit();
            vm.ShowAiActivity("New Schematic", "Created blank circuit canvas", "Default ground reference node placed", "FilePlusOutline");
        });

        return "Created new blank schematic canvas.";
    }

    [Description("Opens and loads a circuit schematic file (.fcircuit JSON or .frycircuit binary) from local disk into the canvas.")]
    public async Task<string> OpenFile([Description("Absolute file path to the .fcircuit or .frycircuit file.")] string filePath)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        if (!File.Exists(filePath)) return $"Error: File '{filePath}' does not exist.";

        string result = string.Empty;
        await RunOnUiAsync(() =>
        {
            try
            {
                _navigateToStudio?.Invoke();
                vm = GetVm() ?? vm;
                vm.OpenCircuit(filePath);
                result = $"Successfully opened '{Path.GetFileName(filePath)}' ({vm.CurrentCircuit?.Devices.Count ?? 0} components).";
                vm.ShowAiActivity("Opened File", $"Loaded '{Path.GetFileName(filePath)}' ({vm.CurrentCircuit?.Devices.Count ?? 0} components)", filePath, "FolderOpenOutline");
            }
            catch (Exception ex)
            {
                result = $"Failed to open file: {ex.Message}";
                vm.ShowAiActivity("Open Error", ex.Message, null, "AlertCircle");
            }
        });

        return result;
    }

    [Description("Saves the active circuit schematic to a file (.fcircuit JSON or .frycircuit binary) on disk.")]
    public async Task<string> SaveFile([Description("Optional destination file path. If omitted, saves to the current file path.")] string? filePath = null)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";
        if (vm.CurrentCircuit == null) return "Error: No active circuit to save.";

        string target = !string.IsNullOrWhiteSpace(filePath)
            ? filePath
            : vm.CurrentFilePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", $"{vm.CurrentCircuit.Name.Replace(" ", "_").ToLowerInvariant()}.fcircuit");

        string result = string.Empty;
        await RunOnUiAsync(() =>
        {
            try
            {
                vm = GetVm() ?? vm;
                vm.SaveCircuit(target);
                var name = vm.CurrentCircuit?.Name ?? "circuit";
                result = $"Successfully saved circuit '{name}' to '{target}'.";
                vm.ShowAiActivity("Saved File", $"Saved '{name}' to disk", Path.GetFileName(target), "ContentSaveOutline");
            }
            catch (Exception ex)
            {
                result = $"Failed to save circuit: {ex.Message}";
                vm.ShowAiActivity("Save Error", ex.Message, null, "AlertCircle");
            }
        });

        return result;
    }

    [Description("Exports the active circuit schematic in various industry formats ('spice' netlist, 'bom' markdown, or 'fcircuit' JSON).")]
    public string Export([Description("Export format: 'spice', 'bom', or 'fcircuit'. Defaults to 'spice'.")] string format = "spice")
    {
        var vm = GetVm();
        if (vm == null || vm.CurrentCircuit == null) return "Error: No active circuit to export.";

        return format.Trim().ToLowerInvariant() switch
        {
            "spice" or "cir" or "netlist" => SpiceNetlistEngine.ExportSpice(vm.CurrentCircuit, vm.OperatingTemperatureKelvin),
            "bom" or "billofmaterials" => BomEngine.GenerateMarkdownBom(vm.CurrentCircuit, vm.OperatingTemperatureKelvin),
            "fcircuit" or "json" => LlmCircuitSerializer.Serialize(vm.CurrentCircuit),
            _ => $"Unsupported format '{format}'. Supported formats: 'spice', 'bom', 'fcircuit'."
        };
    }

    [Description("Lists all built-in pure C# engineering circuit presets (e.g. 'Half-Wave Rectifier', 'Inverting Op-Amp', '555 Timer Oscillator').")]
    public string ListPresets()
    {
        return JsonSerializer.Serialize(CircuitPresets.PresetNames, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Loads one of the built-in pure C# engineering presets directly into Circuit Studio.")]
    public async Task<string> LoadPreset([Description("The exact name of the preset to load (e.g. 'Inverting Op-Amp', 'RC Step Response', '555 Timer Oscillator').")] string presetName)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        string result = string.Empty;
        await RunOnUiAsync(() =>
        {
            try
            {
                _navigateToStudio?.Invoke();
                vm = GetVm() ?? vm;
                vm.LoadPreset(presetName);
                result = $"Successfully loaded preset '{presetName}'.";
                vm.ShowAiActivity("Loaded Preset", $"Loaded '{presetName}' ({vm.CurrentCircuit?.Devices.Count} components)", "SPICE MNA solver re-initialized", "LightningBolt");
            }
            catch (Exception ex)
            {
                result = $"Error loading preset '{presetName}': {ex.Message}";
                vm.ShowAiActivity("Preset Error", ex.Message, null, "AlertCircle");
            }
        });

        return result;
    }

    [Description("Sets the active interactive canvas tool mode in Circuit Studio ('Select', 'Wire', 'Cut', 'Pan', 'ProbeCh1', 'ProbeCh2').")]
    public async Task<string> SetToolMode([Description("Tool mode: 'Select', 'Wire', 'Cut', 'Pan', 'ProbeCh1', 'ProbeCh2'.")] string mode)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        if (!Enum.TryParse<CircuitToolMode>(mode, true, out var toolMode))
        {
            return $"Invalid tool mode '{mode}'. Valid modes: Select, Wire, Cut, Pan, ProbeCh1, ProbeCh2.";
        }

        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            vm.ActiveToolMode = toolMode;
            vm.ShowAiActivity("Tool Mode", $"Switched active canvas tool to '{toolMode}'", null, "CursorDefaultOutline");
        });

        return $"Active tool mode set to {toolMode}.";
    }

    [Description("Controls the real-time SPICE-grade MNA simulation: 'start', 'pause', 'toggle', 'reset', or sets time speed multiplier (0.1 to 4.0).")]
    public async Task<string> ControlSimulation(
        [Description("Action: 'start', 'pause', 'toggle', or 'reset'.")] string action = "toggle",
        [Description("Optional time-step speed multiplier (e.g. 1.0, 2.0).")] double? speed = null)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        string status = string.Empty;
        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            if (speed.HasValue)
            {
                vm.SimulationSpeed = Math.Clamp(speed.Value, 0.1, 4.0);
            }

            switch (action.Trim().ToLowerInvariant())
            {
                case "start" or "run" or "play":
                    vm.IsSimulating = true;
                    status = $"Simulation running at {vm.SimulationSpeed:F1}x speed.";
                    break;
                case "pause" or "stop":
                    vm.IsSimulating = false;
                    status = "Simulation paused.";
                    break;
                case "toggle":
                    vm.IsSimulating = !vm.IsSimulating;
                    status = vm.IsSimulating ? $"Simulation resumed at {vm.SimulationSpeed:F1}x speed." : "Simulation paused.";
                    break;
                case "reset":
                    vm.ResetSimulation();
                    status = "Simulation reset to 0.0 ms.";
                    break;
                default:
                    status = $"Simulation is {(vm.IsSimulating ? "running" : "paused")} at {vm.SimulationSpeed:F1}x speed.";
                    break;
            }

            vm.ShowAiActivity("Simulation Control", status, $"Engine running at {vm.SimulationSpeed:F1}x speed", vm.IsSimulating ? "Play" : "Pause");
        });

        return status;
    }

    [Description("Automatically centers and scales the canvas viewport to fit all schematic components.")]
    public async Task<string> ZoomToFit()
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            vm.ZoomToFitCommand.Execute(null);
            vm.ShowAiActivity("Canvas Centered", "Fitted canvas viewport to all schematic components", null, "FitToPageOutline");
        });

        return "Canvas viewport centered and fitted to circuit.";
    }

    [Description("Modifies a parameter value of an existing component in the circuit (e.g. resistance of 'r1', frequency of 'vsrc').")]
    public async Task<string> ModifyComponent(
        [Description("The component ID or reference (e.g. '1', 'r1', 'vsrc', 'led1').")] string componentId,
        [Description("The parameter name to modify (e.g. 'resistance', 'capacitance', 'voltage', 'frequency').")] string parameterName,
        [Description("The new numerical value for the parameter.")] double value)
    {
        var vm = GetVm();
        if (vm == null || vm.CurrentCircuit == null) return "Error: No active circuit loaded.";

        string result = string.Empty;
        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            var circuit = vm.CurrentCircuit;
            if (circuit == null)
            {
                result = "No active circuit loaded.";
                return;
            }
            var device = FindDevice(circuit, componentId);

            if (device == null)
            {
                result = $"Device '{componentId}' not found in active circuit.";
                vm.ShowAiActivity("Modify Error", result, null, "AlertCircle");
                return;
            }

            var desc = DeviceRegistry.GetDescriptor(device);
            var spec = desc?.Parameters.FirstOrDefault(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase) || string.Equals(p.DisplayLabel, parameterName, StringComparison.OrdinalIgnoreCase));

            if (spec != null)
            {
                spec.Setter(device, value);
                vm.ReinitializeSolver();
                if (vm.SelectedDevice == device)
                {
                    vm.SelectedDevice = null;
                    vm.SelectedDevice = device;
                }
                result = $"Updated {device.Type} (ID #{device.Id}) parameter '{spec.DisplayLabel}' to {value}.";
                vm.ShowAiActivity("Modified Parameter", $"Updated {device.Type} #{device.Id}: {spec.DisplayLabel} = {value}", "SPICE solver re-initialized", "Tune");
            }
            else
            {
                device.Parameters[parameterName] = value;
                vm.ReinitializeSolver();
                if (vm.SelectedDevice == device)
                {
                    vm.SelectedDevice = null;
                    vm.SelectedDevice = device;
                }
                result = $"Updated {device.Type} (ID #{device.Id}) raw parameter '{parameterName}' to {value}.";
                vm.ShowAiActivity("Modified Parameter", $"Updated {device.Type} #{device.Id}: {parameterName} = {value}", "SPICE solver re-initialized", "Tune");
            }
        });

        return result;
    }

    private static Device? FindDevice(FryCircuit.Core.Models.Circuit circuit, string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId)) return null;
        var trimmed = componentId.Trim();

        // 1. Exact numeric Id match (e.g. "3")
        var byNumericId = circuit.Devices.FirstOrDefault(d => string.Equals(d.Id.ToString(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (byNumericId != null) return byNumericId;

        // 2. RefDes match (e.g. "R3", "V1", "U4", "D2", "C1")
        var byRefDes = circuit.Devices.FirstOrDefault(d => string.Equals(GetDeviceRefDes(d), trimmed, StringComparison.OrdinalIgnoreCase));
        if (byRefDes != null) return byRefDes;

        // 3. Strip leading letters to extract trailing integer (e.g. "r3" -> 3)
        var digitsOnly = new string(trimmed.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(digitsOnly, out int parsedId))
        {
            var byParsedId = circuit.Devices.FirstOrDefault(d => d.Id == parsedId);
            if (byParsedId != null) return byParsedId;
        }

        // 4. Fallback to device type match (e.g. "vsource", "diode", "opamp", "resistor")
        return circuit.Devices.FirstOrDefault(d => string.Equals(d.Type, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDeviceRefDes(FryCircuit.Core.Models.Device dev)
    {
        char prefix = dev.Type switch
        {
            "resistor" => 'R',
            "capacitor" => 'C',
            "inductor" => 'L',
            "fuse" => 'F',
            "lamp" => 'B',
            "diode" or "led" or "zener" => 'D',
            "vsource" => 'V',
            "csource" => 'I',
            "npn" or "pnp" => 'Q',
            "nmos" or "pmos" => 'M',
            "opamp" or "timer555" => 'U',
            "ground" => 'G',
            _ => 'X'
        };
        return $"{prefix}{dev.Id}";
    }

    [Description("Undoes the last canvas schematic editing action.")]
    public async Task<string> Undo()
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            if (vm.History.CanUndo) vm.History.Undo();
            vm.ShowAiActivity("Undo Action", "Reverted last schematic edit", null, "Undo");
        });

        return "Undo executed.";
    }

    [Description("Redoes the last undone canvas schematic editing action.")]
    public async Task<string> Redo()
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            if (vm.History.CanRedo) vm.History.Redo();
            vm.ShowAiActivity("Redo Action", "Restored previously reverted edit", null, "Redo");
        });

        return "Redo executed.";
    }

    [Description("Advances the real-time SPICE-grade MNA simulation engine by the specified number of steps.")]
    public async Task<string> StepSimulation([Description("Number of simulation time-steps to compute (e.g. 10).")] int steps = 10)
    {
        var vm = GetVm();
        if (vm == null) return "Error: Circuit Studio view model is not active.";

        await RunOnUiAsync(() =>
        {
            vm = GetVm() ?? vm;
            vm.StepSimulation(Math.Clamp(steps, 1, 1000));
            vm.ShowAiActivity("Simulation Stepped", $"Advanced solver by {steps} steps", $"Simulation time: {(vm.Solver?.CurrentTime ?? 0) * 1000.0:F2} ms", "FastForward");
        });

        return $"Simulated {steps} steps. Current simulation time: {(vm.Solver?.CurrentTime ?? 0) * 1000.0:F2} ms.";
    }

    [Description("Queries all live node voltages and device current readings across the circuit.")]
    public string GetReadings()
    {
        var vm = GetVm();
        var circuit = vm?.CurrentCircuit;
        if (circuit == null) return "Error: No active circuit loaded.";

        var nodeReadings = circuit.Nodes.Values
            .Select(n => new { node = n.Id, name = n.IsGround ? "GND" : $"N{n.Id}", isGround = n.IsGround, voltage = $"{n.Voltage:F3} V" });

        var deviceReadings = circuit.Devices
            .Select(d => new { id = d.Id, type = d.Type, parameters = string.Join(", ", d.Parameters.Select(p => $"{p.Key}={p.Value}")), current = $"{d.Current * 1000.0:F3} mA" });

        return JsonSerializer.Serialize(new
        {
            circuit = circuit.Name,
            simTimeMs = (vm?.Solver?.CurrentTime ?? 0) * 1000.0,
            nodes = nodeReadings,
            devices = deviceReadings
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
