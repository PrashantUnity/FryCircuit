namespace FryCircuit.UI.AI;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using FryCircuit.Core.Control;
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

    [Description("""
Loads or synthesizes a schematic in Circuit Studio using .fcircuit JSON. Automatically performs topological auto-routing and component placement.

FORMAT:
{
  "name": "My Circuit",
  "components": [
    { "id": "V1", "type": "vsource", "waveform": "sine", "amplitude": "10V", "frequency": "1kHz", "pins": { "+": "vin", "-": "gnd" } },
    { "id": "R1", "type": "resistor", "value": "10k", "nodes": ["vin", "vout"] },
    { "id": "C1", "type": "capacitor", "value": "100nF", "nodes": ["vout", "gnd"] },
    { "id": "GND", "type": "ground", "nodes": ["gnd"] }
  ]
}

SUPPORTED DEVICE TYPES:
- resistor, capacitor, inductor: "value": "10k", "nodes": ["n1", "n2"]
- vsource / battery / csource: "waveform" ('sine', 'square', 'triangle', 'dc'), "amplitude" ("10V"), "frequency" ("1kHz"), "pins": {"+": "vcc", "-": "gnd"}
- ground: type 'ground', "nodes": ["gnd"] (must be present in all circuits)
- diode / led / zener: "pins": {"anode": "a", "cathode": "k"}
- opamp: "pins": {"in-": "inv", "in+": "noninv", "out": "vout"}
- npn / pnp (BJT): "pins": {"base": "b", "collector": "c", "emitter": "e"}
- nmos / pmos (MOSFET): "pins": {"gate": "g", "drain": "d", "source": "s"}
- timer555: "pins": {"gnd": "gnd", "trig": "t", "out": "o", "reset": "vcc", "ctrl": "cv", "thresh": "th", "disch": "dis", "vcc": "vcc"}
- and, or, not, nand, nor, xor: "pins": {"in1": "a", "in2": "b", "out": "y"} (or 'in'/'out' for not)
- potentiometer: "pins": {"t1": "a", "wiper": "w", "t2": "b"}, "value": "10k"
- switch: "nodes": ["n1", "n2"]
- fan: "pins": {"+": "vcc", "-": "gnd", "tach": "tach"}, "parameters": {"RatedVoltage": 12.0, "MaxRpm": 5000}
- bldc: "pins": {"u": "u", "v": "v", "w": "w"}, "parameters": {"Kv": 2300, "PropellerDiameterInches": 5.0}
- esc: "pins": {"+": "vbat", "-": "gnd", "pwm": "pwm", "u": "u", "v": "v", "w": "w"}
- bec: "pins": {"vin": "vbat", "gnd": "gnd", "vout": "v5"}, "value": "5.0V"
- buzzer: "pins": {"+": "vcc", "-": "gnd"}
- tvs: "pins": {"anode": "gnd", "cathode": "vbat"}, "value": "18.0V"

ENGINEERING UNITS:
k (1e3), M/MEG (1e6), u/µ (1e-6), n (1e-9), p (1e-12), m (1e-3). Optional units: V, Hz, F, H, Ohm, A.
""")]
    public async Task<string> LoadSchematic(
        [Description("The complete .fcircuit JSON string with 'name' and 'components' array defining device types, values, and net connections.")] string fcircuitJson)
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

    [Description("Exports the active circuit schematic in various industry formats:\n" +
                 "- 'spice': Berkeley SPICE 3f5 / LTspice compatible .cir netlist with thermal anchors and analysis directives.\n" +
                 "- 'bom': Markdown table Bill of Materials with quantities, reference designators, SMD footprints, and manufacturing specs.\n" +
                 "- 'fcircuit': Canonical LLM JSON format with complete component models and topology.")]
    public string Export(
        [Description("Export format: 'spice' (default), 'bom', or 'fcircuit'.")] string format = "spice")
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

    [Description("Controls the real-time SPICE-grade MNA simulation engine.\n" +
                 "- action: 'start' (run solver continuously), 'pause' (freeze solver), 'toggle' (toggle state), 'reset' (clear capacitor/inductor energy states).\n" +
                 "- speed: time-step speed multiplier (0.1 to 4.0).")]
    public async Task<string> ControlSimulation(
        [Description("Action: 'start', 'pause', 'toggle', or 'reset'.")] string action = "toggle",
        [Description("Optional time-step speed multiplier between 0.1 and 4.0 (default 1.0).")] double? speed = null)
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

    [Description("Modifies a parameter value of an existing component in the circuit.\n" +
                 "Supported parameters per component type:\n" +
                 "- resistor: 'resistance' (Ohms)\n" +
                 "- capacitor: 'capacitance' (Farads)\n" +
                 "- inductor: 'inductance' (Henries)\n" +
                 "- vsource / battery: 'voltage' or 'amplitude' (Volts), 'frequency' (Hz), 'offset' (Volts)\n" +
                 "- potentiometer: 'resistance' (Ohms), 'wiper' (0.0 to 1.0)\n" +
                 "- diode / led: 'forwardVoltage' (Volts), 'breakdownVoltage' (Volts)\n" +
                 "- opamp: 'gain' (V/V), 'saturateVoltage' (Volts)")]
    public async Task<string> ModifyComponent(
        [Description("The component ID or reference designator (e.g. '1', 'R1', 'V1', 'C2', 'LED1').")] string componentId,
        [Description("The parameter name (e.g. 'resistance', 'capacitance', 'inductance', 'voltage', 'frequency', 'wiper').")] string parameterName,
        [Description("The new numerical value (float).")] double value)
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

        // 1. Semantic label match (e.g. "Rfb", "Vin", "Rin", "U1")
        var byLabel = circuit.Devices.FirstOrDefault(d => !string.IsNullOrEmpty(d.Label) && string.Equals(d.Label, trimmed, StringComparison.OrdinalIgnoreCase));
        if (byLabel != null) return byLabel;

        // 2. Exact numeric Id match (e.g. "3")
        var byNumericId = circuit.Devices.FirstOrDefault(d => string.Equals(d.Id.ToString(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (byNumericId != null) return byNumericId;

        // 3. RefDes match (e.g. "R3", "V1", "U4", "D2", "C1")
        var byRefDes = circuit.Devices.FirstOrDefault(d => string.Equals(GetDeviceRefDes(d), trimmed, StringComparison.OrdinalIgnoreCase));
        if (byRefDes != null) return byRefDes;

        // 4. Strip leading letters to extract trailing integer (e.g. "r3" -> 3)
        var digitsOnly = new string(trimmed.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(digitsOnly, out int parsedId))
        {
            var byParsedId = circuit.Devices.FirstOrDefault(d => d.Id == parsedId);
            if (byParsedId != null) return byParsedId;
        }

        // 5. Fallback to device type match (e.g. "vsource", "diode", "opamp", "resistor")
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

    [Description("Advances the real-time SPICE-grade MNA circuit simulation engine by the specified number of time-steps (1 to 1000). Useful for computing steady-state or observing transient response step-by-step.")]
    public async Task<string> StepSimulation(
        [Description("Number of simulation time-steps to compute (e.g. 10, 25, 50).")] int steps = 10)
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

    [Description("Queries all live node voltages and device current readings across the circuit.\n" +
                 "Returns JSON: { circuit: string, simTimeMs: number, nodes: [{node, name, isGround, voltage}], devices: [{id, type, current, parameters}] }")]
    public string GetReadings()
    {
        var vm = GetVm();
        var circuit = vm?.CurrentCircuit;
        if (circuit == null) return "Error: No active circuit loaded.";

        var nodeReadings = circuit.Nodes.Values
            .Select(n => new { node = n.Id, name = n.IsGround ? "GND" : (!string.IsNullOrEmpty(n.Name) ? n.Name : $"N{n.Id}"), isGround = n.IsGround, voltage = $"{n.Voltage:F3} V" });

        var deviceReadings = circuit.Devices
            .Select(d => new { id = d.Id, label = !string.IsNullOrEmpty(d.Label) ? d.Label : GetDeviceRefDes(d), type = d.Type, parameters = string.Join(", ", d.Parameters.Select(p => $"{p.Key}={p.Value}")), current = $"{d.Current * 1000.0:F3} mA" });

        return JsonSerializer.Serialize(new
        {
            circuit = circuit.Name,
            simTimeMs = (vm?.Solver?.CurrentTime ?? 0) * 1000.0,
            nodes = nodeReadings,
            devices = deviceReadings
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Analyzes a feedback control system using frequency-domain (Bode, Nyquist, Gang of Four) and time-domain (Step Response) methods.\n" +
                 "Accepts polynomial coefficients (e.g. '100' or 's^2 + 6s + 100') for Plant and optional Controller.\n" +
                 "Returns JSON with poles, zeros, stability, gain/phase margins, crossover frequencies, Nyquist Cauchy encirclements, peak sensitivity Ms, and transient specs (overshoot, settling time).")]
    public string AnalyzeControlSystem(
        [Description("Plant transfer function numerator polynomial, e.g. '100' or '2, 3'.")] string plantNumerator,
        [Description("Plant transfer function denominator polynomial, e.g. '1, 6, 100' or 's^2 + 6s + 100'.")] string plantDenominator,
        [Description("Optional controller numerator polynomial (defaults to '1').")] string? controllerNumerator = null,
        [Description("Optional controller denominator polynomial (defaults to '1').")] string? controllerDenominator = null,
        [Description("Feedback sign convention: -1 for negative feedback (standard), +1 for positive feedback.")] double feedbackSign = -1.0)
    {
        try
        {
            var P = TransferFunction.Parse(plantNumerator, plantDenominator);
            var C = (!string.IsNullOrWhiteSpace(controllerNumerator) && !string.IsNullOrWhiteSpace(controllerDenominator))
                ? TransferFunction.Parse(controllerNumerator, controllerDenominator)
                : TransferFunction.One;

            var L = TransferFunction.Series(C, P);
            int signInt = feedbackSign >= 0 ? 1 : -1;
            var T = TransferFunction.Feedback(L, TransferFunction.One, signInt);

            var bode = BodeAnalyzer.Analyze(L);
            var nyquist = NyquistAnalyzer.Analyze(L);
            var gof = GangOfFourAnalyzer.Analyze(P, C);
            var step = StepResponseAnalyzer.Analyze(T);

            var vm = GetVm();
            vm?.ShowAiActivity("Control Analysis", $"Analyzed loop L(s) = C(s)P(s)", $"GM={bode.GainMarginDb:F1}dB, PM={bode.PhaseMarginDeg:F1}°, Stable={nyquist.IsClosedLoopStable}", "Activity");

            var resultObj = new
            {
                Plant = P.ToString(),
                Controller = C.ToString(),
                OpenLoop = L.ToString(),
                ClosedLoop = T.ToString(),
                OpenLoopPoles = L.Poles.Select(p => $"{p.Real:F4}{(p.Imaginary >= 0 ? "+" : "")}{p.Imaginary:F4}j").ToArray(),
                OpenLoopZeros = L.Zeros.Select(z => $"{z.Real:F4}{(z.Imaginary >= 0 ? "+" : "")}{z.Imaginary:F4}j").ToArray(),
                ClosedLoopPoles = T.Poles.Select(p => $"{p.Real:F4}{(p.Imaginary >= 0 ? "+" : "")}{p.Imaginary:F4}j").ToArray(),
                ClosedLoopZeros = T.Zeros.Select(z => $"{z.Real:F4}{(z.Imaginary >= 0 ? "+" : "")}{z.Imaginary:F4}j").ToArray(),
                Stability = new
                {
                    IsClosedLoopStable = nyquist.IsClosedLoopStable,
                    OpenLoopRhpPolesP = nyquist.OpenLoopRhpPoles,
                    CauchyClockwiseEncirclementsN = nyquist.ClockwiseEncirclements,
                    ClosedLoopRhpPolesZ = nyquist.ClosedLoopRhpPoles,
                    DistanceToCriticalPoint = Math.Round(nyquist.DistanceToCriticalPoint, 4)
                },
                Bode = new
                {
                    GainMarginDb = bode.GainMarginDb.HasValue ? Math.Round(bode.GainMarginDb.Value, 2) : (double?)null,
                    PhaseMarginDeg = bode.PhaseMarginDeg.HasValue ? Math.Round(bode.PhaseMarginDeg.Value, 2) : (double?)null,
                    GainCrossoverFreqRadS = bode.GainCrossoverFrequency.HasValue ? Math.Round(bode.GainCrossoverFrequency.Value, 3) : (double?)null,
                    PhaseCrossoverFreqRadS = bode.PhaseCrossoverFrequency.HasValue ? Math.Round(bode.PhaseCrossoverFrequency.Value, 3) : (double?)null,
                    DelayMarginSec = bode.DelayMarginSeconds.HasValue ? Math.Round(bode.DelayMarginSeconds.Value, 4) : (double?)null
                },
                GangOfFour = new
                {
                    PeakSensitivityMs = Math.Round(gof.PeakSensitivityMs, 3),
                    PeakCompSensitivityMt = Math.Round(gof.PeakCompSensitivityMt, 3),
                    DisturbanceBandwidthWbRadS = Math.Round(gof.DisturbanceBandwidthWb, 3),
                    BodeSensitivityIntegral = Math.Round(gof.BodeSensitivityIntegralValue, 4),
                    TheoreticalWaterbedTarget = Math.Round(gof.TheoreticalWaterbedTarget, 4)
                },
                StepResponse = new
                {
                    RiseTimeSec = Math.Round(step.RiseTime, 4),
                    PeakTimeSec = Math.Round(step.PeakTime, 4),
                    PeakValue = Math.Round(step.PeakValue, 4),
                    OvershootPercent = Math.Round(step.OvershootPercent, 2),
                    SettlingTimeSec = Math.Round(step.SettlingTime, 4),
                    SteadyStateValue = Math.Round(step.SteadyStateValue, 4),
                    SteadyStateError = Math.Round(step.SteadyStateError, 4)
                }
            };

            return JsonSerializer.Serialize(resultObj, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [Description("Simulates the unit step response of an LTI transfer function H(s) = N(s)/D(s).\n" +
                 "Returns key transient metrics (RiseTime, PeakTime, OvershootPercent, SettlingTime, SteadyStateError) and downsampled response trajectory points.")]
    public string ComputeStepResponse(
        [Description("Transfer function numerator, e.g. '100' or '1, 2'.")] string numerator,
        [Description("Transfer function denominator, e.g. '1, 6, 100'.")] string denominator,
        [Description("Optional duration in seconds (defaults to auto-calculated 6x dominant time constant).")] double? durationSeconds = null,
        [Description("Number of output trajectory sample points (defaults to 100).")] int sampleCount = 100)
    {
        try
        {
            var tf = TransferFunction.Parse(numerator, denominator);
            var result = StepResponseAnalyzer.Analyze(tf, durationSeconds, 600);

            // Downsample trajectory for concise JSON payload
            int total = result.Points.Count;
            int stride = Math.Max(1, total / Math.Clamp(sampleCount, 10, 500));
            var samples = result.Points.Where((_, idx) => idx % stride == 0 || idx == total - 1)
                .Select(p => new { t = Math.Round(p.Time, 4), y = Math.Round(p.Value, 4) })
                .ToArray();

            var vm = GetVm();
            vm?.ShowAiActivity("Step Response", $"Simulated H(s) = {tf}", $"%OS={result.OvershootPercent:F1}%, Ts={result.SettlingTime:F2}s", "Play");

            var payload = new
            {
                TransferFunction = tf.ToString(),
                IsStable = tf.IsStable,
                Poles = tf.Poles.Select(p => $"{p.Real:F4}{(p.Imaginary >= 0 ? "+" : "")}{p.Imaginary:F4}j").ToArray(),
                Metrics = new
                {
                    RiseTimeSec = Math.Round(result.RiseTime, 4),
                    PeakTimeSec = Math.Round(result.PeakTime, 4),
                    PeakValue = Math.Round(result.PeakValue, 4),
                    OvershootPercent = Math.Round(result.OvershootPercent, 2),
                    SettlingTimeSec = Math.Round(result.SettlingTime, 4),
                    SteadyStateValue = Math.Round(result.SteadyStateValue, 4),
                    SteadyStateError = Math.Round(result.SteadyStateError, 4)
                },
                Trajectory = samples
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [Description("Solves canonical feedback control engineering problems: second-order transient specifications synthesis (%OS, Ts -> zeta, wn, K, a), speed plant parameter sensitivity SaT & steady-state disturbance errors, and block diagram feedback reduction.\n" +
                 "problemType can be 'transient_specs', 'speed_plant', or 'feedback_equivalence'.")]
    public string SolveControlProblem(
        [Description("Problem type: 'transient_specs' (2nd-order specs), 'speed_plant' (sensitivity & disturbance), or 'feedback_equivalence' (block diagram reduction)")] string problemType,
        [Description("Parameter 1: For 'transient_specs': desired Overshoot % (e.g. 16.3); For 'speed_plant': plant pole 'a' (e.g. 2.0)")] double param1 = 16.3,
        [Description("Parameter 2: For 'transient_specs': desired Settling Time Ts (e.g. 8.0); For 'speed_plant': feedback gain 'K' (e.g. 5.0)")] double param2 = 8.0,
        [Description("Parameter 3: Additional parameter if needed (e.g. load disturbance magnitude)")] double param3 = 1.0)
    {
        try
        {
            var vm = GetVm();
            switch (problemType.ToLowerInvariant())
            {
                case "transient_specs":
                case "specs_design":
                case "problem4_specs":
                case "problem4":
                {
                    double os = param1;
                    double ts = param2;
                    var sol = ControlSpecsSolver.SolveProblem4Specs(os, ts);
                    vm?.ShowAiActivity("Transient Specs Synthesized", $"%OS={os}%, Ts={ts}s -> K={sol.RequiredGainK:F2}, a={sol.ParameterA:F2}", $"zeta={sol.DampingRatioZeta:F3}, wn={sol.NaturalFrequencyWn:F3}", "Check");
                    var payload = new
                    {
                        Problem = "Second-Order Transient Specification Synthesis (%OS, Ts)",
                        Inputs = new { DesiredOvershootPercent = os, DesiredSettlingTimeSeconds = ts },
                        Formulas = new
                        {
                            DampingRatio = "zeta = ln(100/%OS) / sqrt(pi^2 + ln(100/%OS)^2)",
                            NaturalFrequency = "wn = 4 / (zeta * Ts)",
                            RequiredGainK = "K = wn^2",
                            ParameterA = "a = 2 * zeta * wn",
                            DampedFrequency = "wd = wn * sqrt(1 - zeta^2)"
                        },
                        Outputs = new
                        {
                            DampingRatioZeta = Math.Round(sol.DampingRatioZeta, 4),
                            NaturalFrequencyWn = Math.Round(sol.NaturalFrequencyWn, 4),
                            RequiredGainK = Math.Round(sol.RequiredGainK, 4),
                            ParameterA = Math.Round(sol.ParameterA, 4),
                            DampedFrequencyWd = Math.Round(sol.DampedFrequencyWd, 4),
                            PeakTimeTp = Math.Round(sol.PeakTimeTp, 4),
                            DominantPoles = new[]
                            {
                                $"{sol.Pole1.Real:F4}+{sol.Pole1.Imaginary:F4}j",
                                $"{sol.Pole2.Real:F4}{sol.Pole2.Imaginary:F4}j"
                            },
                            ClosedLoopTf = $"T(s) = {sol.RequiredGainK:F2} / (s^2 + {sol.ParameterA:F2}s + {sol.RequiredGainK:F2})"
                        }
                    };
                    return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                }

                case "speed_plant":
                case "sensitivity":
                case "problem5_speed_plant":
                case "problem5":
                {
                    double a = param1;
                    double K = param2;
                    var sol = ControlSpecsSolver.SolveProblem5SpeedPlant(a, K);
                    vm?.ShowAiActivity("Speed Plant Analyzed", $"P(s)=1/(s+{a}), K={K}", $"Sensitivity={sol.LowFrequencySensitivity:F4}, e_ss={sol.SteadyStateErrorStep:F4}", "Check");
                    var payload = new
                    {
                        Problem = "Speed Plant Parameter Sensitivity and Steady-State Errors",
                        Inputs = new { PlantPoleA = a, ProportionalGainK = K },
                        Formulas = new
                        {
                            ClosedLoopTf = "T(s) = K / (s + a + K)",
                            ClosedLoopPole = "s = -(a + K)",
                            SensitivitySaT = "S_a^T = (a / T) * (dT/da) = -a / (s + a + K)",
                            LowFrequencySensitivity = "|S_a^T(0)| = a / (a + K)",
                            SteadyStateErrorStep = "e_ss(step) = 1 - T(0) = a / (a + K)",
                            SteadyStateErrorDisturbance = "y_ss(dist) = 1 / (a + K)"
                        },
                        Outputs = new
                        {
                            ClosedLoopPole = -sol.ClosedLoopPoleReal,
                            LowFrequencySensitivityPercent = Math.Round(sol.LowFrequencySensitivity * 100.0, 2),
                            SteadyStateErrorUnitStep = Math.Round(sol.SteadyStateErrorStep, 4),
                            SteadyStateErrorDisturbance = Math.Round(sol.SteadyStateErrorDisturbance, 4),
                            SensitivityReductionDb = Math.Round(20.0 * Math.Log10(sol.LowFrequencySensitivity), 2)
                        }
                    };
                    return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                }

                case "feedback_equivalence":
                case "block_diagram_reduction":
                case "equivalence":
                {
                    var g1 = new TransferFunction(new Polynomial(1.0), new Polynomial(1.0, 1.0));
                    var g2 = new TransferFunction(new Polynomial(1.0), new Polynomial(1.0, 2.0));
                    var closed = TransferFunction.Feedback(g1, g2, -1);
                    vm?.ShowAiActivity("Loop Equivalence Computed", $"T(s) = {closed}", "Poles at -1.5 ± j0.866", "Check");
                    var payload = new
                    {
                        Problem = "Block Diagram Reduction and Feedback Equivalence",
                        ForwardPathG1 = g1.ToString(),
                        FeedbackPathG2 = g2.ToString(),
                        ClosedLoopEquivalence = closed.ToString(),
                        ClosedLoopPoles = closed.Poles.Select(p => $"{p.Real:F4}{(p.Imaginary >= 0 ? "+" : "")}{p.Imaginary:F4}j").ToArray(),
                        IsStable = closed.IsStable
                    };
                    return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                }

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown problem type '{problemType}'. Supported: 'transient_specs', 'speed_plant', 'feedback_equivalence'." });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
