using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.History;
using FryCircuit.Core.IO;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using FryCircuit.UI.Controls;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Shortcuts;
using FryCircuit.Core.Control;
using FryCircuit.Core.Devices.Control;
using Material.Icons;

namespace FryCircuit.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// The globally active ViewModel instance connected to the visible UI.
    /// MCP and external AI copilots use this to guarantee updates reflect on the live canvas.
    /// </summary>
    public static MainWindowViewModel? ActiveInstance { get; set; }

    private readonly DispatcherTimer _timer;
    private readonly FryWorld.Sdk.Plugins.IFryPluginContext? _pluginContext;
    private readonly EventHandler? _historyHandler;
    private readonly Action<string>? _settingsHandler;
    private readonly List<IDisposable> _shortcutRegistrations = new();
    private bool _disposed;
    private MnaSolver? _solver;
    public MnaSolver? Solver => _solver;
    private readonly List<CircuitCanvas> _activeCanvases = new();
    private IEnumerable<CircuitCanvas> AllCanvases =>
        _activeCanvases.Concat(CircuitCanvas.ActiveCanvases).Distinct();
    private CircuitCanvas? _canvas => _activeCanvases.LastOrDefault() ?? CircuitCanvas.ActiveCanvases.LastOrDefault();
    private OscilloscopeControl? _oscilloscope;

    private void InvalidateAllCanvases()
    {
        foreach (var c in AllCanvases)
        {
            c.InvalidateVisual();
        }
    }

    public UndoRedoManager History { get; } = new();

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoLabel = "Undo (Ctrl+Z)";

    [ObservableProperty]
    private string _redoLabel = "Redo (Ctrl+Y)";

    [ObservableProperty]
    private int? _probedNodeCh1;

    [ObservableProperty]
    private int? _probedNodeCh2;

    [ObservableProperty]
    private int? _probedNodeCh3;

    [ObservableProperty]
    private int? _probedNodeCh4;

    [ObservableProperty]
    private string _probedCh1Label = "CH1: Auto (Input)";
    partial void OnProbedNodeCh1Changed(int? value) => OnPropertyChanged(nameof(ProbedCh1ShortLabel));
    public string ProbedCh1ShortLabel => ProbedNodeCh1.HasValue ? $"CH1 #{ProbedNodeCh1.Value}" : "CH1";

    [ObservableProperty]
    private string _probedCh2Label = "CH2: Auto (Output)";
    partial void OnProbedNodeCh2Changed(int? value) => OnPropertyChanged(nameof(ProbedCh2ShortLabel));
    public string ProbedCh2ShortLabel => ProbedNodeCh2.HasValue ? $"CH2 #{ProbedNodeCh2.Value}" : "CH2";

    [ObservableProperty]
    private string _probedCh3Label = "CH3: Auto";
    partial void OnProbedNodeCh3Changed(int? value) => OnPropertyChanged(nameof(ProbedCh3ShortLabel));
    public string ProbedCh3ShortLabel => ProbedNodeCh3.HasValue ? $"CH3 #{ProbedNodeCh3.Value}" : "CH3";

    [ObservableProperty]
    private string _probedCh4Label = "CH4: Auto";
    partial void OnProbedNodeCh4Changed(int? value) => OnPropertyChanged(nameof(ProbedCh4ShortLabel));
    public string ProbedCh4ShortLabel => ProbedNodeCh4.HasValue ? $"CH4 #{ProbedNodeCh4.Value}" : "CH4";

    public System.Collections.ObjectModel.ObservableCollection<OscilloscopeChannel> ScopeChannels { get; } = new();

    [ObservableProperty]
    private int _activeProbingChannel = 1;

    [ObservableProperty]
    private Circuit? _currentCircuit;

    private bool _isSwitchingCircuit;

    [ObservableProperty]
    private Device? _selectedDevice;

    [ObservableProperty]
    private CircuitWire? _selectedWire;

    [ObservableProperty]
    private bool _hasSelectedDevice = false;

    [ObservableProperty]
    private bool _hasSelectedWire = false;

    [ObservableProperty]
    private int _selectedWireNodeId = -1;

    [ObservableProperty]
    private double _selectedWireVoltage = 0.0;

    [ObservableProperty]
    private string _formattedSelectedWireVoltage = "0.00 V";

    [ObservableProperty]
    private bool _isSelectedWireGround = false;

    [ObservableProperty]
    private CircuitToolMode _activeToolMode = CircuitToolMode.Select;

    public bool IsSelectMode => ActiveToolMode == CircuitToolMode.Select;
    public bool IsWireMode => ActiveToolMode == CircuitToolMode.Wire;
    public bool IsCutMode => ActiveToolMode == CircuitToolMode.Cut;
    public bool IsProbingCh1 => ActiveToolMode == CircuitToolMode.ProbeCh1 || (ActiveToolMode == CircuitToolMode.Probe && ActiveProbingChannel == 1);
    public bool IsProbingCh2 => ActiveToolMode == CircuitToolMode.ProbeCh2 || (ActiveToolMode == CircuitToolMode.Probe && ActiveProbingChannel == 2);
    public bool IsProbingCh3 => ActiveToolMode == CircuitToolMode.Probe && ActiveProbingChannel == 3;
    public bool IsProbingCh4 => ActiveToolMode == CircuitToolMode.Probe && ActiveProbingChannel == 4;

    partial void OnActiveToolModeChanged(CircuitToolMode value)
    {
        OnPropertyChanged(nameof(IsSelectMode));
        OnPropertyChanged(nameof(IsWireMode));
        OnPropertyChanged(nameof(IsCutMode));
        OnPropertyChanged(nameof(IsProbingCh1));
        OnPropertyChanged(nameof(IsProbingCh2));
        OnPropertyChanged(nameof(IsProbingCh3));
        OnPropertyChanged(nameof(IsProbingCh4));
    }

    [ObservableProperty]
    private bool _isSimulating = true;

    [ObservableProperty]
    private double _simulationSpeed = 1.0;

    [ObservableProperty]
    private string _circuitTitle = "FryCircuit - Rectifier Demo";

    public IStorageProvider? StorageProvider { get; set; }

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _currentFileName = "Untitled.frycircuit";

    partial void OnCurrentFilePathChanged(string? value)
    {
        CurrentFileName = string.IsNullOrWhiteSpace(value) ? "Untitled.frycircuit" : Path.GetFileName(value);
        UpdateTitle();
    }

    partial void OnIsDirtyChanged(bool value)
    {
        UpdateTitle();
    }

    public void UpdateTitle()
    {
        string dirtyIndicator = IsDirty ? " •" : "";
        string name = string.IsNullOrWhiteSpace(CurrentFilePath)
            ? (CurrentCircuit?.Name ?? "Untitled")
            : Path.GetFileName(CurrentFilePath);
        CircuitTitle = $"FryCircuit - {name}{dirtyIndicator}";
    }

    [ObservableProperty]
    private bool _showCurrentAnnotations = true;

    [ObservableProperty]
    private bool _showVoltageAnnotations = true;

    [RelayCommand]
    private void ToggleCurrentAnnotations()
    {
        ShowCurrentAnnotations = !ShowCurrentAnnotations;
        InvalidateAllCanvases();
    }

    [RelayCommand]
    private void ToggleVoltageAnnotations()
    {
        ShowVoltageAnnotations = !ShowVoltageAnnotations;
        InvalidateAllCanvases();
    }

    [ObservableProperty]
    private string _statusText = "Interactive Circuit Builder Ready";

    // Inspector Properties
    [ObservableProperty]
    private double _inspectorValue = 1000.0;

    [ObservableProperty]
    private string _inspectorUnit = "Ω";

    [ObservableProperty]
    private string _inspectorLabel = "Resistance";

    [ObservableProperty]
    private double _inspectorMin = 1.0;

    [ObservableProperty]
    private double _inspectorMax = 1000000.0;

    [ObservableProperty]
    private ObservableCollection<DeviceParameterViewModel> _deviceParameters = new();

    [ObservableProperty]
    private bool _isLeftPanelOpen = true;

    [RelayCommand]
    public void ToggleLeftPanel()
    {
        IsLeftPanelOpen = !IsLeftPanelOpen;
    }

    // Right Inspector Panel State (Collapsible)
    [ObservableProperty]
    private bool _isInspectorOpen = true;

    partial void OnIsInspectorOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(InspectorColumnWidth));
    }

    [RelayCommand]
    public void ToggleInspector()
    {
        IsInspectorOpen = !IsInspectorOpen;
    }

    public GridLength InspectorColumnWidth => IsInspectorOpen 
        ? new GridLength(315, GridUnitType.Pixel) 
        : new GridLength(0, GridUnitType.Pixel);

    // Bottom Oscilloscope Panel State (Collapsible & Maximizable)
    [ObservableProperty]
    private bool _isOscilloscopeOpen = true;

    partial void OnIsOscilloscopeOpenChanged(bool value)
    {
        NotifyScopeLayoutChanged();
    }

    [ObservableProperty]
    private bool _isOscilloscopeMaximized = false;

    partial void OnIsOscilloscopeMaximizedChanged(bool value)
    {
        NotifyScopeLayoutChanged();
    }

    public string MaximizeOscilloscopeTooltip => IsOscilloscopeMaximized ? "Restore Oscilloscope (🗗)" : "Maximize Oscilloscope (🗖)";
    public string MaximizeOscilloscopeIcon => IsOscilloscopeMaximized ? "🗗" : "🗖";
    public string MaximizeOscilloscopeLabel => IsOscilloscopeMaximized ? "Restore" : "Maximize";

    [RelayCommand]
    public void ToggleOscilloscope()
    {
        if (IsOscilloscopeMaximized)
        {
            IsOscilloscopeMaximized = false;
            IsOscilloscopeOpen = true;
        }
        else
        {
            IsOscilloscopeOpen = !IsOscilloscopeOpen;
        }
    }

    [RelayCommand]
    public void ToggleMaximizeOscilloscope()
    {
        if (!IsOscilloscopeOpen)
        {
            IsOscilloscopeOpen = true;
        }
        IsOscilloscopeMaximized = !IsOscilloscopeMaximized;
    }

    private void NotifyScopeLayoutChanged()
    {
        OnPropertyChanged(nameof(CanvasRowHeight));
        OnPropertyChanged(nameof(ScopeRowHeight));
        OnPropertyChanged(nameof(MaximizeOscilloscopeTooltip));
        OnPropertyChanged(nameof(MaximizeOscilloscopeIcon));
        OnPropertyChanged(nameof(MaximizeOscilloscopeLabel));
    }

    public GridLength CanvasRowHeight => IsOscilloscopeMaximized 
        ? new GridLength(0, GridUnitType.Pixel) 
        : new GridLength(1, GridUnitType.Star);

    public GridLength ScopeRowHeight => !IsOscilloscopeOpen 
        ? new GridLength(0, GridUnitType.Pixel) 
        : (IsOscilloscopeMaximized 
            ? new GridLength(1, GridUnitType.Star) 
            : new GridLength(185, GridUnitType.Pixel));

    // Collapsible Secondary CAD Drafting Ribbon State
    [ObservableProperty]
    private bool _isCadRibbonOpen = true;

    partial void OnIsCadRibbonOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(RibbonToggleIconKind));
        OnPropertyChanged(nameof(RibbonToggleTooltip));
    }

    public string RibbonToggleIconKind => IsCadRibbonOpen ? "ChevronUp" : "ChevronDown";
    public string RibbonToggleTooltip => IsCadRibbonOpen ? "Collapse CAD Ribbon (F1)" : "Expand CAD Ribbon (F1)";

    [RelayCommand]
    public void ToggleCadRibbon()
    {
        IsCadRibbonOpen = !IsCadRibbonOpen;
    }

    [ObservableProperty]
    private string _componentSearchQuery = "";

    partial void OnComponentSearchQueryChanged(string value)
    {
        UpdateFilteredPaletteItems();
    }

    [ObservableProperty]
    private string _selectedPaletteCategory = "All";

    public bool IsAllSelected => SelectedPaletteCategory == "All";
    public bool IsPassivesVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Passives";
    public bool IsPassivesSelected => SelectedPaletteCategory == "Passives";
    public bool IsSourcesVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Sources";
    public bool IsSourcesSelected => SelectedPaletteCategory == "Sources";
    public bool IsSemiconductorsVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Semiconductors";
    public bool IsSemiconductorsSelected => SelectedPaletteCategory == "Semiconductors";
    public bool IsQuantumVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Quantum & RF";
    public bool IsQuantumSelected => SelectedPaletteCategory == "Quantum & RF";
    public bool IsLogicVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Logic & ICs";
    public bool IsLogicSelected => SelectedPaletteCategory == "Logic & ICs";
    public bool IsMetersVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Meters & Switches";
    public bool IsMetersSelected => SelectedPaletteCategory == "Meters & Switches";
    public bool IsDronesVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Drones & Aero";
    public bool IsDronesSelected => SelectedPaletteCategory == "Drones & Aero";
    public bool IsControlVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Control Systems";
    public bool IsControlSelected => SelectedPaletteCategory == "Control Systems";

    public IReadOnlyList<PaletteItemViewModel> AllPaletteItems { get; } = new List<PaletteItemViewModel>
    {
        // Passives
        new() { Code = "resistor", Name = "Resistor", Symbol = "R", Category = "Passives", Hotkey = "R", ToolTip = "Passive linear 2-terminal resistor" },
        new() { Code = "capacitor", Name = "Capacitor", Symbol = "C", Category = "Passives", Hotkey = "C", ToolTip = "Passive dual-plate energy storage capacitor" },
        new() { Code = "inductor", Name = "Inductor", Symbol = "L", Category = "Passives", Hotkey = "L", ToolTip = "Passive magnetic flux coil inductor" },
        new() { Code = "potentiometer", Name = "Potentiometer", Symbol = "POT", Category = "Passives", Hotkey = "P", ToolTip = "3-terminal variable voltage divider" },
        new() { Code = "fuse", Name = "Fuse", Symbol = "FUSE", Category = "Passives", Hotkey = "F", ToolTip = "Fast-acting overcurrent fuse that melts open when rated current is exceeded" },
        new() { Code = "lamp", Name = "Lamp Bulb", Symbol = "LAMP", Category = "Passives", Hotkey = "B", ToolTip = "Incandescent filament bulb with thermal radiation and burnout protection" },

        // Sources & Ground
        new() { Code = "vsource", Name = "DC Voltage", Symbol = "V", Category = "Sources", Hotkey = "V", ToolTip = "Constant DC voltage power source" },
        new() { Code = "vsrcsine", Name = "AC Sine Source", Symbol = "∿", Category = "Sources", Hotkey = "S", ToolTip = "Sinusoidal AC alternating voltage generator" },
        new() { Code = "csource", Name = "DC Current", Symbol = "I", Category = "Sources", Hotkey = "I", ToolTip = "Constant ideal current generator" },
        new() { Code = "battery", Name = "LiPo Battery", Symbol = "BAT", Category = "Sources", Hotkey = "BAT", ToolTip = "Lithium-Ion / LiPo Electrochemical Cell with Temperature Derating & SOC" },
        new() { Code = "ground", Name = "Ground (0V)", Symbol = "⏚", Category = "Sources", Hotkey = "G", ToolTip = "Earth ground 0V reference rail" },

        // Drones & Aerospace
        new() { Code = "fan", Name = "Cooling Fan", Symbol = "FAN", Category = "Drones & Aero", Hotkey = "FN", ToolTip = "DC Brushless Cooling Fan / Propeller Blower with Dynamic RPM & Airflow" },
        new() { Code = "bldc", Name = "BLDC Motor", Symbol = "BLDC", Category = "Drones & Aero", Hotkey = "BM", ToolTip = "3-Phase BLDC Drone Propulsion Motor with Back-EMF & Thrust Telemetry" },
        new() { Code = "esc", Name = "Drone ESC", Symbol = "ESC", Category = "Drones & Aero", Hotkey = "ESC", ToolTip = "Electronic Speed Controller Inverter Stage with PWM Throttle Control" },
        new() { Code = "bec", Name = "5V BEC", Symbol = "BEC", Category = "Drones & Aero", Hotkey = "BEC", ToolTip = "Step-Down DC-DC BEC Power Regulator (4S-6S LiPo to 5V Clean Avionics)" },
        new() { Code = "buzzer", Name = "Piezo Buzzer", Symbol = "BUZZ", Category = "Drones & Aero", Hotkey = "BZ", ToolTip = "Piezoelectric Drone Lost-Model Alarm & Battery Telemetry Beeper" },
        new() { Code = "tvs", Name = "TVS Diode", Symbol = "TVS", Category = "Drones & Aero", Hotkey = "TVS", ToolTip = "High-Energy Transient Voltage Clamping Diode for ESC Inductive Spikes" },

        // Semiconductors
        new() { Code = "diode", Name = "Diode", Symbol = "▷|", Category = "Semiconductors", Hotkey = "D", ToolTip = "P-N junction rectifier diode" },
        new() { Code = "zener", Name = "Zener Diode", Symbol = "▷|", Category = "Semiconductors", Hotkey = "Z", ToolTip = "Reverse breakdown voltage regulator diode" },
        new() { Code = "led", Name = "LED", Symbol = "LED", Category = "Semiconductors", Hotkey = "LED", ToolTip = "Optoelectronic light-emitting diode with photon emission" },
        new() { Code = "npn", Name = "NPN BJT", Symbol = "BJT", Category = "Semiconductors", Hotkey = "Q", ToolTip = "Bipolar junction transistor (Ebers-Moll model)" },
        new() { Code = "nmos", Name = "NMOS FET", Symbol = "FET", Category = "Semiconductors", Hotkey = "M", ToolTip = "N-Channel MOSFET (Shichman-Hodges model)" },
        new() { Code = "opamp", Name = "Op-Amp", Symbol = "▷", Category = "Semiconductors", Hotkey = "OA", ToolTip = "Operational amplifier (Differential input, single output)" },

        // Quantum, RF & Aerospace
        new() { Code = "josephson", Name = "Josephson JJ", Symbol = "JJ", Category = "Quantum & RF", Hotkey = "JJ", ToolTip = "Superconducting Josephson Junction (RCSJ Qubit Model)" },
        new() { Code = "squid", Name = "DC SQUID", Symbol = "SQ", Category = "Quantum & RF", Hotkey = "SQ", ToolTip = "Superconducting Quantum Interference Device (Dual-JJ Magnetometer with Magnetic Flux Tuning)" },
        new() { Code = "tunneldiode", Name = "Tunnel Diode", Symbol = "TD", Category = "Quantum & RF", Hotkey = "TD", ToolTip = "Esaki Tunnel Diode with Quantum Band-to-Band Tunneling & NDR" },
        new() { Code = "tline", Name = "T-Line", Symbol = "TL", Category = "Quantum & RF", Hotkey = "TL", ToolTip = "High-Frequency Bergeron Wave Distributed Transmission Line" },
        new() { Code = "cosmicipulse", Name = "Cosmic Pulse", Symbol = "SEU", Category = "Quantum & RF", Hotkey = "CP", ToolTip = "Aerospace Radiation Heavy-Ion Strike SEU Double-Exponential Charge Injection" },
        new() { Code = "hallsensor", Name = "Hall Sensor", Symbol = "HALL", Category = "Quantum & RF", Hotkey = "HS", ToolTip = "4-Terminal Magnetic Hall Effect Transducer for Drone Motor Commutation" },
        new() { Code = "netlabel", Name = "Net Label", Symbol = "NET", Category = "Quantum & RF", Hotkey = "NET", ToolTip = "EDA Net Label / Global Rail Marker" },

        // Logic & ICs
        new() { Code = "and", Name = "AND Gate", Symbol = "&", Category = "Logic & ICs", Hotkey = "AND", ToolTip = "Dual-input Boolean AND logic gate" },
        new() { Code = "or", Name = "OR Gate", Symbol = "≥1", Category = "Logic & ICs", Hotkey = "OR", ToolTip = "Dual-input Boolean OR logic gate" },
        new() { Code = "not", Name = "NOT Gate", Symbol = "!", Category = "Logic & ICs", Hotkey = "NOT", ToolTip = "Single-input Boolean NOT inverter" },
        new() { Code = "nand", Name = "NAND Gate", Symbol = "&̄", Category = "Logic & ICs", Hotkey = "NAND", ToolTip = "Dual-input Boolean NAND logic gate" },
        new() { Code = "nor", Name = "NOR Gate", Symbol = "≥̄1", Category = "Logic & ICs", Hotkey = "NOR", ToolTip = "Dual-input Boolean NOR logic gate" },
        new() { Code = "xor", Name = "XOR Gate", Symbol = "=1", Category = "Logic & ICs", Hotkey = "XOR", ToolTip = "Dual-input Boolean XOR logic gate" },
        new() { Code = "timer555", Name = "555 Timer", Symbol = "555", Category = "Logic & ICs", Hotkey = "555", ToolTip = "Precision analog/digital timing circuit IC" },
        new() { Code = "seg7", Name = "7-Seg Display", Symbol = "7SEG", Category = "Logic & ICs", Hotkey = "7SEG", ToolTip = "7-Segment LED Numeric & Hex Display" },
        new() { Code = "bcd7seg", Name = "7448 Decoder", Symbol = "7448", Category = "Logic & ICs", Hotkey = "7448", ToolTip = "BCD-to-7-Segment Active-High Decoder Driver IC" },
        new() { Code = "counter4", Name = "74193 Counter", Symbol = "74193", Category = "Logic & ICs", Hotkey = "74193", ToolTip = "4-Bit Synchronous Up/Down Binary Counter IC" },

        // Meters & Controls
        new() { Code = "spst", Name = "SPST Switch", Symbol = "SW", Category = "Meters & Switches", Hotkey = "SW", ToolTip = "Single-pole single-throw toggle switch (click to toggle)" },
        new() { Code = "lamp", Name = "Lamp", Symbol = "LAMP", Category = "Meters & Switches", Hotkey = "LAMP", ToolTip = "Thermal tungsten filament dynamic load indicator" },
        new() { Code = "voltmeter", Name = "Voltmeter", Symbol = "[V]", Category = "Meters & Switches", Hotkey = "VM", ToolTip = "High-impedance potential difference meter" },
        new() { Code = "amperemeter", Name = "Ammeter", Symbol = "[A]", Category = "Meters & Switches", Hotkey = "AM", ToolTip = "Series low-resistance branch current meter" },
        new() { Code = "ohmmeter", Name = "Ohmmeter", Symbol = "[Ω]", Category = "Meters & Switches", Hotkey = "OHM", ToolTip = "Active 2-terminal resistance measurement probe" },

        // Control Systems (Feedback Fundamentals & Loop Dynamics)
        new() { Code = "tf", Name = "Transfer Function", Symbol = "G(s)", Category = "Control Systems", Hotkey = "TF", ToolTip = "SISO LTI rational transfer function block G(s) = N(s)/D(s)" },
        new() { Code = "sum", Name = "Summing Junction", Symbol = "Σ", Category = "Control Systems", Hotkey = "SUM", ToolTip = "Multi-input summing/differencing algebraic junction (+/-)" },
        new() { Code = "pid", Name = "PID Controller", Symbol = "PID", Category = "Control Systems", Hotkey = "PID", ToolTip = "Proportional-Integral-Derivative controller block with anti-windup" },
        new() { Code = "gain", Name = "Gain Block", Symbol = "×K", Category = "Control Systems", Hotkey = "K", ToolTip = "Linear scalar scaling gain amplifier block" },
        new() { Code = "step", Name = "Step Source", Symbol = "STEP", Category = "Control Systems", Hotkey = "U(t)", ToolTip = "Heaviside unit step reference command input" },
        new() { Code = "integrator", Name = "Integrator", Symbol = "1/s", Category = "Control Systems", Hotkey = "INT", ToolTip = "Continuous time integrator state block" },
        new() { Code = "derivative", Name = "Derivative", Symbol = "s", Category = "Control Systems", Hotkey = "DER", ToolTip = "Filtered differentiator block with high-frequency rolloff" },
        new() { Code = "saturation", Name = "Saturation", Symbol = "SAT", Category = "Control Systems", Hotkey = "SAT", ToolTip = "Nonlinear actuator amplitude limiter [Min, Max]" },
        new() { Code = "delay", Name = "Time Delay", Symbol = "e^-sτ", Category = "Control Systems", Hotkey = "TAU", ToolTip = "Transport pure time delay buffer" },

        // Universal Control & System Dynamics (MIMO State-Space, Math, Physical Plants & Discrete)
        new() { Code = "statespace", Name = "State-Space", Symbol = "[A,B]", Category = "Control Systems", Hotkey = "SS", ToolTip = "MIMO/SISO continuous dynamical state-space block dx/dt = Ax + Bu, y = Cx + Du" },
        new() { Code = "mult", Name = "Multiplier", Symbol = "A×B", Category = "Control Systems", Hotkey = "MUL", ToolTip = "Analog signal product block y = Scale * (uA * uB)" },
        new() { Code = "div", Name = "Divider", Symbol = "A÷B", Category = "Control Systems", Hotkey = "DIV", ToolTip = "Signal quotient divider block y = Scale * (uA / (uB + ε))" },
        new() { Code = "mathexpr", Name = "Math Function", Symbol = "f(u)", Category = "Control Systems", Hotkey = "FN", ToolTip = "Arbitrary user-defined nonlinear math expression y = f(u)" },
        new() { Code = "dcmotor", Name = "DC Motor Plant", Symbol = "MTR", Category = "Control Systems", Hotkey = "MOT", ToolTip = "Physical DC motor electromechanical actuator plant (Ra, La, J, B, Ke, Kt)" },
        new() { Code = "ramp", Name = "Ramp Source", Symbol = "RAMP", Category = "Control Systems", Hotkey = "RMP", ToolTip = "Continuous linear ramp excitation r(t) = a*t for Type 1/2 tracking error specs" },
        new() { Code = "const", Name = "Constant Setpoint", Symbol = "K₀", Category = "Control Systems", Hotkey = "SET", ToolTip = "Steady constant reference command setpoint / DC bias block" },
        new() { Code = "noise", Name = "Noise Generator", Symbol = "NOISE", Category = "Control Systems", Hotkey = "WGN", ToolTip = "Band-limited Gaussian white noise generator for sensor noise simulation" },
        new() { Code = "pulse", Name = "Pulse Generator", Symbol = "PLS", Category = "Control Systems", Hotkey = "PUL", ToolTip = "Periodic rectangular pulse/square train reference command" },
        new() { Code = "zoh", Name = "Zero-Order Hold", Symbol = "ZOH", Category = "Control Systems", Hotkey = "ZOH", ToolTip = "Discrete-time sample-and-hold block y(t) = u(k*Ts)" },
        new() { Code = "unitdelay", Name = "Unit Delay", Symbol = "z⁻¹", Category = "Control Systems", Hotkey = "Z1", ToolTip = "Discrete-time unit sample delay block y[k] = u[k-1]" },
        new() { Code = "discretetf", Name = "Discrete TF", Symbol = "H(z)", Category = "Control Systems", Hotkey = "HZ", ToolTip = "Discrete-time pulse transfer function difference equation H(z) = B(z)/A(z)" },
        new() { Code = "deadzone", Name = "Dead-Zone", Symbol = "DZ", Category = "Control Systems", Hotkey = "DZ", ToolTip = "Nonlinear actuator deadband threshold limiter [-W, +W]" },
        new() { Code = "relay", Name = "Relay (Hysteresis)", Symbol = "REL", Category = "Control Systems", Hotkey = "RLY", ToolTip = "Bang-bang switching relay with hysteresis threshold band" },
        new() { Code = "ratelimiter", Name = "Rate Limiter", Symbol = "SLEW", Category = "Control Systems", Hotkey = "RT", ToolTip = "Actuator maximum slew-rate velocity limiter |du/dt| <= R" }
    };

    public ObservableCollection<PaletteItemViewModel> FilteredPaletteItems { get; } = new();

    public void UpdateFilteredPaletteItems()
    {
        FilteredPaletteItems.Clear();
        var q = ComponentSearchQuery?.Trim();
        bool hasQuery = !string.IsNullOrEmpty(q);

        foreach (var item in AllPaletteItems)
        {
            if (SelectedPaletteCategory != "All" && item.Category != SelectedPaletteCategory)
                continue;

            if (hasQuery)
            {
                bool matches = item.Name.Contains(q!, StringComparison.OrdinalIgnoreCase)
                    || item.Code.Contains(q!, StringComparison.OrdinalIgnoreCase)
                    || item.Category.Contains(q!, StringComparison.OrdinalIgnoreCase)
                    || item.Hotkey.Contains(q!, StringComparison.OrdinalIgnoreCase)
                    || item.ToolTip.Contains(q!, StringComparison.OrdinalIgnoreCase);

                if (!matches) continue;
            }

            FilteredPaletteItems.Add(item);
        }
    }

    [RelayCommand]
    public void SelectPaletteCategory(string category)
    {
        SelectedPaletteCategory = category;
        UpdateFilteredPaletteItems();
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsPassivesVisible));
        OnPropertyChanged(nameof(IsPassivesSelected));
        OnPropertyChanged(nameof(IsSourcesVisible));
        OnPropertyChanged(nameof(IsSourcesSelected));
        OnPropertyChanged(nameof(IsSemiconductorsVisible));
        OnPropertyChanged(nameof(IsSemiconductorsSelected));
        OnPropertyChanged(nameof(IsQuantumVisible));
        OnPropertyChanged(nameof(IsQuantumSelected));
        OnPropertyChanged(nameof(IsLogicVisible));
        OnPropertyChanged(nameof(IsLogicSelected));
        OnPropertyChanged(nameof(IsMetersVisible));
        OnPropertyChanged(nameof(IsMetersSelected));
        OnPropertyChanged(nameof(IsDronesVisible));
        OnPropertyChanged(nameof(IsDronesSelected));
        OnPropertyChanged(nameof(IsControlVisible));
        OnPropertyChanged(nameof(IsControlSelected));
    }

    [ObservableProperty]
    private string? _selectedPreset = "Half-Wave Rectifier";

    public ObservableCollection<string> AvailablePresets { get; } = new();

    // AI / MCP Activity HUD Snackbar Notification
    [ObservableProperty]
    private bool _isAiActivityVisible;

    [ObservableProperty]
    private string _aiActivityTitle = "AI Agent Action";

    [ObservableProperty]
    private string _aiActivityMessage = "";

    [ObservableProperty]
    private string? _aiActivityDetail;

    public bool HasAiActivityDetail => !string.IsNullOrEmpty(AiActivityDetail);

    partial void OnAiActivityDetailChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAiActivityDetail));
    }

    [ObservableProperty]
    private string _aiActivityIconKind = "AutoFixHigh";

    [ObservableProperty]
    private string _aiActivityTimeText = "Just now";

    private DispatcherTimer? _aiActivityTimer;

    [RelayCommand]
    public void DismissAiActivity()
    {
        IsAiActivityVisible = false;
        _aiActivityTimer?.Stop();
    }

    public void ShowAiActivity(string title, string message, string? detail = null, string iconKind = "AutoFixHigh", int durationMs = 4500)
    {
        void Update()
        {
            AiActivityTitle = title;
            AiActivityMessage = message;
            AiActivityDetail = detail;
            AiActivityIconKind = iconKind;
            AiActivityTimeText = DateTime.Now.ToString("HH:mm:ss");
            IsAiActivityVisible = true;

            _aiActivityTimer?.Stop();
            _aiActivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            _aiActivityTimer.Tick += (s, e) =>
            {
                _aiActivityTimer?.Stop();
                IsAiActivityVisible = false;
            };
            _aiActivityTimer.Start();

            if (_pluginContext != null && _pluginContext.TryGetService<INotificationService>(out var notif))
            {
                notif.ShowToast(message, $"⚡ AI: {title}", iconKind);
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    // Environmental Operating Temperature & Physical Physics
    [ObservableProperty]
    private string _selectedTemperaturePreset = "Room Temp (25 °C)";

    [ObservableProperty]
    private double _operatingTemperatureKelvin = 300.15;

    [ObservableProperty]
    private string _thermalVoltageLabel = "Vt: 25.86 mV";

    public ObservableCollection<string> TemperaturePresets { get; } = new()
    {
        "Quantum (15 mK)",
        "Cryogenic (4.2 K)",
        "Liquid N2 (77 K)",
        "Space Shadow (150 K)",
        "Room Temp (25 °C)",
        "Aerospace (+125 °C)"
    };

    partial void OnSelectedTemperaturePresetChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        OperatingTemperatureKelvin = value switch
        {
            "Quantum (15 mK)" => 0.015,
            "Cryogenic (4.2 K)" => 4.2,
            "Liquid N2 (77 K)" => 77.36,
            "Space Shadow (150 K)" => 150.0,
            "Aerospace (+125 °C)" => 398.15,
            _ => 300.15
        };

        double vt = FryCircuit.Core.Physics.PhysicalConstants.ThermalVoltage(OperatingTemperatureKelvin);
        ThermalVoltageLabel = vt < 0.001 ? $"Vt: {vt * 1e6:F1} µV" : $"Vt: {vt * 1e3:F2} mV";

        if (_solver != null)
        {
            _solver.TemperatureKelvin = OperatingTemperatureKelvin;
        }
        StatusText = $"Physics Engine: {value} (T = {OperatingTemperatureKelvin:F2} K, {ThermalVoltageLabel})";
    }

    public MainWindowViewModel(FryWorld.Sdk.Plugins.IFryPluginContext? context = null)
    {
        ActiveInstance = this;
        _pluginContext = context;

        _historyHandler = (s, e) =>
        {
            CanUndo = History.CanUndo;
            CanRedo = History.CanRedo;
            UndoLabel = History.NextUndoDescription != null ? $"Undo {History.NextUndoDescription}" : "Undo (Ctrl+Z)";
            RedoLabel = History.NextRedoDescription != null ? $"Redo {History.NextRedoDescription}" : "Redo (Ctrl+Y)";
            IsDirty = true;
        };
        History.HistoryChanged += _historyHandler;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // 60 FPS
        };
        _timer.Tick += OnSimulationTick;
        FryCircuit.UI.Theming.CircuitThemeService.Instance.ThemeChanged += OnThemeChanged;

        LoadAvailablePresets();
        UpdateFilteredPaletteItems();

        string initialPreset = "Half-Wave Rectifier";

        if (_pluginContext != null)
        {
            try
            {
                var settings = _pluginContext.GetPluginSettings();
                var preset = settings.Get<string>("DefaultPreset");
                if (!string.IsNullOrEmpty(preset) && AvailablePresets.Contains(preset))
                {
                    initialPreset = preset;
                }

                SimulationSpeed = settings.Get<double>("SimulationSpeed");
                if (SimulationSpeed <= 0) SimulationSpeed = 1.0;

                ShowCurrentAnnotations = settings.Get<bool>("ShowCurrentAnnotations");
                ShowVoltageAnnotations = settings.Get<bool>("ShowVoltageAnnotations");

                var temp = settings.Get<string>("OperatingTemperature");
                if (!string.IsNullOrEmpty(temp) && TemperaturePresets.Contains(temp))
                {
                    SelectedTemperaturePreset = temp;
                }

                _settingsHandler = key =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (key == "SimulationSpeed")
                        {
                            var spd = settings.Get<double>("SimulationSpeed");
                            if (spd > 0) SimulationSpeed = spd;
                        }
                        else if (key == "ShowCurrentAnnotations")
                        {
                            ShowCurrentAnnotations = settings.Get<bool>("ShowCurrentAnnotations");
                        }
                        else if (key == "ShowVoltageAnnotations")
                        {
                            ShowVoltageAnnotations = settings.Get<bool>("ShowVoltageAnnotations");
                        }
                        else if (key == "OperatingTemperature")
                        {
                            var tmp = settings.Get<string>("OperatingTemperature");
                            if (!string.IsNullOrEmpty(tmp) && TemperaturePresets.Contains(tmp))
                            {
                                SelectedTemperaturePreset = tmp;
                            }
                        }
                    });
                };
                settings.Changed += _settingsHandler;
            }
            catch
            {
                // Settings store not available or mocked
            }

            if (_pluginContext.TryGetService<IShortcutManager>(out var shortcutManager))
            {
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.tool.wire", () => SetToolMode("wire")));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.tool.select", () => SetToolMode("select")));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.tool.cut", () => SetToolMode("cut")));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.device.rotate", () => RotateSelected()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.view.zoomToFit", () => ZoomToFit()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.edit.delete", () => DeleteSelected()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.edit.undo", () => Undo()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.edit.redo", () => Redo()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.sim.restart", () => ResetSimulation()));
                _shortcutRegistrations.Add(shortcutManager.RegisterCommandHandler("circuit.sim.toggle", () => ToggleSimulation()));
            }
        }

        ScopeChannels.Add(OscilloscopeChannel.CreateDefault(1));
        ScopeChannels.Add(OscilloscopeChannel.CreateDefault(2));
        ScopeChannels.Add(OscilloscopeChannel.CreateDefault(3));
        ScopeChannels.Add(OscilloscopeChannel.CreateDefault(4));

        LoadPreset(initialPreset);
        _timer.Start();
    }

    public void AttachControls(CircuitCanvas canvas, OscilloscopeControl? oscilloscope)
    {
        ActiveInstance = this;
        if (!_activeCanvases.Contains(canvas))
        {
            _activeCanvases.Add(canvas);
        }
        if (oscilloscope != null)
        {
            _oscilloscope = oscilloscope;
            foreach (var ch in _oscilloscope.Channels)
            {
                if (!ScopeChannels.Any(c => c.ChannelIndex == ch.ChannelIndex))
                {
                    ScopeChannels.Add(ch);
                }
            }
        }

        canvas.HistoryManager = History;
        canvas.ProbedChannels = _oscilloscope?.Channels ?? ScopeChannels;
        canvas.OnNodeProbed = (ch, node) =>
        {
            SetProbedNode(ch, node);
        };

        if (CurrentCircuit != null)
        {
            canvas.Circuit = CurrentCircuit;
            canvas.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            canvas._needsZoomToFit = true;
            canvas.ZoomToFit();
            canvas.InvalidateVisual();
        }
    }

    public void DetachControls(CircuitCanvas canvas)
    {
        _activeCanvases.Remove(canvas);
    }

    public bool IsVoltageSourceSelected => SelectedDevice != null && (SelectedDevice.Type == "vsource" || SelectedDevice.Type == "ac_source" || SelectedDevice.Type == "dc_source");
    public bool IsCurrentSourceSelected => SelectedDevice != null && (SelectedDevice.Type == "csource" || SelectedDevice.Type == "isource");
    public bool IsResistorSelected => SelectedDevice != null && (SelectedDevice.Type == "resistor" || SelectedDevice.Type == "potentiometer");
    public bool IsCapacitorSelected => SelectedDevice != null && SelectedDevice.Type == "capacitor";
    public bool IsInductorSelected => SelectedDevice != null && SelectedDevice.Type == "inductor";
    public bool IsLedSelected => SelectedDevice != null && (SelectedDevice is LedDevice || SelectedDevice.Type == "led");
    public bool IsControlDeviceSelected => SelectedDevice is TransferFunctionDevice ||
                                           SelectedDevice is SummingJunctionDevice ||
                                           SelectedDevice is PidControllerDevice ||
                                           SelectedDevice is GainBlockDevice ||
                                           SelectedDevice is StepSourceDevice ||
                                           SelectedDevice is IntegratorDevice ||
                                           SelectedDevice is DerivativeDevice ||
                                           SelectedDevice is SaturationDevice ||
                                           SelectedDevice is TimeDelayDevice ||
                                           SelectedDevice is RampSourceDevice ||
                                           SelectedDevice is ConstantSourceDevice ||
                                           SelectedDevice is NoiseSourceDevice ||
                                           SelectedDevice is PulseSourceDevice ||
                                           SelectedDevice is MultiplierDevice ||
                                           SelectedDevice is DividerDevice ||
                                           SelectedDevice is MathExpressionDevice ||
                                           SelectedDevice is DeadZoneDevice ||
                                           SelectedDevice is RelayDevice ||
                                           SelectedDevice is RateLimiterDevice ||
                                           SelectedDevice is StateSpaceBlockDevice ||
                                           SelectedDevice is DcMotorPlantDevice ||
                                           SelectedDevice is ZeroOrderHoldDevice ||
                                           SelectedDevice is UnitDelayDevice ||
                                           SelectedDevice is DiscreteTransferFunctionDevice;

    public bool IsTransferFunctionSelected => SelectedDevice is TransferFunctionDevice;
    public bool IsSummingJunctionSelected => SelectedDevice is SummingJunctionDevice;
    public bool IsStateSpaceSelected => SelectedDevice is StateSpaceBlockDevice;
    public bool IsMathExpressionSelected => SelectedDevice is MathExpressionDevice;
    public bool IsDiscreteTfSelected => SelectedDevice is DiscreteTransferFunctionDevice;
    public bool IsDcMotorSelected => SelectedDevice is DcMotorPlantDevice;

    public TransferFunctionDevice? SelectedTransferFunction => SelectedDevice as TransferFunctionDevice;
    public SummingJunctionDevice? SelectedSummingJunction => SelectedDevice as SummingJunctionDevice;
    public StateSpaceBlockDevice? SelectedStateSpace => SelectedDevice as StateSpaceBlockDevice;
    public MathExpressionDevice? SelectedMathExpression => SelectedDevice as MathExpressionDevice;
    public DiscreteTransferFunctionDevice? SelectedDiscreteTf => SelectedDevice as DiscreteTransferFunctionDevice;
    public DcMotorPlantDevice? SelectedDcMotor => SelectedDevice as DcMotorPlantDevice;

    public string SelectedTfNumerator
    {
        get => SelectedTransferFunction?.NumeratorString ?? "1";
        set
        {
            if (SelectedTransferFunction != null && SelectedTransferFunction.NumeratorString != value)
            {
                SelectedTransferFunction.NumeratorString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                OnPropertyChanged(nameof(SelectedTfNumeratorAcademic));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedTfDenominator
    {
        get => SelectedTransferFunction?.DenominatorString ?? "1, 1";
        set
        {
            if (SelectedTransferFunction != null && SelectedTransferFunction.DenominatorString != value)
            {
                SelectedTransferFunction.DenominatorString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                OnPropertyChanged(nameof(SelectedTfDenominatorAcademic));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedTfNumeratorAcademic => SelectedTransferFunction != null
        ? SelectedTransferFunction.CurrentTransferFunction.Numerator.ToAcademicString("s")
        : "1";

    public string SelectedTfDenominatorAcademic => SelectedTransferFunction != null
        ? SelectedTransferFunction.CurrentTransferFunction.Denominator.ToAcademicString("s")
        : "s + 1";

    public string SelectedSumSigns
    {
        get => SelectedSummingJunction?.Signs ?? "+-";
        set
        {
            if (SelectedSummingJunction != null && SelectedSummingJunction.Signs != value)
            {
                SelectedSummingJunction.Signs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedStateSpaceA
    {
        get => SelectedStateSpace?.AString ?? "0, 1; -2, -3";
        set
        {
            if (SelectedStateSpace != null && SelectedStateSpace.AString != value)
            {
                SelectedStateSpace.AString = value;
                SelectedStateSpace.RebuildModel();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedStateSpaceB
    {
        get => SelectedStateSpace?.BString ?? "0; 1";
        set
        {
            if (SelectedStateSpace != null && SelectedStateSpace.BString != value)
            {
                SelectedStateSpace.BString = value;
                SelectedStateSpace.RebuildModel();
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedStateSpaceC
    {
        get => SelectedStateSpace?.CString ?? "1, 0";
        set
        {
            if (SelectedStateSpace != null && SelectedStateSpace.CString != value)
            {
                SelectedStateSpace.CString = value;
                SelectedStateSpace.RebuildModel();
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedStateSpaceD
    {
        get => SelectedStateSpace?.DString ?? "0";
        set
        {
            if (SelectedStateSpace != null && SelectedStateSpace.DString != value)
            {
                SelectedStateSpace.DString = value;
                SelectedStateSpace.RebuildModel();
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedMathExpr
    {
        get => SelectedMathExpression?.Expression ?? "sin(u)";
        set
        {
            if (SelectedMathExpression != null && SelectedMathExpression.Expression != value)
            {
                SelectedMathExpression.Expression = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedDiscreteTfNum
    {
        get => SelectedDiscreteTf?.NumeratorString ?? "0.1, 0.05";
        set
        {
            if (SelectedDiscreteTf != null && SelectedDiscreteTf.NumeratorString != value)
            {
                SelectedDiscreteTf.NumeratorString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public string SelectedDiscreteTfDen
    {
        get => SelectedDiscreteTf?.DenominatorString ?? "1, -0.85";
        set
        {
            if (SelectedDiscreteTf != null && SelectedDiscreteTf.DenominatorString != value)
            {
                SelectedDiscreteTf.DenominatorString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDeviceDisplayName));
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    [RelayCommand]
    public void SetSelectedSumSigns(string signs)
    {
        SelectedSumSigns = signs;
    }

    [RelayCommand]
    public void SetSelectedTfPreset(string preset)
    {
        if (SelectedTransferFunction == null) return;
        switch (preset)
        {
            case "first_order":
                SelectedTfNumerator = "1";
                SelectedTfDenominator = "1, 1";
                break;
            case "second_order_osc":
                SelectedTfNumerator = "100";
                SelectedTfDenominator = "1, 6, 100";
                break;
            case "integrator":
                SelectedTfNumerator = "1";
                SelectedTfDenominator = "1, 0";
                break;
            case "double_integrator":
                SelectedTfNumerator = "1";
                SelectedTfDenominator = "1, 0, 0";
                break;
        }
    }

    public double SelectedDcMotorRa
    {
        get => SelectedDcMotor?.ResistanceRa ?? 1.0;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.ResistanceRa - value) > 1e-6)
            {
                SelectedDcMotor.ResistanceRa = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public double SelectedDcMotorLa
    {
        get => SelectedDcMotor?.InductanceLa ?? 0.005;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.InductanceLa - value) > 1e-6)
            {
                SelectedDcMotor.InductanceLa = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public double SelectedDcMotorKt
    {
        get => SelectedDcMotor?.TorqueConstantKt ?? 0.05;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.TorqueConstantKt - value) > 1e-6)
            {
                SelectedDcMotor.TorqueConstantKt = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public double SelectedDcMotorKe
    {
        get => SelectedDcMotor?.BackEmfConstantKe ?? 0.05;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.BackEmfConstantKe - value) > 1e-6)
            {
                SelectedDcMotor.BackEmfConstantKe = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public double SelectedDcMotorJ
    {
        get => SelectedDcMotor?.RotorInertiaJ ?? 0.001;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.RotorInertiaJ - value) > 1e-6)
            {
                SelectedDcMotor.RotorInertiaJ = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    public double SelectedDcMotorB
    {
        get => SelectedDcMotor?.ViscousDampingB ?? 0.002;
        set
        {
            if (SelectedDcMotor != null && Math.Abs(SelectedDcMotor.ViscousDampingB - value) > 1e-6)
            {
                SelectedDcMotor.ViscousDampingB = value;
                OnPropertyChanged();
                ReinitializeSolver();
                InvalidateAllCanvases();
            }
        }
    }

    [RelayCommand]
    public void SetSelectedMathExprPreset(string expr)
    {
        SelectedMathExpr = expr;
    }

    [RelayCommand]
    public void SetSelectedStateSpacePreset(string preset)
    {
        if (SelectedStateSpace == null) return;
        switch (preset)
        {
            case "second_order_osc":
                SelectedStateSpaceA = "0, 1; -100, -6";
                SelectedStateSpaceB = "0; 100";
                SelectedStateSpaceC = "1, 0";
                SelectedStateSpaceD = "0";
                break;
            case "double_integrator":
                SelectedStateSpaceA = "0, 1; 0, 0";
                SelectedStateSpaceB = "0; 1";
                SelectedStateSpaceC = "1, 0";
                SelectedStateSpaceD = "0";
                break;
            case "first_order_lag":
                SelectedStateSpaceA = "-10";
                SelectedStateSpaceB = "10";
                SelectedStateSpaceC = "1";
                SelectedStateSpaceD = "0";
                break;
        }
    }

    [RelayCommand]
    public void SetSelectedDiscreteTfPreset(string preset)
    {
        if (SelectedDiscreteTf == null) return;
        switch (preset)
        {
            case "first_order_iir":
                SelectedDiscreteTfNum = "0.1";
                SelectedDiscreteTfDen = "1, -0.9";
                break;
            case "accumulator":
                SelectedDiscreteTfNum = "1";
                SelectedDiscreteTfDen = "1, -1";
                break;
            case "moving_average_2":
                SelectedDiscreteTfNum = "0.5, 0.5";
                SelectedDiscreteTfDen = "1";
                break;
        }
    }

    [RelayCommand]
    public void SetSelectedDcMotorPreset(string preset)
    {
        if (SelectedDcMotor == null) return;
        switch (preset)
        {
            case "drone_motor":
                SelectedDcMotorRa = 0.25;
                SelectedDcMotorLa = 0.0001;
                SelectedDcMotorKt = 0.015;
                SelectedDcMotorKe = 0.015;
                SelectedDcMotorJ = 0.00005;
                SelectedDcMotorB = 0.0005;
                break;
            case "small_servo":
                SelectedDcMotorRa = 2.5;
                SelectedDcMotorLa = 0.002;
                SelectedDcMotorKt = 0.08;
                SelectedDcMotorKe = 0.08;
                SelectedDcMotorJ = 0.0005;
                SelectedDcMotorB = 0.001;
                break;
            case "industrial_dc":
                SelectedDcMotorRa = 0.8;
                SelectedDcMotorLa = 0.01;
                SelectedDcMotorKt = 0.25;
                SelectedDcMotorKe = 0.25;
                SelectedDcMotorJ = 0.02;
                SelectedDcMotorB = 0.01;
                break;
        }
    }

    public LedDevice? SelectedLed => SelectedDevice as LedDevice;
    public string SelectedLedColorHex => SelectedLed?.ColorHex ?? "#FF2020";
    public string SelectedLedColorName => SelectedLed?.ColorName ?? "Red";
    public double SelectedLedForwardVoltage => SelectedLed?.ForwardVoltage ?? 1.8;
    public bool ShowPrimaryKnob => HasSelectedDevice && !IsLedSelected && !IsTransferFunctionSelected && !IsSummingJunctionSelected && !IsStateSpaceSelected && !IsMathExpressionSelected && !IsDiscreteTfSelected && !IsDcMotorSelected && DeviceRegistry.GetPrimaryParameter(SelectedDevice!) != null;

    public bool IsRedSelected => string.Equals(SelectedLedColorName, "Red", StringComparison.OrdinalIgnoreCase);
    public bool IsOrangeSelected => string.Equals(SelectedLedColorName, "Orange", StringComparison.OrdinalIgnoreCase);
    public bool IsYellowSelected => string.Equals(SelectedLedColorName, "Yellow", StringComparison.OrdinalIgnoreCase);
    public bool IsGreenSelected => string.Equals(SelectedLedColorName, "Green", StringComparison.OrdinalIgnoreCase);
    public bool IsCyanSelected => string.Equals(SelectedLedColorName, "Cyan", StringComparison.OrdinalIgnoreCase);
    public bool IsBlueSelected => string.Equals(SelectedLedColorName, "Blue", StringComparison.OrdinalIgnoreCase);
    public bool IsVioletSelected => string.Equals(SelectedLedColorName, "Violet", StringComparison.OrdinalIgnoreCase);
    public bool IsWhiteSelected => string.Equals(SelectedLedColorName, "White", StringComparison.OrdinalIgnoreCase);

    private void NotifyLedPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedLed));
        OnPropertyChanged(nameof(SelectedLedColorHex));
        OnPropertyChanged(nameof(SelectedLedColorBrush));
        OnPropertyChanged(nameof(SelectedLedColorName));
        OnPropertyChanged(nameof(SelectedLedForwardVoltage));
        OnPropertyChanged(nameof(IsRedSelected));
        OnPropertyChanged(nameof(IsOrangeSelected));
        OnPropertyChanged(nameof(IsYellowSelected));
        OnPropertyChanged(nameof(IsGreenSelected));
        OnPropertyChanged(nameof(IsCyanSelected));
        OnPropertyChanged(nameof(IsBlueSelected));
        OnPropertyChanged(nameof(IsVioletSelected));
        OnPropertyChanged(nameof(IsWhiteSelected));
    }

    public IBrush SelectedLedColorBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush(Color.Parse(SelectedLedColorHex));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(255, 32, 32));
            }
        }
    }

    public IReadOnlyList<LedColorPresetItem> LedColorPresets { get; } = new List<LedColorPresetItem>
    {
        new("Red", "#FF2020", 650.0, "650nm (1.9V)"),
        new("Orange", "#FF8C00", 610.0, "610nm (2.0V)"),
        new("Yellow", "#FFD700", 585.0, "585nm (2.1V)"),
        new("Green", "#00E060", 525.0, "525nm (2.4V)"),
        new("Cyan", "#00F0FF", 500.0, "500nm (2.5V)"),
        new("Blue", "#0080FF", 470.0, "470nm (2.6V)"),
        new("Violet", "#8B00FF", 405.0, "405nm (3.1V)"),
        new("White", "#FFFFFF", 0.0, "White (3.2V)")
    };

    public string SelectedDeviceDisplayName
    {
        get
        {
            if (SelectedDevice == null) return "No Selection";
            var desc = DeviceRegistry.GetDescriptor(SelectedDevice);
            return desc != null ? desc.DisplayName : SelectedDevice.Type.ToUpperInvariant();
        }
    }

    public string SelectedDeviceCategoryName
    {
        get
        {
            if (SelectedDevice == null) return string.Empty;
            var desc = DeviceRegistry.GetDescriptor(SelectedDevice);
            return desc != null ? desc.Category : string.Empty;
        }
    }

    partial void OnSelectedDeviceChanged(Device? value)
    {
        HasSelectedDevice = value != null;
        if (value != null)
        {
            SelectedWire = null;
            HasSelectedWire = false;
        }

        if (_pluginContext != null && _pluginContext.TryGetService<IShortcutManager>(out var smDev))
        {
            smDev.SetContextKey("circuit.hasSelection", HasSelectedDevice || HasSelectedWire);
        }

        OnPropertyChanged(nameof(HasNoSelection));
        OnPropertyChanged(nameof(SelectedDeviceDisplayName));
        OnPropertyChanged(nameof(SelectedDeviceCategoryName));
        OnPropertyChanged(nameof(ShowPrimaryKnob));
        OnPropertyChanged(nameof(IsVoltageSourceSelected));
        OnPropertyChanged(nameof(IsCurrentSourceSelected));
        OnPropertyChanged(nameof(IsResistorSelected));
        OnPropertyChanged(nameof(IsCapacitorSelected));
        OnPropertyChanged(nameof(IsInductorSelected));
        OnPropertyChanged(nameof(IsLedSelected));
        OnPropertyChanged(nameof(IsControlDeviceSelected));
        OnPropertyChanged(nameof(IsTransferFunctionSelected));
        OnPropertyChanged(nameof(IsSummingJunctionSelected));
        OnPropertyChanged(nameof(IsStateSpaceSelected));
        OnPropertyChanged(nameof(IsMathExpressionSelected));
        OnPropertyChanged(nameof(IsDiscreteTfSelected));
        OnPropertyChanged(nameof(IsDcMotorSelected));
        OnPropertyChanged(nameof(SelectedTransferFunction));
        OnPropertyChanged(nameof(SelectedSummingJunction));
        OnPropertyChanged(nameof(SelectedStateSpace));
        OnPropertyChanged(nameof(SelectedMathExpression));
        OnPropertyChanged(nameof(SelectedDiscreteTf));
        OnPropertyChanged(nameof(SelectedDcMotor));
        OnPropertyChanged(nameof(SelectedTfNumerator));
        OnPropertyChanged(nameof(SelectedTfDenominator));
        OnPropertyChanged(nameof(SelectedTfNumeratorAcademic));
        OnPropertyChanged(nameof(SelectedTfDenominatorAcademic));
        OnPropertyChanged(nameof(SelectedSumSigns));
        OnPropertyChanged(nameof(SelectedStateSpaceA));
        OnPropertyChanged(nameof(SelectedStateSpaceB));
        OnPropertyChanged(nameof(SelectedStateSpaceC));
        OnPropertyChanged(nameof(SelectedStateSpaceD));
        OnPropertyChanged(nameof(SelectedMathExpr));
        OnPropertyChanged(nameof(SelectedDiscreteTfNum));
        OnPropertyChanged(nameof(SelectedDiscreteTfDen));
        OnPropertyChanged(nameof(SelectedDcMotorRa));
        OnPropertyChanged(nameof(SelectedDcMotorLa));
        OnPropertyChanged(nameof(SelectedDcMotorKt));
        OnPropertyChanged(nameof(SelectedDcMotorKe));
        OnPropertyChanged(nameof(SelectedDcMotorJ));
        OnPropertyChanged(nameof(SelectedDcMotorB));
        NotifyLedPropertiesChanged();

        DeviceParameters.Clear();

        if (value == null) return;

        var specs = DeviceRegistry.GetParameters(value);
        foreach (var pSpec in specs)
        {
            DeviceParameters.Add(new DeviceParameterViewModel(value, pSpec, () =>
            {
                InvalidateAllCanvases();
                var primary = DeviceRegistry.GetPrimaryParameter(value);
                if (primary != null && primary.ParameterName == pSpec.ParameterName)
                {
                    InspectorValue = pSpec.Getter(value);
                }
                if (value is LedDevice)
                {
                    NotifyLedPropertiesChanged();
                }
            }));
        }

        var spec = DeviceRegistry.GetPrimaryParameter(value);
        if (spec != null)
        {
            InspectorLabel = spec.DisplayLabel;
            InspectorUnit = spec.Unit;
            InspectorMin = spec.Minimum;
            InspectorMax = spec.Maximum;
            InspectorValue = spec.Getter(value);
        }
        else
        {
            InspectorLabel = "Value";
            InspectorUnit = "";
            InspectorMin = 0.0;
            InspectorMax = 100.0;
            InspectorValue = 0.0;
        }
    }

    public bool HasNoSelection => !HasSelectedDevice && !HasSelectedWire;

    public bool IsSelectedWireProbed => SelectedWire != null && 
        ((ProbedNodeCh1.HasValue && ProbedNodeCh1.Value == SelectedWire.NodeId) || 
         (ProbedNodeCh2.HasValue && ProbedNodeCh2.Value == SelectedWire.NodeId));

    public bool IsSelectedWireProbedCh1 => SelectedWire != null && ProbedNodeCh1.HasValue && ProbedNodeCh1.Value == SelectedWire.NodeId;
    public bool IsSelectedWireProbedCh2 => SelectedWire != null && ProbedNodeCh2.HasValue && ProbedNodeCh2.Value == SelectedWire.NodeId;

    public string SelectedWireProbeButtonText
    {
        get
        {
            if (SelectedWire == null) return "View on Oscilloscope";
            if (IsSelectedWireProbedCh1) return "Probed on CH1 (Click to Detach)";
            if (IsSelectedWireProbedCh2) return "Probed on CH2 (Click to Detach)";
            return "View on Oscilloscope";
        }
    }

    public MaterialIconKind SelectedWireProbeEyeIconKind =>
        IsSelectedWireProbed ? MaterialIconKind.Eye : MaterialIconKind.EyeOutline;

    public string SelectedWireProbeButtonTooltip =>
        IsSelectedWireProbed 
            ? "Signal is currently streaming on the oscilloscope. Click to detach." 
            : "Click to stream this wire's voltage waveform on the oscilloscope.";

    public IBrush SelectedWireProbeAccentBrush
    {
        get
        {
            if (IsSelectedWireProbedCh1) return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
            if (IsSelectedWireProbedCh2) return new SolidColorBrush(Color.FromRgb(6, 182, 212)); // Cyan
            return new SolidColorBrush(Color.FromRgb(248, 250, 252)); // Slate/white
        }
    }

    public string SelectedWireRailTypeText =>
        IsSelectedWireGround ? "Ground (0 V Ref)" : "Active Signal Net";

    public IBrush SelectedWireRailTypeColor =>
        IsSelectedWireGround 
            ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) 
            : new SolidColorBrush(Color.FromRgb(56, 189, 248));

    public string SelectedWireConnectionDescription
    {
        get
        {
            if (SelectedWire == null || CurrentCircuit == null) return "Floating Wire";
            var d1 = CurrentCircuit.Devices.FirstOrDefault(d => d.Id == SelectedWire.FromDeviceId);
            var d2 = CurrentCircuit.Devices.FirstOrDefault(d => d.Id == SelectedWire.ToDeviceId);
            string name1 = d1 != null 
                ? $"{(DeviceRegistry.GetDescriptor(d1)?.DisplayName ?? d1.Type)} (Pin {SelectedWire.FromPinIndex})" 
                : $"Dev #{SelectedWire.FromDeviceId}";
            string name2 = d2 != null 
                ? $"{(DeviceRegistry.GetDescriptor(d2)?.DisplayName ?? d2.Type)} (Pin {SelectedWire.ToPinIndex})" 
                : $"Dev #{SelectedWire.ToDeviceId}";
            return $"{name1} ── {name2}";
        }
    }

    partial void OnSelectedWireChanged(CircuitWire? value)
    {
        HasSelectedWire = value != null;
        if (value != null)
        {
            SelectedDevice = null;
            HasSelectedDevice = false;
            SelectedWireNodeId = value.NodeId;
            UpdateSelectedWireMeasurements();
        }
        else
        {
            SelectedWireNodeId = -1;
            SelectedWireVoltage = 0.0;
            FormattedSelectedWireVoltage = "0.00 V";
            IsSelectedWireGround = false;
        }

        if (_pluginContext != null && _pluginContext.TryGetService<IShortcutManager>(out var smWire))
        {
            smWire.SetContextKey("circuit.hasSelection", HasSelectedDevice || HasSelectedWire);
        }

        NotifySelectedWireProbeProperties();
    }

    private void UpdateSelectedWireMeasurements()
    {
        if (SelectedWire == null || CurrentCircuit == null) return;
        if (CurrentCircuit.Nodes.TryGetValue(SelectedWire.NodeId, out var node))
        {
            SelectedWireVoltage = node.Voltage;
            IsSelectedWireGround = node.IsGround;
            FormattedSelectedWireVoltage = Device.FormatEngineering(node.Voltage, "V");
        }
    }

    [RelayCommand]
    public void ToggleSelectedWireProbe()
    {
        if (SelectedWire == null) return;

        if (IsSelectedWireProbed)
        {
            DetachSelectedWireProbe();
        }
        else
        {
            // Auto-assign: prefer CH1 if not set or auto, otherwise CH2
            if (!ProbedNodeCh1.HasValue || ProbedNodeCh1.Value < 0)
            {
                ProbeSelectedWireCh1();
            }
            else if (!ProbedNodeCh2.HasValue || ProbedNodeCh2.Value < 0)
            {
                ProbeSelectedWireCh2();
            }
            else
            {
                // Default to replacing CH1
                ProbeSelectedWireCh1();
            }
        }
    }

    [RelayCommand]
    public void DetachSelectedWireProbe()
    {
        if (SelectedWire == null) return;
        int nodeId = SelectedWire.NodeId;

        if (ProbedNodeCh1 == nodeId)
        {
            ProbedNodeCh1 = null;
            ProbedCh1Label = "CH1: Auto (Input)";
            foreach (var c in AllCanvases) c.ProbedNodeCh1 = null;
        }
        if (ProbedNodeCh2 == nodeId)
        {
            ProbedNodeCh2 = null;
            ProbedCh2Label = "CH2: Auto (Output)";
            foreach (var c in AllCanvases) c.ProbedNodeCh2 = null;
        }

        StatusText = $"Detached probe from Node #{nodeId}";
        NotifySelectedWireProbeProperties();
        InvalidateAllCanvases();
    }

    [RelayCommand]
    public void ProbeSelectedWireCh1()
    {
        if (SelectedWire == null) return;
        ProbedNodeCh1 = SelectedWire.NodeId;
        ProbedCh1Label = $"CH1: Net #{SelectedWire.NodeId}";
        foreach (var c in AllCanvases) c.ProbedNodeCh1 = SelectedWire.NodeId;
        
        if (!IsOscilloscopeOpen)
        {
            IsOscilloscopeOpen = true;
        }
        
        StatusText = $"CH1 (Amber) Probing Net #{SelectedWire.NodeId}";
        NotifySelectedWireProbeProperties();
        InvalidateAllCanvases();
    }

    [RelayCommand]
    public void ProbeSelectedWireCh2()
    {
        if (SelectedWire == null) return;
        ProbedNodeCh2 = SelectedWire.NodeId;
        ProbedCh2Label = $"CH2: Net #{SelectedWire.NodeId}";
        foreach (var c in AllCanvases) c.ProbedNodeCh2 = SelectedWire.NodeId;
        
        if (!IsOscilloscopeOpen)
        {
            IsOscilloscopeOpen = true;
        }
        
        StatusText = $"CH2 (Cyan) Probing Net #{SelectedWire.NodeId}";
        NotifySelectedWireProbeProperties();
        InvalidateAllCanvases();
    }

    private void NotifySelectedWireProbeProperties()
    {
        OnPropertyChanged(nameof(IsSelectedWireProbed));
        OnPropertyChanged(nameof(IsSelectedWireProbedCh1));
        OnPropertyChanged(nameof(IsSelectedWireProbedCh2));
        OnPropertyChanged(nameof(SelectedWireProbeButtonText));
        OnPropertyChanged(nameof(SelectedWireProbeEyeIconKind));
        OnPropertyChanged(nameof(SelectedWireProbeButtonTooltip));
        OnPropertyChanged(nameof(SelectedWireProbeAccentBrush));
        OnPropertyChanged(nameof(SelectedWireConnectionDescription));
        OnPropertyChanged(nameof(SelectedWireRailTypeText));
        OnPropertyChanged(nameof(SelectedWireRailTypeColor));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    partial void OnInspectorValueChanged(double value)
    {
        if (SelectedDevice == null) return;

        var spec = DeviceRegistry.GetPrimaryParameter(SelectedDevice);
        if (spec != null)
        {
            spec.Setter(SelectedDevice, value);
            var vm = DeviceParameters.FirstOrDefault(p => p.ParameterName == spec.ParameterName);
            vm?.RefreshFromDevice();
        }

        if (SelectedDevice is LedDevice)
        {
            NotifyLedPropertiesChanged();
        }

        InvalidateAllCanvases();
    }

    private void OnSimulationTick(object? sender, EventArgs e)
    {
        if (!IsSimulating || _solver == null || CurrentCircuit == null) return;

        int subSteps = 20;
        double totalDt = 0.001 * SimulationSpeed;
        double subDt = totalDt / subSteps;

        for (int i = 0; i < subSteps; i++)
        {
            _solver.Step(subDt);
        }

        foreach (var c in AllCanvases)
        {
            c.ParticleEngine.Update(1.0 / 60.0, SimulationSpeed);
            c.InvalidateVisual();
        }
        if (HasSelectedWire)
        {
            UpdateSelectedWireMeasurements();
        }
        UpdateOscilloscope();
    }

    private void UpdateOscilloscope()
    {
        if (_oscilloscope == null || CurrentCircuit == null) return;

        var channels = _oscilloscope.Channels;
        if (channels.Count == 0) return;

        foreach (var ch in channels)
        {
            if (!ch.IsEnabled) continue;

            double val = 0.0;
            if (ch.TargetNodeId.HasValue && CurrentCircuit.Nodes.TryGetValue(ch.TargetNodeId.Value, out var targetNode))
            {
                val = targetNode.Voltage;
                if (ch.ReferenceNodeId.HasValue && CurrentCircuit.Nodes.TryGetValue(ch.ReferenceNodeId.Value, out var refNode))
                {
                    val -= refNode.Voltage;
                }
            }
            else
            {
                val = GetAutoChannelSample(ch.ChannelIndex);
            }

            ch.AddSample(val);
        }

        _oscilloscope.InvalidateVisual();
    }

    private double GetAutoChannelSample(int channelIndex)
    {
        if (CurrentCircuit == null) return 0.0;

        int? probedNode = channelIndex switch
        {
            1 => ProbedNodeCh1,
            2 => ProbedNodeCh2,
            3 => ProbedNodeCh3,
            4 => ProbedNodeCh4,
            _ => null
        };

        if (probedNode.HasValue && CurrentCircuit.Nodes.TryGetValue(probedNode.Value, out var probed))
        {
            return probed.Voltage;
        }

        // Direct node index match
        if (CurrentCircuit.Nodes.TryGetValue(channelIndex, out var node))
        {
            return node.Voltage;
        }

        // Fall back to non-ground nodes in sequential order
        var nonGroundNodes = CurrentCircuit.Nodes.Values.Where(n => !n.IsGround).OrderBy(n => n.Id).ToList();
        int nodeIdx = channelIndex - 1;
        if (nodeIdx >= 0 && nodeIdx < nonGroundNodes.Count)
        {
            return nonGroundNodes[nodeIdx].Voltage;
        }

        return 0.0;
    }

    [RelayCommand]
    public void Undo()
    {
        if (History.Undo())
        {
            foreach (var c in AllCanvases)
            {
                if (CurrentCircuit != null) c.ParticleEngine.SyncWithCircuit(CurrentCircuit);
                c.InvalidateVisual();
            }
            StatusText = $"Undone: {History.NextRedoDescription ?? "action"}";
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (History.Redo())
        {
            foreach (var c in AllCanvases)
            {
                if (CurrentCircuit != null) c.ParticleEngine.SyncWithCircuit(CurrentCircuit);
                c.InvalidateVisual();
            }
            StatusText = $"Redone: {History.NextUndoDescription ?? "action"}";
        }
    }

    [RelayCommand]
    public void StartProbing(string ch)
    {
        int chIdx = int.TryParse(ch, out int parsed) ? parsed : 1;
        ActiveProbingChannel = chIdx;
        var mode = chIdx switch
        {
            1 => CircuitToolMode.ProbeCh1,
            2 => CircuitToolMode.ProbeCh2,
            _ => CircuitToolMode.Probe
        };
        ActiveToolMode = mode;
        foreach (var c in AllCanvases)
        {
            c.ActiveProbingChannelIndex = chIdx;
            c.ToolMode = mode;
            c.PlacingDeviceType = null;
        }
        StatusText = $"Probe Mode: Click any pin, wire or component to attach Oscilloscope Channel {ch}. (Esc to cancel)";
    }

    [RelayCommand]
    public void ClearProbes()
    {
        ProbedNodeCh1 = null;
        ProbedNodeCh2 = null;
        ProbedNodeCh3 = null;
        ProbedNodeCh4 = null;
        ProbedCh1Label = "CH1: Auto (Input)";
        ProbedCh2Label = "CH2: Auto (Output)";
        ProbedCh3Label = "CH3: Auto";
        ProbedCh4Label = "CH4: Auto";

        if (_oscilloscope != null)
        {
            foreach (var ch in _oscilloscope.Channels)
            {
                ch.TargetNodeId = null;
                ch.Name = $"CH{ch.ChannelIndex}";
            }
        }
        foreach (var ch in ScopeChannels)
        {
            ch.TargetNodeId = null;
            ch.Name = $"CH{ch.ChannelIndex}";
        }

        foreach (var c in AllCanvases)
        {
            c.ProbedNodeCh1 = null;
            c.ProbedNodeCh2 = null;
            c.InvalidateVisual();
        }
        StatusText = "Cleared oscilloscope probes to Auto";
        NotifySelectedWireProbeProperties();
    }

    public void SetProbedNode(int channel, int nodeId)
    {
        var targetCh = _oscilloscope?.Channels.FirstOrDefault(c => c.ChannelIndex == channel)
                       ?? ScopeChannels.FirstOrDefault(c => c.ChannelIndex == channel);
        if (targetCh != null)
        {
            targetCh.TargetNodeId = nodeId;
            targetCh.Name = $"CH{channel}: #{nodeId}";
        }

        if (channel == 1)
        {
            ProbedNodeCh1 = nodeId;
            ProbedCh1Label = $"CH1: Net #{nodeId}";
            StatusText = $"Oscilloscope CH1 attached to Net #{nodeId}";
        }
        else if (channel == 2)
        {
            ProbedNodeCh2 = nodeId;
            ProbedCh2Label = $"CH2: Net #{nodeId}";
            StatusText = $"Oscilloscope CH2 attached to Net #{nodeId}";
        }
        else if (channel == 3)
        {
            ProbedNodeCh3 = nodeId;
            ProbedCh3Label = $"CH3: Net #{nodeId}";
            StatusText = $"Oscilloscope CH3 attached to Net #{nodeId}";
        }
        else if (channel == 4)
        {
            ProbedNodeCh4 = nodeId;
            ProbedCh4Label = $"CH4: Net #{nodeId}";
            StatusText = $"Oscilloscope CH4 attached to Net #{nodeId}";
        }
        else
        {
            StatusText = $"Oscilloscope CH{channel} attached to Net #{nodeId}";
        }

        if (!IsOscilloscopeOpen)
        {
            IsOscilloscopeOpen = true;
        }
        ActiveToolMode = CircuitToolMode.Select;
        foreach (var c in AllCanvases)
        {
            c.ToolMode = CircuitToolMode.Select;
            c.InvalidateVisual();
        }
        NotifySelectedWireProbeProperties();
    }

    [ObservableProperty]
    private bool _isScopeBoxZoomActive;

    [ObservableProperty]
    private bool _isScopeCursorsActive;

    [RelayCommand]
    public void ToggleScopeCursors()
    {
        if (_oscilloscope == null) return;
        _oscilloscope.ShowCursors = !_oscilloscope.ShowCursors;
        IsScopeCursorsActive = _oscilloscope.ShowCursors;
        StatusText = _oscilloscope.ShowCursors ? "Oscilloscope Cursors Enabled" : "Oscilloscope Cursors Disabled";
    }

    [RelayCommand]
    public void AutoSetScope()
    {
        _oscilloscope?.AutoSet();
        StatusText = "Oscilloscope Auto-Set completed";
    }

    [RelayCommand]
    public void ResetScopePan()
    {
        _oscilloscope?.ResetPanAndZoom();
        IsScopeBoxZoomActive = false;
        StatusText = "Oscilloscope Pan & Zoom reset to center";
    }

    [RelayCommand]
    public void ScopeZoomInTime()
    {
        _oscilloscope?.ZoomInTime();
        StatusText = $"Time Zoom: {_oscilloscope?.TimebaseZoom:F1}x";
    }

    [RelayCommand]
    public void ScopeZoomOutTime()
    {
        _oscilloscope?.ZoomOutTime();
        StatusText = $"Time Zoom: {_oscilloscope?.TimebaseZoom:F1}x";
    }

    [RelayCommand]
    public void ScopePanLeftTime()
    {
        _oscilloscope?.PanTime(-1.0);
        StatusText = $"Time Offset: {_oscilloscope?.TimeOffsetDivs:+0.0;-0.0;0.0} divs";
    }

    [RelayCommand]
    public void ScopePanRightTime()
    {
        _oscilloscope?.PanTime(1.0);
        StatusText = $"Time Offset: {_oscilloscope?.TimeOffsetDivs:+0.0;-0.0;0.0} divs";
    }

    [RelayCommand]
    public void ScopeZoomInVoltage()
    {
        _oscilloscope?.ZoomInVoltage();
        var ch = _oscilloscope?.Channels.FirstOrDefault(c => c.ChannelIndex == _oscilloscope.ActiveChannelIndex);
        StatusText = $"CH{_oscilloscope?.ActiveChannelIndex} Volts/Div: {ch?.VoltsPerDiv:G3} V/div";
    }

    [RelayCommand]
    public void ScopeZoomOutVoltage()
    {
        _oscilloscope?.ZoomOutVoltage();
        var ch = _oscilloscope?.Channels.FirstOrDefault(c => c.ChannelIndex == _oscilloscope.ActiveChannelIndex);
        StatusText = $"CH{_oscilloscope?.ActiveChannelIndex} Volts/Div: {ch?.VoltsPerDiv:G3} V/div";
    }

    [RelayCommand]
    public void ScopePanUpVoltage()
    {
        _oscilloscope?.PanVoltage(0.5);
        var ch = _oscilloscope?.Channels.FirstOrDefault(c => c.ChannelIndex == _oscilloscope.ActiveChannelIndex);
        StatusText = $"CH{_oscilloscope?.ActiveChannelIndex} Offset: {ch?.VerticalOffsetDivs:+0.0;-0.0;0.0} divs";
    }

    [RelayCommand]
    public void ScopePanDownVoltage()
    {
        _oscilloscope?.PanVoltage(-0.5);
        var ch = _oscilloscope?.Channels.FirstOrDefault(c => c.ChannelIndex == _oscilloscope.ActiveChannelIndex);
        StatusText = $"CH{_oscilloscope?.ActiveChannelIndex} Offset: {ch?.VerticalOffsetDivs:+0.0;-0.0;0.0} divs";
    }

    [RelayCommand]
    public void ScopeToggleBoxZoom()
    {
        if (_oscilloscope == null) return;
        _oscilloscope.ToggleBoxZoom();
        IsScopeBoxZoomActive = _oscilloscope.IsBoxZoomMode;
        StatusText = _oscilloscope.IsBoxZoomMode
            ? "Box Zoom: Drag a rectangle over the waveform to zoom"
            : "Box Zoom Deactivated";
    }

    [RelayCommand]
    public void ToggleScopeLayout()
    {
        if (_oscilloscope == null) return;
        _oscilloscope.DisplayLayout = _oscilloscope.DisplayLayout == OscilloscopeDisplayLayout.Overlay
            ? OscilloscopeDisplayLayout.Stacked
            : OscilloscopeDisplayLayout.Overlay;
        OnPropertyChanged(nameof(ScopeLayoutLabel));
        StatusText = $"Oscilloscope Layout set to {_oscilloscope.DisplayLayout}";
    }

    public string ScopeLayoutLabel => (_oscilloscope?.DisplayLayout == OscilloscopeDisplayLayout.Stacked) ? "Stacked" : "Overlay";

    [RelayCommand]
    public void AddScopeChannel()
    {
        if (_oscilloscope == null) return;
        int nextIdx = _oscilloscope.Channels.Count + 1;
        var newCh = OscilloscopeChannel.CreateDefault(nextIdx);
        _oscilloscope.Channels.Add(newCh);
        if (!ScopeChannels.Any(c => c.ChannelIndex == nextIdx))
        {
            ScopeChannels.Add(newCh);
        }
        StatusText = $"Added Oscilloscope Channel {nextIdx}";
    }

    [RelayCommand]
    public void SetToolMode(string modeStr)
    {
        var mode = modeStr.ToLowerInvariant() switch
        {
            "wire" => CircuitToolMode.Wire,
            "cut" => CircuitToolMode.Cut,
            "pan" => CircuitToolMode.Pan,
            "probe1" => CircuitToolMode.ProbeCh1,
            "probe2" => CircuitToolMode.ProbeCh2,
            _ => CircuitToolMode.Select
        };

        ActiveToolMode = mode;
        foreach (var c in AllCanvases)
        {
            c.ToolMode = mode;
            c.PlacingDeviceType = null;
        }

        StatusText = mode switch
        {
            CircuitToolMode.Wire => "Wire Mode: Click any pin, then click another pin to connect wire. (Esc to cancel)",
            CircuitToolMode.Cut => "Cut Mode: Click any wire to cut it, or pin to disconnect. (Esc to cancel)",
            _ => "Select Mode: Click components to inspect, drag to move, click switches to toggle."
        };
    }

    [RelayCommand]
    public void StartPlacingDevice(string type)
    {
        ActiveToolMode = CircuitToolMode.Select;
        foreach (var c in AllCanvases)
        {
            c.ToolMode = CircuitToolMode.Select;
            c.PlacingDeviceType = type;
        }

        StatusText = $"Placing {type.ToUpperInvariant()} - Click on canvas to drop. Press 'R' to rotate, Esc to cancel.";
    }

    [RelayCommand]
    public void RotateSelected()
    {
        foreach (var c in AllCanvases)
        {
            c.RotateSelected(clockwise: true);
        }
        StatusText = "Rotated 90° Clockwise";
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        foreach (var c in AllCanvases)
        {
            c.DeleteSelected();
        }
        StatusText = "Deleted selection";
    }

    [RelayCommand]
    public void ResetBlownDevice()
    {
        if (SelectedDevice != null && SelectedDevice.IsBlown)
        {
            SelectedDevice.ResetBlown();
            StatusText = $"Replaced {SelectedDevice.Type.ToUpperInvariant()} #{SelectedDevice.Id} (Pristine Condition)";
            foreach (var c in AllCanvases)
            {
                c.InvalidateVisual();
            }
        }
    }

    [RelayCommand]
    public void SetInspectorPreset(object? parameter)
    {
        if (parameter is double d)
        {
            InspectorValue = d;
        }
        else if (parameter is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            InspectorValue = parsed;
        }
        else if (parameter is IConvertible c)
        {
            InspectorValue = Convert.ToDouble(c, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [RelayCommand]
    public void SetLedColor(object? parameter)
    {
        if (SelectedDevice is not LedDevice led) return;

        double targetWavelength = led.Wavelength;
        if (parameter is LedColorPresetItem preset)
        {
            targetWavelength = preset.Wavelength;
        }
        else if (parameter is double d)
        {
            targetWavelength = d;
        }
        else if (parameter is string s)
        {
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedWavelength))
            {
                targetWavelength = parsedWavelength;
            }
            else
            {
                var match = LedColorPresets.FirstOrDefault(p => string.Equals(p.Name, s, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    targetWavelength = match.Wavelength;
                }
            }
        }

        led.Wavelength = targetWavelength;
        InspectorValue = targetWavelength;

        var pVm = DeviceParameters.FirstOrDefault(p => p.ParameterName == "Wavelength");
        pVm?.RefreshFromDevice();

        NotifyLedPropertiesChanged();

        InvalidateAllCanvases();
        StatusText = $"Set LED color to {led.ColorName} ({led.Wavelength:F0} nm, Vf ≈ {led.ForwardVoltage:F1} V)";
    }

    [RelayCommand]
    public void SetWaveform(string type)
    {
        if (SelectedDevice is VoltageSourceDevice vs)
        {
            vs.WaveformType = type.ToLowerInvariant() switch
            {
                "sine" => VoltageWaveformType.Sine,
                "pulse" => VoltageWaveformType.Pulse,
                "triangle" => VoltageWaveformType.Triangle,
                _ => VoltageWaveformType.Dc
            };
            InspectorLabel = vs.WaveformType == VoltageWaveformType.Sine ? "Amplitude" : "Voltage";
            InvalidateAllCanvases();
            StatusText = $"Set waveform to {vs.WaveformType}";
        }
    }

    [RelayCommand]
    private void ToggleSimulation()
    {
        IsSimulating = !IsSimulating;
        StatusText = IsSimulating ? "Simulation Running" : "Simulation Paused";
    }

    [RelayCommand]
    private void StepSimulation()
    {
        if (_solver == null || CurrentCircuit == null) return;
        int subSteps = 5;
        double subDt = 0.00025 / subSteps;
        for (int i = 0; i < subSteps; i++)
        {
            _solver.Step(subDt);
        }
        foreach (var c in AllCanvases)
        {
            c.ParticleEngine.Update(0.00025, SimulationSpeed);
            c.InvalidateVisual();
        }
        UpdateOscilloscope();
        StatusText = $"Stepped to {_solver.CurrentTime * 1000.0:F2} ms";
    }

    [RelayCommand]
    public void ResetSimulation()
    {
        _solver?.Reset();

        // Clear oscilloscope history traces
        if (_oscilloscope != null)
        {
            foreach (var trace in _oscilloscope.Traces)
            {
                trace.Clear();
            }
            _oscilloscope.InvalidateVisual();
        }

        // Re-sync particle engines to clear stale current animations and refresh all canvases
        foreach (var c in AllCanvases)
        {
            if (CurrentCircuit != null)
            {
                c.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            }
            c.InvalidateVisual();
        }

        StatusText = "Simulation Restarted: t = 0.0 ms, charges cleared & all components restored";
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        foreach (var c in AllCanvases)
        {
            c.ZoomToFit();
            c.InvalidateVisual();
        }
        StatusText = "View centered to fit circuit";
    }

    partial void OnSelectedPresetChanged(string? value)
    {
        if (_isSwitchingCircuit) return;
        if (!string.IsNullOrEmpty(value) && value != CurrentCircuit?.Name && FryCircuit.Core.Presets.CircuitPresets.PresetNames.Contains(value))
        {
            LoadPreset(value);
        }
    }

    [RelayCommand]
    public void LoadPreset(string presetName)
    {
        try
        {
            var circuit = FryCircuit.Core.Presets.CircuitPresets.Create(presetName);
            SetCircuit(circuit);
            SelectedPreset = presetName;
            CircuitTitle = $"FryCircuit - {circuit.Name}";
            StatusText = $"Loaded '{circuit.Name}' (Pure C# Preset)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading {presetName}: {ex.Message}";
        }
    }

    [RelayCommand]
    public void NewCircuit()
    {
        bool wasSimulating = IsSimulating;
        IsSimulating = false;

        var newCirc = new Circuit
        {
            Name = "New Circuit"
        };
        var groundDev = DeviceRegistry.CreateDevice("ground", 1, 10, 15);
        if (groundDev != null)
        {
            newCirc.AddDevice(groundDev);
        }

        CurrentFilePath = null;
        SetCircuit(newCirc);
        History.Clear();
        CanUndo = History.CanUndo;
        CanRedo = History.CanRedo;
        IsDirty = false;
        StatusText = "Created new blank circuit";

        if (wasSimulating)
        {
            IsSimulating = true;
        }
    }

    [RelayCommand]
    public async Task OpenCircuitFileDialogAsync()
    {
        if (StorageProvider == null)
        {
            StatusText = "File picker unavailable";
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Circuit Schematic",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Circuit Schematics (*.fcircuit; *.frycircuit; *.json; *.circuit)")
                    {
                        Patterns = new[] { "*.fcircuit", "*.frycircuit", "*.json", "*.circuit", "*.xml" }
                    },
                    new("LLM Circuit Format (*.fcircuit; *.json)")
                    {
                        Patterns = new[] { "*.fcircuit", "*.json", "*.fcircuit.json" }
                    },
                    new("Native FryCircuit XML (*.frycircuit; *.circuit)")
                    {
                        Patterns = new[] { "*.frycircuit", "*.circuit" }
                    },
                    new("SPICE Netlists (*.cir; *.sp; *.net)")
                    {
                        Patterns = new[] { "*.cir", "*.sp", "*.net" }
                    },
                    new("All Files (*.*)")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                string localPath = file.Path.LocalPath;
                await using var stream = await file.OpenReadAsync();
                var circuit = CircuitSerializer.LoadFromStream(stream);
                CurrentFilePath = localPath;
                SetCircuit(circuit);
                IsDirty = false;
                StatusText = $"Opened {Path.GetFileName(localPath)}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Open error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveCircuitAsync()
    {
        if (!string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            SaveCircuit(CurrentFilePath);
            return;
        }

        await SaveCircuitAsDialogAsync();
    }

    [RelayCommand]
    public async Task SaveCircuitAsDialogAsync()
    {
        if (StorageProvider == null)
        {
            StatusText = "File picker unavailable";
            return;
        }

        if (CurrentCircuit == null) return;

        try
        {
            string suggested = string.IsNullOrWhiteSpace(CurrentFilePath)
                ? (string.IsNullOrWhiteSpace(CurrentCircuit.Name) ? "circuit.fcircuit" : $"{CurrentCircuit.Name.Replace(" ", "_").ToLowerInvariant()}.fcircuit")
                : Path.GetFileName(CurrentFilePath);

            if (!suggested.EndsWith(".fcircuit", StringComparison.OrdinalIgnoreCase) &&
                !suggested.EndsWith(".frycircuit", StringComparison.OrdinalIgnoreCase) &&
                !suggested.EndsWith(".circuit", StringComparison.OrdinalIgnoreCase) &&
                !suggested.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                suggested += ".fcircuit";
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Circuit As",
                DefaultExtension = "fcircuit",
                SuggestedFileName = suggested,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("LLM Circuit Format (*.fcircuit)")
                    {
                        Patterns = new[] { "*.fcircuit" }
                    },
                    new("FryCircuit XML File (*.frycircuit)")
                    {
                        Patterns = new[] { "*.frycircuit" }
                    },
                    new("JSON Circuit (*.json)")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new("Legacy Circuit File (*.circuit)")
                    {
                        Patterns = new[] { "*.circuit" }
                    }
                }
            });

            if (file != null)
            {
                string localPath = file.Path.LocalPath;
                SaveCircuit(localPath);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    public void SaveCircuit(string filePath)
    {
        if (CurrentCircuit == null) return;
        try
        {
            if (filePath.EndsWith(".fcircuit", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = LlmCircuitSerializer.Serialize(CurrentCircuit);
                File.WriteAllText(filePath, json);
            }
            else
            {
                CircuitSerializer.SaveToFile(CurrentCircuit, filePath);
            }
            CurrentFilePath = filePath;
            IsDirty = false;
            StatusText = $"Saved circuit to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    public void OpenCircuit(string filePath)
    {
        try
        {
            Circuit circuit;
            if (filePath.EndsWith(".fcircuit", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = File.ReadAllText(filePath);
                circuit = LlmCircuitSerializer.Deserialize(json, Path.GetFileNameWithoutExtension(filePath));
            }
            else
            {
                circuit = CircuitSerializer.LoadFromFile(filePath);
            }
            CurrentFilePath = filePath;
            SetCircuit(circuit);
            IsDirty = false;
            StatusText = $"Opened {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Open error: {ex.Message}";
        }
    }

    public string PlayPauseButtonText => IsSimulating ? "Pause" : "Play";
    public MaterialIconKind PlayPauseIconKind => IsSimulating ? MaterialIconKind.Pause : MaterialIconKind.Play;

    public bool IsDarkTheme => FryCircuit.UI.Theming.CircuitThemeService.Instance.IsDark;
    public MaterialIconKind ThemeIconKind => IsDarkTheme ? MaterialIconKind.WeatherSunny : MaterialIconKind.WeatherNight;
    public string ThemeTooltip => IsDarkTheme ? "Switch to Light Theme" : "Switch to Dark Theme";

    [RelayCommand]
    public void ToggleTheme()
    {
        FryCircuit.UI.Theming.CircuitThemeService.Instance.ToggleTheme();
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIconKind));
        OnPropertyChanged(nameof(ThemeTooltip));
    }

    private void OnThemeChanged(FryWorld.Sdk.Services.AppTheme theme)
    {
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIconKind));
        OnPropertyChanged(nameof(ThemeTooltip));
    }

    partial void OnIsSimulatingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseButtonText));
        OnPropertyChanged(nameof(PlayPauseIconKind));
    }

    [ObservableProperty]
    private bool _isSpiceDrawerOpen = false;

    [ObservableProperty]
    private string _spiceNetlistText = "";

    [RelayCommand]
    public void ToggleSpiceDrawer()
    {
        IsSpiceDrawerOpen = !IsSpiceDrawerOpen;
        if (IsSpiceDrawerOpen && CurrentCircuit != null)
        {
            GenerateSpiceNetlist();
        }
    }

    [RelayCommand]
    public void GenerateSpiceNetlist()
    {
        if (CurrentCircuit == null) return;
        SpiceNetlistText = SpiceNetlistEngine.ExportSpice(CurrentCircuit, OperatingTemperatureKelvin);
        StatusText = $"Generated SPICE simulation deck ({CurrentCircuit.Devices.Count} devices, T = {OperatingTemperatureKelvin:F1} K)";
    }

    [RelayCommand]
    public async Task CopySpiceToClipboard()
    {
        if (string.IsNullOrEmpty(SpiceNetlistText)) return;
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = desktop.MainWindow;
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(SpiceNetlistText);
                    StatusText = "SPICE deck copied to clipboard!";
                    return;
                }
            }
        }
        catch { }
        StatusText = "SPICE deck ready to copy.";
    }

    [RelayCommand]
    public void ImportSpiceDeck()
    {
        if (string.IsNullOrWhiteSpace(SpiceNetlistText)) return;
        try
        {
            var imported = SpiceNetlistEngine.ImportSpice(SpiceNetlistText, "Imported SPICE");
            SetCircuit(imported);
            IsSpiceDrawerOpen = false;
            StatusText = $"Successfully imported SPICE netlist ({imported.Devices.Count} components)";
        }
        catch (Exception ex)
        {
            StatusText = $"SPICE import error: {ex.Message}";
        }
    }

    [ObservableProperty]
    private bool _isBomDrawerOpen = false;

    [ObservableProperty]
    private string _bomText = "";

    [RelayCommand]
    public void ToggleBomDrawer()
    {
        IsBomDrawerOpen = !IsBomDrawerOpen;
        if (IsBomDrawerOpen && CurrentCircuit != null)
        {
            GenerateBom();
        }
    }

    [RelayCommand]
    public void GenerateBom()
    {
        if (CurrentCircuit == null) return;
        BomText = BomEngine.GenerateMarkdownBom(CurrentCircuit, OperatingTemperatureKelvin);
        StatusText = $"Generated Bill of Materials ({CurrentCircuit.Devices.Count} components)";
    }

    [RelayCommand]
    public async Task CopyBomToClipboard()
    {
        if (string.IsNullOrEmpty(BomText)) return;
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = desktop.MainWindow;
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(BomText);
                    StatusText = "Bill of Materials (BOM) copied to clipboard!";
                    return;
                }
            }
        }
        catch { }
        StatusText = "BOM ready to copy.";
    }

    [ObservableProperty]
    private bool _isLlmDrawerOpen = false;

    [ObservableProperty]
    private string _llmCircuitText = "";

    [ObservableProperty]
    private string _llmPromptText = "";

    [ObservableProperty]
    private int _llmDrawerTab = 0; // 0 = JSON deck, 1 = Turnkey AI prompt

    [RelayCommand]
    public void ToggleLlmDrawer()
    {
        IsLlmDrawerOpen = !IsLlmDrawerOpen;
        if (IsLlmDrawerOpen && CurrentCircuit != null)
        {
            GenerateLlmDeck();
        }
    }

    [RelayCommand]
    public void GenerateLlmDeck()
    {
        if (CurrentCircuit == null) return;
        LlmCircuitText = LlmCircuitSerializer.Serialize(CurrentCircuit);
        LlmPromptText = LlmPromptGenerator.GenerateCircuitInspectionPrompt(CurrentCircuit);
        StatusText = $"Generated AI .fcircuit deck ({CurrentCircuit.Devices.Count} components)";
    }

    [RelayCommand]
    public async Task CopyLlmJsonToClipboard()
    {
        if (string.IsNullOrEmpty(LlmCircuitText)) return;
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = desktop.MainWindow;
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(LlmCircuitText);
                    StatusText = ".fcircuit JSON copied to clipboard!";
                    return;
                }
            }
        }
        catch { }
        StatusText = ".fcircuit JSON ready to copy.";
    }

    [RelayCommand]
    public async Task CopyLlmPromptToClipboard()
    {
        if (string.IsNullOrEmpty(LlmPromptText))
        {
            LlmPromptText = CurrentCircuit != null
                ? LlmPromptGenerator.GenerateCircuitInspectionPrompt(CurrentCircuit)
                : LlmPromptGenerator.GenerateSystemPrompt();
        }
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = desktop.MainWindow;
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(LlmPromptText);
                    StatusText = "Turnkey AI Circuit Prompt copied to clipboard!";
                    return;
                }
            }
        }
        catch { }
        StatusText = "AI Circuit Prompt ready to copy.";
    }

    [RelayCommand]
    public void ImportLlmDeck()
    {
        if (string.IsNullOrWhiteSpace(LlmCircuitText)) return;
        try
        {
            var imported = LlmCircuitSerializer.Deserialize(LlmCircuitText, "AI Generated Circuit");
            SetCircuit(imported);
            IsLlmDrawerOpen = false;
            StatusText = $"Successfully imported AI circuit ({imported.Devices.Count} components)";
            ShowAiActivity("Imported AI Deck", $"Loaded '{imported.Name}' ({imported.Devices.Count} components)", "Imported from AI Circuit Deck Studio", "AutoFixHigh");
        }
        catch (Exception ex)
        {
            StatusText = $"AI deck import error: {ex.Message}";
            ShowAiActivity("Import Error", ex.Message, null, "AlertCircle");
        }
    }

    [RelayCommand]
    public void LoadLlmTemplate(string templateName)
    {
        LlmCircuitText = LlmPromptGenerator.GetSampleTemplate(templateName);
        LlmDrawerTab = 0;
        StatusText = $"Loaded template '{templateName}'. Click 'Import & Simulate' to run.";
    }

    // ========================================================
    // CONTROL SYSTEMS STUDIO STATE & TELEMETRY
    // ========================================================
    [ObservableProperty]
    private bool _isControlStudioDrawerOpen = false;

    [ObservableProperty]
    private string _controlStudioActiveTab = "Bode"; // Bode, Nyquist, GangOfFour, StepResponse, RootLocus, ProblemSolver

    public bool IsControlBodeTab => ControlStudioActiveTab == "Bode";
    public bool IsControlNyquistTab => ControlStudioActiveTab == "Nyquist";
    public bool IsControlGangOfFourTab => ControlStudioActiveTab == "GangOfFour";
    public bool IsControlStepTab => ControlStudioActiveTab == "StepResponse";
    public bool IsControlRootLocusTab => ControlStudioActiveTab == "RootLocus";
    public bool IsControlProblemSolverTab => ControlStudioActiveTab == "ProblemSolver";

    [ObservableProperty]
    private string _controlNumeratorText = "1";

    [ObservableProperty]
    private string _controlDenominatorText = "1, 3, 3, 1";

    [ObservableProperty]
    private string _controlControllerNumText = "3, 2";

    [ObservableProperty]
    private string _controlControllerDenText = "1, 0";

    [ObservableProperty]
    private TransferFunction? _controlPlant;

    [ObservableProperty]
    private TransferFunction? _controlController;

    [ObservableProperty]
    private double _controlLocusGain = 1.0;

    [ObservableProperty]
    private double _controlProblem4Overshoot = 9.5;

    [ObservableProperty]
    private double _controlProblem4SettlingTime = 0.74;

    [ObservableProperty]
    private double _controlProblem5PoleA = 1.0;

    [ObservableProperty]
    private double _controlProblem5GainK = 5.0;

    [ObservableProperty]
    private bool _isControlSystemStable = true;

    [ObservableProperty]
    private string _controlSystemStabilityText = "STABLE";

    [ObservableProperty]
    private string _controlGainMarginText = "∞ dB";

    [ObservableProperty]
    private string _controlPhaseMarginText = "53.4°";

    [ObservableProperty]
    private string _controlCrossoverFreqText = "1.24 rad/s";

    [ObservableProperty]
    private string _controlMsText = "1.32";

    [ObservableProperty]
    private string _controlMtText = "1.00";

    [ObservableProperty]
    private string _controlBandwidthText = "1.15 rad/s";

    [ObservableProperty]
    private string _controlRiseTimeText = "1.28 s";

    [ObservableProperty]
    private string _controlOvershootText = "9.48 %";

    [ObservableProperty]
    private string _controlSettlingTimeText = "0.74 s";

    [ObservableProperty]
    private string _controlSteadyStateErrorText = "0.000";

    [ObservableProperty]
    private string _controlPolesZerosText = "Poles: -1, -1, -1 | Zeros: none";

    [ObservableProperty]
    private string _controlPolesAcademicText = "• s₁,₂ = -0.03 ± j1.15  (ζ = 0.026, ωn = 1.15 rad/s)\n• s₃ = -0.66\n• s₄ = -2.29";

    [ObservableProperty]
    private string _controlZerosAcademicText = "• z₁ = -0.67";

    [ObservableProperty]
    private string _controlOrderBadgeText = "Order n = 4, Zeros m = 1";

    [ObservableProperty]
    private string _controlStabilityBadgeText = "✓ Hurwitz (All LHP)";

    [ObservableProperty]
    private bool _controlIsHurwitzStable = true;

    [ObservableProperty]
    private string _controlClosedLoopTfNumText = "3s + 2";

    [ObservableProperty]
    private string _controlClosedLoopTfDenText = "s⁴ + 3s³ + 3s² + 4s + 2";

    [ObservableProperty]
    private string _controlClosedLoopTfFullText = "T(s) = (3s + 2) / (s⁴ + 3s³ + 3s² + 4s + 2)";

    [ObservableProperty]
    private string _controlWaterbedText = "Bode Integral = 0.000 (No RHP poles)";

    [ObservableProperty]
    private string _controlProblem4SolutionText = "";

    [ObservableProperty]
    private string _controlProblem5SolutionText = "";

    [ObservableProperty]
    private string _selectedControlPreset = "Åström Gang of Four Benchmark";

    public ObservableCollection<string> ControlPresetsList { get; } = new()
    {
        "Åström Gang of Four Benchmark",
        "2nd-Order Step Specs Synthesis (%OS, Ts)",
        "Speed Plant Sensitivity & Rejection",
        "Feedback Loop Block Diagram Equivalence",
        "PID Motor Speed Control",
        "Second-Order Underdamped (OS = 9.5%)",
        "Type 1 Integrator System (1/s(s+1))"
    };

    [RelayCommand]
    public void ToggleControlStudioDrawer()
    {
        IsControlStudioDrawerOpen = !IsControlStudioDrawerOpen;
        if (IsControlStudioDrawerOpen)
        {
            RecalculateControlStudio();
        }
    }

    [RelayCommand]
    public void SetControlStudioTab(string tab)
    {
        ControlStudioActiveTab = tab;
        OnPropertyChanged(nameof(IsControlBodeTab));
        OnPropertyChanged(nameof(IsControlNyquistTab));
        OnPropertyChanged(nameof(IsControlGangOfFourTab));
        OnPropertyChanged(nameof(IsControlStepTab));
        OnPropertyChanged(nameof(IsControlRootLocusTab));
        OnPropertyChanged(nameof(IsControlProblemSolverTab));
    }

    [RelayCommand]
    public void SelectControlPreset(string preset)
    {
        SelectedControlPreset = preset;
        switch (preset)
        {
            case "Åström Gang of Four Benchmark":
                ControlNumeratorText = "1";
                ControlDenominatorText = "1, 3, 3, 1"; // 1/(s+1)^3
                ControlControllerNumText = "3, 2";       // (3s + 2)/s
                ControlControllerDenText = "1, 0";
                break;
            case "2nd-Order Step Specs Synthesis (%OS, Ts)":
            case "Problem 4 - Second-Order Step Specs":
                // OS = 9.5% => zeta = 0.6, Ts = 0.74 => wn = 9.0 => G = wn^2 / (s^2 + 2*zeta*wn*s)
                ControlNumeratorText = "81.08";
                ControlDenominatorText = "1, 10.81, 0";
                ControlControllerNumText = "1";
                ControlControllerDenText = "1";
                ControlProblem4Overshoot = 9.5;
                ControlProblem4SettlingTime = 0.74;
                break;
            case "Speed Plant Sensitivity & Rejection":
            case "Problem 5 - Plant Speed & Disturbance":
                ControlNumeratorText = "1";
                ControlDenominatorText = "1, 1"; // 1/(s+1)
                ControlControllerNumText = "5"; // K = 5
                ControlControllerDenText = "1";
                ControlProblem5PoleA = 1.0;
                ControlProblem5GainK = 5.0;
                break;
            case "Feedback Loop Block Diagram Equivalence":
            case "Image 2 - Loop Equivalence":
                // (s^2 + 3s + 2) / (s^3 + 4s^2 + 4s + 1)
                ControlNumeratorText = "1, 3, 2";
                ControlDenominatorText = "1, 4, 4, 1";
                ControlControllerNumText = "1";
                ControlControllerDenText = "1";
                break;
            case "PID Motor Speed Control":
                ControlNumeratorText = "2";
                ControlDenominatorText = "0.5, 1.5, 1";
                ControlControllerNumText = "2.5, 3.0, 0.5";
                ControlControllerDenText = "1, 0";
                break;
            case "Second-Order Underdamped (OS = 9.5%)":
                ControlNumeratorText = "16";
                ControlDenominatorText = "1, 4.8, 16";
                ControlControllerNumText = "1";
                ControlControllerDenText = "1";
                break;
            case "Type 1 Integrator System (1/s(s+1))":
                ControlNumeratorText = "1";
                ControlDenominatorText = "1, 1, 0";
                ControlControllerNumText = "2";
                ControlControllerDenText = "1";
                break;
        }
        RecalculateControlStudio();
    }

    [RelayCommand]
    public void SyncControlStudioFromSchematic()
    {
        if (SelectedDevice is TransferFunctionDevice tfDev)
        {
            ControlNumeratorText = tfDev.NumeratorString;
            ControlDenominatorText = tfDev.DenominatorString;
            ControlControllerNumText = "1";
            ControlControllerDenText = "1";
            StatusText = $"Synced Control Studio from selected block #{tfDev.Id}";
            RecalculateControlStudio();
            return;
        }

        if (CurrentCircuit != null)
        {
            var firstTf = CurrentCircuit.Devices.OfType<TransferFunctionDevice>().FirstOrDefault();
            if (firstTf != null)
            {
                ControlNumeratorText = firstTf.NumeratorString;
                ControlDenominatorText = firstTf.DenominatorString;
                StatusText = $"Synced Control Studio from schematic block #{firstTf.Id}";
                RecalculateControlStudio();
                return;
            }
        }

        StatusText = "No transfer function blocks found in active schematic.";
    }

    [RelayCommand]
    public void OpenControlStudioForSelectedDevice()
    {
        if (SelectedDevice is TransferFunctionDevice tfDev)
        {
            ControlNumeratorText = tfDev.NumeratorString;
            ControlDenominatorText = tfDev.DenominatorString;
        }
        else if (SelectedDevice is GainBlockDevice gb)
        {
            ControlControllerNumText = gb.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ControlControllerDenText = "1";
        }
        else if (SelectedDevice is PidControllerDevice pid)
        {
            ControlControllerNumText = $"{pid.Kd}, {pid.Kp}, {pid.Ki}";
            ControlControllerDenText = "1, 0";
        }
        IsControlStudioDrawerOpen = true;
        RecalculateControlStudio();
    }

    [RelayCommand]
    public void RecalculateControlStudio()
    {
        try
        {
            var p = TransferFunction.Parse(ControlNumeratorText, ControlDenominatorText);
            var c = TransferFunction.Parse(ControlControllerNumText, ControlControllerDenText);
            ControlPlant = p;
            ControlController = c;

            var loop = c * p;
            var cl = loop.Feedback(TransferFunction.One);

            // Bode & Margins
            var bode = BodeAnalyzer.Analyze(loop);
            IsControlSystemStable = bode.IsStable;
            ControlSystemStabilityText = bode.IsStable ? "STABLE" : "UNSTABLE";
            ControlGainMarginText = (bode.GainMarginDb.HasValue && !double.IsInfinity(bode.GainMarginDb.Value)) ? $"{bode.GainMarginDb.Value:F1} dB" : "∞ dB";
            ControlPhaseMarginText = bode.PhaseMarginDeg.HasValue ? $"{bode.PhaseMarginDeg.Value:F1}°" : "N/A";
            ControlCrossoverFreqText = bode.GainCrossoverFrequency.HasValue ? $"{bode.GainCrossoverFrequency.Value:F2} rad/s" : "N/A";

            // Gang of Four (Astrom Ch. 5)
            var gof = GangOfFourAnalyzer.Analyze(p, c);
            ControlMsText = $"{gof.PeakSensitivityMs:F2}";
            ControlMtText = $"{gof.PeakCompSensitivityMt:F2}";
            ControlBandwidthText = $"{gof.DisturbanceBandwidthWb:F2} rad/s";
            ControlWaterbedText = $"Integral = {gof.BodeSensitivityIntegralValue:F3} (Target: {gof.TheoreticalWaterbedTarget:F3})";

            // Step Response
            var step = StepResponseAnalyzer.Analyze(cl, 10.0, 300);
            ControlRiseTimeText = $"{step.RiseTime:F2} s";
            ControlOvershootText = $"{step.OvershootPercent:F1} %";
            ControlSettlingTimeText = $"{step.SettlingTime:F2} s";
            ControlSteadyStateErrorText = $"{step.SteadyStateError:F3}";

            // Academic Poles and Zeros Formatting (Åström-Murray & Ogata standard notation)
            var academicPoles = AcademicMathFormatter.GroupAndFormatRoots(cl.Poles, "s");
            var academicZeros = AcademicMathFormatter.GroupAndFormatRoots(cl.Zeros, "z");

            var poleLines = new List<string>();
            bool hasRhp = false;
            foreach (var pRoot in academicPoles)
            {
                if (pRoot.IsRhp) hasRhp = true;
                string detail = !string.IsNullOrEmpty(pRoot.DampingDetails) ? $"  ({pRoot.DampingDetails})" : "";
                string rhpWarning = pRoot.IsRhp ? "  ⚠ RHP (Unstable)" : "";
                poleLines.Add($"• {pRoot.Label} = {pRoot.ValueString}{detail}{rhpWarning}");
            }
            ControlPolesAcademicText = poleLines.Count > 0 ? string.Join("\n", poleLines) : "• None (No poles)";

            var zeroLines = new List<string>();
            foreach (var zRoot in academicZeros)
            {
                zeroLines.Add($"• {zRoot.Label} = {zRoot.ValueString}");
            }
            ControlZerosAcademicText = zeroLines.Count > 0 ? string.Join("\n", zeroLines) : "• None (All-pole plant)";

            ControlOrderBadgeText = $"Order n = {cl.Poles.Count}" + (cl.Zeros.Count > 0 ? $", Zeros m = {cl.Zeros.Count}" : ", All-Pole");
            ControlIsHurwitzStable = !hasRhp;
            ControlStabilityBadgeText = hasRhp ? "⚠ Unstable (RHP Poles)" : "✓ Hurwitz (All LHP)";

            string polesSummary = string.Join(", ", academicPoles.Select(p => $"{p.Label} = {p.ValueString}"));
            string zerosSummary = academicZeros.Count > 0 ? string.Join(", ", academicZeros.Select(z => $"{z.Label} = {z.ValueString}")) : "none";
            ControlPolesZerosText = $"Poles: [{polesSummary}] | Zeros: [{zerosSummary}]";

            // Closed-Loop Transfer Function Math Strings
            ControlClosedLoopTfNumText = cl.Numerator.ToAcademicString("s");
            ControlClosedLoopTfDenText = cl.Denominator.ToAcademicString("s");
            ControlClosedLoopTfFullText = $"T(s) = ({ControlClosedLoopTfNumText}) / ({ControlClosedLoopTfDenText})";

            // Problem 4 & 5 Solutions
            var p4 = ControlSpecsSolver.SolveProblem4Specs(ControlProblem4Overshoot, ControlProblem4SettlingTime);
            ControlProblem4SolutionText = $"ζ = {p4.DampingRatioZeta:F4}, ωn = {p4.NaturalFrequencyWn:F2} rad/s, Req K = {p4.RequiredGainK:F2}, Req a = {p4.ParameterA:F2}, Tp = {p4.PeakTimeTp:F2}s";

            var p5 = ControlSpecsSolver.SolveProblem5SpeedPlant(ControlProblem5PoleA, ControlProblem5GainK);
            ControlProblem5SolutionText = $"Closed pole: -{p5.ClosedLoopPoleReal:F2}, DC Sens |Sa^T(0)| = {p5.LowFrequencySensitivity:F4}, ess(Step) = {p5.SteadyStateErrorStep:F4}, ess(Dist) = {p5.SteadyStateErrorDisturbance:F4}";
        }
        catch (Exception ex)
        {
            StatusText = $"Control studio syntax error: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads an AI-generated .fcircuit JSON schematic into the studio canvas and reinitializes solver.
    /// </summary>
    public void LoadCircuitFromLlmJson(string json)
    {
        var imported = LlmCircuitSerializer.Deserialize(json, "AI Generated Circuit");
        CurrentFilePath = null;
        SetCircuit(imported);
        IsLlmDrawerOpen = false;
        CurrentFileName = string.IsNullOrWhiteSpace(imported.Name) ? "ai_circuit.fcircuit" : $"{imported.Name.Trim()}.fcircuit";
        CircuitTitle = $"FryCircuit - {imported.Name}";
        StatusText = $"Successfully imported AI circuit '{imported.Name}' ({imported.Devices.Count} components)";
        ShowAiActivity("AI Schematic Loaded", $"Synthesized '{imported.Name}' ({imported.Devices.Count} components)", "Auto-routed netlists & placed components", "AutoFixHigh");
    }

    /// <summary>
    /// Serializes the currently loaded circuit schematic to .fcircuit JSON.
    /// </summary>
    public string ExportCircuitToLlmJson()
    {
        return CurrentCircuit != null ? LlmCircuitSerializer.Serialize(CurrentCircuit) : string.Empty;
    }

    /// <summary>
    /// Advances the MNA simulation engine by specified steps.
    /// </summary>
    public void StepSimulation(int steps = 10, double dt = 0.0001)
    {
        if (_solver == null || CurrentCircuit == null) return;
        for (int i = 0; i < steps; i++)
        {
            _solver.Step(dt);
        }
        var canvases = _activeCanvases.Concat(CircuitCanvas.ActiveCanvases).Distinct().ToList();
        foreach (var c in canvases)
        {
            c.ParticleEngine.Update(dt * steps, SimulationSpeed);
            c.InvalidateVisual();
        }
        UpdateOscilloscope();
        StatusText = $"Simulation stepped to {_solver.CurrentTime * 1000.0:F2} ms";
    }

    private void SetCircuit(Circuit circuit)
    {
        _isSwitchingCircuit = true;
        try
        {
            if (!string.IsNullOrEmpty(circuit.Name) && !AvailablePresets.Contains(circuit.Name))
            {
                AvailablePresets.Insert(0, circuit.Name);
            }
            CurrentCircuit = circuit;
            circuit.EnsureWiresSynthesized();
            SelectedPreset = circuit.Name;
            _solver = new MnaSolver(circuit)
            {
                TemperatureKelvin = OperatingTemperatureKelvin
            };
            var canvases = _activeCanvases.Concat(CircuitCanvas.ActiveCanvases).Distinct().ToList();
            foreach (var c in canvases)
            {
                c.Circuit = circuit;
                c.ParticleEngine.SyncWithCircuit(circuit);
                c.ZoomToFit();
                c.InvalidateVisual();
            }
            SelectedDevice = circuit.Devices.FirstOrDefault(d => d.Type != "ground") ?? circuit.Devices.FirstOrDefault();
            UpdateTitle();
        }
        finally
        {
            _isSwitchingCircuit = false;
        }
    }

    public void ReinitializeSolver()
    {
        if (CurrentCircuit == null) return;
        _solver = new MnaSolver(CurrentCircuit)
        {
            TemperatureKelvin = OperatingTemperatureKelvin
        };
        var canvases = _activeCanvases.Concat(CircuitCanvas.ActiveCanvases).Distinct().ToList();
        foreach (var c in canvases)
        {
            c.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            c.InvalidateVisual();
        }
    }

    private void LoadDefaultCircuit()
    {
        LoadPreset("Half-Wave Rectifier");
    }

    private void LoadAvailablePresets()
    {
        AvailablePresets.Clear();
        foreach (var name in FryCircuit.Core.Presets.CircuitPresets.PresetNames)
        {
            AvailablePresets.Add(name);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        _timer.Stop();
        _timer.Tick -= OnSimulationTick;
        _aiActivityTimer?.Stop();
        FryCircuit.UI.Theming.CircuitThemeService.Instance.ThemeChanged -= OnThemeChanged;

        if (_historyHandler != null)
        {
            History.HistoryChanged -= _historyHandler;
        }

        if (_pluginContext != null && _settingsHandler != null)
        {
            try
            {
                _pluginContext.GetPluginSettings().Changed -= _settingsHandler;
            }
            catch
            {
                // Ignored
            }
        }

        foreach (var reg in _shortcutRegistrations)
        {
            reg.Dispose();
        }
        _shortcutRegistrations.Clear();

        _activeCanvases.Clear();
        _oscilloscope = null;
    }
}

public record LedColorPresetItem(string Name, string Hex, double Wavelength, string Label);

