using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FryCircuit.UI.AI;
using FryCircuit.UI.ViewModels;
using FryCircuit.UI.Views;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using Microsoft.Extensions.AI;

namespace FryCircuit.Plugin;

/// <summary>
/// FryWorld plugin registration for the Circuit Studio (SPICE-grade schematic CAD and real-time MNA simulator).
/// Contributes a full-viewport studio page, floating shell overlay, Command Palette commands, Status Bar widget, and Ribbon tab.
/// </summary>
public class CircuitPlugin : IFryPlugin
{
    private static MainWindowViewModel? _sharedViewModel;

    public string Id => "com.fryworld.circuit";
    public string Name => "Circuit Studio";
    public Version Version => new(1, 0, 3);

    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema =>
        new Dictionary<string, PluginSettingDefinition>
        {
            ["DefaultPreset"] = new()
            {
                Type = PluginSettingType.Select,
                Label = "Default Circuit Preset",
                Description = "Starting circuit loaded when the studio opens",
                DefaultValue = "Half-Wave Rectifier",
                Options = new()
                {
                    "Half-Wave Rectifier",
                    "Resistor Voltage Divider",
                    "RC Step Response",
                    "LED with Current Limiter",
                    "Inverting Op-Amp",
                    "555 Timer Oscillator",
                    "Transmon Qubit (Quantum JJ)",
                    "Aerospace 3-Phase Drone Inverter"
                }
            },
            ["SimulationSpeed"] = new()
            {
                Type = PluginSettingType.Double,
                Label = "Simulation Speed",
                Description = "Time-step speed multiplier",
                DefaultValue = 1.0,
                MinValue = 0.1,
                MaxValue = 4.0,
                Step = 0.1
            },
            ["ShowCurrentAnnotations"] = new()
            {
                Type = PluginSettingType.Boolean,
                Label = "Show Current Annotations",
                Description = "Display animated current flow direction arrows and branch readings",
                DefaultValue = true
            },
            ["ShowVoltageAnnotations"] = new()
            {
                Type = PluginSettingType.Boolean,
                Label = "Show Voltage Annotations",
                Description = "Display live node voltage badges",
                DefaultValue = true
            },
            ["OperatingTemperature"] = new()
            {
                Type = PluginSettingType.Select,
                Label = "Operating Temperature",
                Description = "Environmental operating temperature physics model",
                DefaultValue = "Room Temp (25 °C)",
                Options = new()
                {
                    "Quantum (15 mK)",
                    "Cryogenic (4.2 K)",
                    "Liquid N2 (77 K)",
                    "Space Shadow (150 K)",
                    "Room Temp (25 °C)",
                    "Aerospace (+125 °C)"
                }
            }
        };

    public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        void NavigateToStudio()
        {
            if (ctx.TryGetService<IShellService>(out var shell))
            {
                shell.NavigateTo(Id);
            }
        }

        void OpenOverlay()
        {
            if (ctx.TryGetService<IShellService>(out var shell))
            {
                shell.ShowOverlay($"{Id}.overlay");
            }
        }

        // Register FryWorld theme contributor and track host theme changes
        if (ctx.TryGetService<FryWorld.Sdk.Theming.IThemeManager>(out var themeManager))
        {
            var contributor = new FryCircuit.UI.Theming.CircuitStudioTokenContributor();
            var sub = themeManager.RegisterTokenContributor(contributor);
            ctx.RegisterDisposable(sub);
        }

        if (ctx.TryGetService<IThemeService>(out var hostThemeService))
        {
            FryCircuit.UI.Theming.CircuitThemeService.Instance.SetTheme(hostThemeService.CurrentTheme);
            hostThemeService.ThemeChanged += t => FryCircuit.UI.Theming.CircuitThemeService.Instance.SetTheme(t);
        }

        MainWindowViewModel GetViewModel()
        {
            if (_sharedViewModel == null)
            {
                _sharedViewModel = MainWindowViewModel.ActiveInstance ?? new MainWindowViewModel(ctx);
                ctx.RegisterDisposable(_sharedViewModel);
            }
            MainWindowViewModel.ActiveInstance = _sharedViewModel;
            return _sharedViewModel;
        }

        CircuitStudioView CreateStudioView()
        {
            var vm = GetViewModel();
            var view = new CircuitStudioView { DataContext = vm };

            if (ctx.TryGetService<FryWorld.Sdk.Theming.IThemeManager>(out var tm))
            {
                tm.TrackResourceRoot(view);
            }
            FryCircuit.UI.Theming.CircuitThemeService.Instance.TrackResourceRoot(view);

            return view;
        }

        // 1. Full-Viewport Navigation Rail Item in FryWorld host
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = Id,
            Title = "Circuit Studio",
            Category = "Engineering & Simulation",
            IconKind = "LightningBolt",
            AccentColor = "#00F0FF",
            Order = 40,
            ViewFactory = () => CreateStudioView()
        });

        // 2. Floating Draggable Shell Overlay
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = $"{Id}.overlay",
            Title = "⚡ Circuit Studio",
            IconKind = "LightningBolt",
            DefaultWidth = 1200,
            DefaultHeight = 780,
            MinWidth = 800,
            MinHeight = 550,
            IsResizable = true,
            AutoOpenOnMount = false,
            ViewFactory = () => CreateStudioView()
        });

        // 3. Command Palette (Ctrl+K / ⌘K)
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.open",
            Title = "Open Circuit Studio",
            Subtitle = "Interactive SPICE-grade analog & digital schematic CAD and real-time MNA simulation",
            Category = "Engineering & Simulation",
            IconKind = "LightningBolt",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+Alt+C" : "Ctrl+Alt+C",
            Command = new RelayCommand(NavigateToStudio)
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.new",
            Title = "New Circuit Schematic",
            Subtitle = "Create a blank .frycircuit schematic in Circuit Studio",
            Category = "Engineering & Simulation",
            IconKind = "FilePlusOutline",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+N" : "Ctrl+N",
            Command = new RelayCommand(NavigateToStudio)
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.file.open",
            Title = "Open Circuit Schematic (.frycircuit)",
            Subtitle = "Open a .frycircuit or .circuit schematic file in Circuit Studio",
            Category = "Engineering & Simulation",
            IconKind = "FolderOpenOutline",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+O" : "Ctrl+O",
            Command = new RelayCommand(NavigateToStudio)
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.overlay",
            Title = "Open Circuit Floating Overlay",
            Subtitle = "Launch draggable picture-in-picture circuit simulator window",
            Category = "Engineering & Simulation",
            IconKind = "DockWindow",
            Command = new RelayCommand(OpenOverlay)
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.ai",
            Title = "AI Circuit Architect (.fcircuit)",
            Subtitle = "Open AI Circuit Deck & Turnkey Prompt Studio for LLM-assisted schematic CAD",
            Category = "Engineering & Simulation",
            IconKind = "AutoFixHigh",
            Command = new RelayCommand(NavigateToStudio)
        });

        // 4. Status Bar Widget (⚡ Circuit)
        ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
        {
            Id = "fryworld.status.circuit",
            Label = "⚡ Circuit",
            Tooltip = "Open Circuit Studio (Interactive MNA Simulation)",
            AlignRight = true,
            Order = 25,
            ClickCommand = new RelayCommand(NavigateToStudio)
        });

        // 5. Dynamic Ribbon Tab
        ctx.RegisterRibbonTab(new RibbonTabDescriptor
        {
            Id = "circuit",
            Header = "Circuit",
            Order = 50,
            Actions = new()
            {
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.new",
                    GroupId = "file",
                    Label = "New",
                    Tooltip = "Create a new blank circuit schematic",
                    IconKind = "FilePlusOutline",
                    Order = 1,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.file.open",
                    GroupId = "file",
                    Label = "Open",
                    Tooltip = "Open .fcircuit / .frycircuit file",
                    IconKind = "FolderOpenOutline",
                    Order = 2,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.open",
                    GroupId = "studio",
                    Label = "Circuit Studio",
                    Tooltip = "Open Full-Viewport Circuit Studio",
                    IconKind = "LightningBolt",
                    Order = 10,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.overlay",
                    GroupId = "studio",
                    Label = "Floating Window",
                    Tooltip = "Open Circuit Studio as a Floating Window",
                    IconKind = "DockWindow",
                    Order = 20,
                    Command = new RelayCommand(OpenOverlay)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.ai",
                    GroupId = "ai",
                    Label = "AI Deck",
                    Tooltip = "Open AI Circuit Assistant & .fcircuit JSON Deck",
                    IconKind = "AutoFixHigh",
                    Order = 30,
                    Command = new RelayCommand(NavigateToStudio)
                }
            }
        });

        // 6. Scoped Keyboard Shortcuts (Active when Circuit Studio is open)
        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.tool.wire",
            Description = "Switch to Wire Placement Tool",
            KeyGesture = "W",
            Scope = Id,
            Category = "Tools"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.tool.select",
            Description = "Switch to Selection Tool",
            KeyGesture = "V",
            Scope = Id,
            Category = "Tools"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.tool.cut",
            Description = "Switch to Wire Cut Tool",
            KeyGesture = "X",
            Scope = Id,
            Category = "Tools"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.device.rotate",
            Description = "Rotate Selected Component",
            KeyGesture = "R",
            Scope = Id,
            Category = "Edit"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.view.zoomToFit",
            Description = "Zoom Canvas to Fit All Components",
            KeyGesture = "F",
            Scope = Id,
            Category = "View"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.edit.delete",
            Description = "Delete Selected Device or Wire",
            KeyGesture = "Delete",
            Scope = Id,
            Category = "Edit"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.edit.undo",
            Description = "Undo Last Action",
            KeyGesture = "Mod+Z",
            Scope = Id,
            Category = "Edit"
        });

        ctx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "circuit.edit.redo",
            Description = "Redo Last Action",
            KeyGesture = "Mod+Shift+Z",
            Scope = Id,
            Category = "Edit"
        });

        // 7. Context-Aware AI Subsystem Integration
        var aiContextProvider = new CircuitAiContextProvider(() => GetViewModel());
        ctx.RegisterAiContextProvider(aiContextProvider);

        // 8. Circuit Tools for In-App AI Copilot and External MCP Agents
        var circuitTools = new CircuitAiTools(() => GetViewModel(), NavigateToStudio);

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_get_schematic",
            Description = "Returns the complete schematic currently open in Circuit Studio formatted as .fcircuit JSON with topology, components, values, and netlists.",
            Category = "Circuit Engineering",
            Function = AIFunctionFactory.Create(circuitTools.GetSchematic, "circuit_get_schematic", "Returns the complete schematic currently open in Circuit Studio formatted as .fcircuit JSON with topology, components, values, and netlists.")
        });

        const string loadSchematicDesc = """
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

ENGINEERING UNITS:
k (1e3), M/MEG (1e6), u/µ (1e-6), n (1e-9), p (1e-12), m (1e-3). Optional units: V, Hz, F, H, Ohm, A.
""";

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_load_schematic",
            Description = loadSchematicDesc,
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.LoadSchematic, "circuit_load_schematic", loadSchematicDesc)
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_step_simulation",
            Description = "Advances the real-time SPICE-grade MNA circuit simulation engine by the specified number of time-steps (1 to 1000). Useful for computing steady-state or observing transient response step-by-step.",
            Category = "Circuit Engineering",
            Function = AIFunctionFactory.Create(circuitTools.StepSimulation, "circuit_step_simulation", "Advances the real-time SPICE-grade MNA circuit simulation engine by the specified number of steps.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_get_readings",
            Description = "Queries all live node voltages and device current readings across the circuit. Returns JSON: { circuit: string, simTimeMs: number, nodes: [{node, name, isGround, voltage}], devices: [{id, type, current, parameters}] }.",
            Category = "Circuit Engineering",
            Function = AIFunctionFactory.Create(circuitTools.GetReadings, "circuit_get_readings", "Queries live node voltages and branch current readings across all components in the active circuit.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_new_schematic",
            Description = "Creates a new blank schematic canvas in Circuit Studio with a default ground node.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.NewSchematic, "circuit_new_schematic", "Creates a new blank circuit schematic canvas.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_open_file",
            Description = "Opens and loads a circuit schematic file (.fcircuit JSON or .frycircuit binary) from local disk into Circuit Studio.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.OpenFile, "circuit_open_file", "Opens a schematic file from disk.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_save_file",
            Description = "Saves the active circuit schematic to a file (.fcircuit JSON or .frycircuit binary) on disk.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.SaveFile, "circuit_save_file", "Saves the active schematic to disk.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_export",
            Description = "Exports the active schematic to industry formats: 'spice' netlist (.cir), 'bom' markdown, or 'fcircuit' JSON.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.Export, "circuit_export", "Exports the active schematic.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_list_presets",
            Description = "Lists all 21 built-in pure C# engineering circuit presets.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.ListPresets, "circuit_list_presets", "Lists all built-in circuit presets.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_load_preset",
            Description = "Loads one of the built-in pure C# engineering circuit presets directly into Circuit Studio by name.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.LoadPreset, "circuit_load_preset", "Loads a built-in circuit preset by name.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_set_tool_mode",
            Description = "Sets the active interactive canvas tool mode in Circuit Studio ('Select', 'Wire', 'Cut', 'Pan', 'ProbeCh1', 'ProbeCh2').",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.SetToolMode, "circuit_set_tool_mode", "Sets the active canvas tool mode.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_control_simulation",
            Description = "Controls the real-time SPICE-grade MNA simulation: 'start', 'pause', 'toggle', 'reset', or sets time speed multiplier (0.1 to 4.0).",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.ControlSimulation, "circuit_control_simulation", "Controls the real-time circuit simulation engine.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_zoom_to_fit",
            Description = "Automatically centers and scales the canvas viewport to fit all schematic components.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.ZoomToFit, "circuit_zoom_to_fit", "Centers and fits the canvas viewport.")
        });

        const string modifyComponentDesc = """
Modifies a parameter value of an existing component in the active circuit.
Supported parameters:
- resistor: 'resistance' (Ohms)
- capacitor: 'capacitance' (Farads)
- inductor: 'inductance' (Henries)
- vsource / battery: 'voltage' or 'amplitude' (Volts), 'frequency' (Hz), 'offset' (Volts)
- potentiometer: 'resistance' (Ohms), 'wiper' (0.0 to 1.0)
- diode / led: 'forwardVoltage' (Volts), 'breakdownVoltage' (Volts)
- opamp: 'gain' (V/V), 'saturateVoltage' (Volts)
""";

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_modify_component",
            Description = modifyComponentDesc,
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.ModifyComponent, "circuit_modify_component", modifyComponentDesc)
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_undo",
            Description = "Undoes the last canvas schematic editing action.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.Undo, "circuit_undo", "Undoes the last canvas edit.")
        });

        ctx.RegisterAiTool(new AiToolDescriptor
        {
            Name = "circuit_redo",
            Description = "Redoes the last undone canvas schematic editing action.",
            Category = "Circuit Engineering",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(circuitTools.Redo, "circuit_redo", "Redoes the last undone canvas edit.")
        });

        // 9. Turnkey Engineering Prompts
        ctx.RegisterAiPrompt(new AiPromptDescriptor
        {
            Id = "circuit.design_opamp",
            Title = "Design Inverting Op-Amp",
            Description = "Design an active inverting operational amplifier with specific gain and rails.",
            Category = "Circuit Engineering",
            Template = "Design an inverting op-amp circuit using the .fcircuit schema with a gain of -10, input resistor of 10kΩ, feedback resistor of 100kΩ, and dual ±15V supply rails. Ensure all ground nodes and pins are connected correctly."
        });

        ctx.RegisterAiPrompt(new AiPromptDescriptor
        {
            Id = "circuit.design_lowpass",
            Title = "Design Butterworth Low-Pass Filter",
            Description = "Calculate and generate a 2nd-order Sallen-Key low-pass RC filter.",
            Category = "Circuit Engineering",
            Template = "Generate a 2nd-order active low-pass Sallen-Key RC filter circuit with a cutoff frequency fc = 1 kHz and Q = 0.707 (Butterworth response). Return valid .fcircuit JSON and explain the component choices."
        });

        ctx.RegisterAiPrompt(new AiPromptDescriptor
        {
            Id = "circuit.analyze_simulation",
            Title = "Analyze Simulation Readings",
            Description = "Inspect current operating point, node voltages, and branch currents to diagnose errors or power dissipation.",
            Category = "Circuit Engineering",
            Template = "Analyze the current circuit schematic and live simulation readings. Verify if any component exceeds typical voltage/current ratings, check if the circuit is in steady state, and suggest optimizations."
        });

        return Task.CompletedTask;
    }
}
