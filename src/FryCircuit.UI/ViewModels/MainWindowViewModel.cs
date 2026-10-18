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

namespace FryCircuit.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly FryWorld.Sdk.Plugins.IFryPluginContext? _pluginContext;
    private readonly EventHandler? _historyHandler;
    private readonly Action<string>? _settingsHandler;
    private bool _disposed;
    private MnaSolver? _solver;
    private CircuitCanvas? _canvas;
    private OscilloscopeControl? _oscilloscope;

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
    private string _probedCh1Label = "CH1: Auto (Input)";

    [ObservableProperty]
    private string _probedCh2Label = "CH2: Auto (Output)";

    [ObservableProperty]
    private Circuit? _currentCircuit;

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
    public bool IsProbingCh1 => ActiveToolMode == CircuitToolMode.ProbeCh1;
    public bool IsProbingCh2 => ActiveToolMode == CircuitToolMode.ProbeCh2;

    partial void OnActiveToolModeChanged(CircuitToolMode value)
    {
        OnPropertyChanged(nameof(IsSelectMode));
        OnPropertyChanged(nameof(IsWireMode));
        OnPropertyChanged(nameof(IsCutMode));
        OnPropertyChanged(nameof(IsProbingCh1));
        OnPropertyChanged(nameof(IsProbingCh2));
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
        _canvas?.InvalidateVisual();
    }

    [RelayCommand]
    private void ToggleVoltageAnnotations()
    {
        ShowVoltageAnnotations = !ShowVoltageAnnotations;
        _canvas?.InvalidateVisual();
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

    public IReadOnlyList<PaletteItemViewModel> AllPaletteItems { get; } = new List<PaletteItemViewModel>
    {
        // Passives
        new() { Code = "resistor", Name = "Resistor", Symbol = "R", Category = "Passives", Hotkey = "R", ToolTip = "Passive linear 2-terminal resistor" },
        new() { Code = "capacitor", Name = "Capacitor", Symbol = "C", Category = "Passives", Hotkey = "C", ToolTip = "Passive dual-plate energy storage capacitor" },
        new() { Code = "inductor", Name = "Inductor", Symbol = "L", Category = "Passives", Hotkey = "L", ToolTip = "Passive magnetic flux coil inductor" },
        new() { Code = "potentiometer", Name = "Potentiometer", Symbol = "POT", Category = "Passives", Hotkey = "P", ToolTip = "3-terminal variable voltage divider" },

        // Sources & Ground
        new() { Code = "vsource", Name = "DC Voltage", Symbol = "V", Category = "Sources", Hotkey = "V", ToolTip = "Constant DC voltage power source" },
        new() { Code = "vsrcsine", Name = "AC Sine Source", Symbol = "∿", Category = "Sources", Hotkey = "S", ToolTip = "Sinusoidal AC alternating voltage generator" },
        new() { Code = "csource", Name = "DC Current", Symbol = "I", Category = "Sources", Hotkey = "I", ToolTip = "Constant ideal current generator" },
        new() { Code = "battery", Name = "LiPo Battery", Symbol = "BAT", Category = "Sources", Hotkey = "BAT", ToolTip = "Lithium-Ion / LiPo Electrochemical Cell with Temperature Derating & SOC" },
        new() { Code = "ground", Name = "Ground (0V)", Symbol = "⏚", Category = "Sources", Hotkey = "G", ToolTip = "Earth ground 0V reference rail" },

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
        new() { Code = "ohmmeter", Name = "Ohmmeter", Symbol = "[Ω]", Category = "Meters & Switches", Hotkey = "OHM", ToolTip = "Active 2-terminal resistance measurement probe" }
    };

    public ObservableCollection<PaletteItemViewModel> FilteredPaletteItems { get; } = new();

    public void UpdateFilteredPaletteItems()
    {
        FilteredPaletteItems.Clear();
        var q = ComponentSearchQuery?.Trim();
        bool hasQuery = !string.IsNullOrWhiteSpace(q);

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
    }

    [ObservableProperty]
    private string? _selectedPreset = "Half-Wave Rectifier";

    public ObservableCollection<string> AvailablePresets { get; } = new();

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
        }

        LoadPreset(initialPreset);
        _timer.Start();
    }

    public void AttachControls(CircuitCanvas canvas, OscilloscopeControl oscilloscope)
    {
        _canvas = canvas;
        _oscilloscope = oscilloscope;

        _canvas.HistoryManager = History;
        _canvas.OnNodeProbed = (ch, node) =>
        {
            SetProbedNode(ch, node);
        };

        if (CurrentCircuit != null)
        {
            _canvas.Circuit = CurrentCircuit;
            _canvas.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            _canvas.ZoomToFit();
        }
    }

    public bool IsVoltageSourceSelected => SelectedDevice != null && (SelectedDevice.Type == "vsource" || SelectedDevice.Type == "ac_source" || SelectedDevice.Type == "dc_source");
    public bool IsCurrentSourceSelected => SelectedDevice != null && (SelectedDevice.Type == "csource" || SelectedDevice.Type == "isource");
    public bool IsResistorSelected => SelectedDevice != null && (SelectedDevice.Type == "resistor" || SelectedDevice.Type == "potentiometer");
    public bool IsCapacitorSelected => SelectedDevice != null && SelectedDevice.Type == "capacitor";
    public bool IsInductorSelected => SelectedDevice != null && SelectedDevice.Type == "inductor";
    public bool IsLedSelected => SelectedDevice != null && (SelectedDevice is LedDevice || SelectedDevice.Type == "led");
    public LedDevice? SelectedLed => SelectedDevice as LedDevice;
    public string SelectedLedColorHex => SelectedLed?.ColorHex ?? "#FF2020";
    public string SelectedLedColorName => SelectedLed?.ColorName ?? "Red";
    public double SelectedLedForwardVoltage => SelectedLed?.ForwardVoltage ?? 1.8;
    public bool ShowPrimaryKnob => HasSelectedDevice && !IsLedSelected;

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

        OnPropertyChanged(nameof(SelectedDeviceDisplayName));
        OnPropertyChanged(nameof(SelectedDeviceCategoryName));
        OnPropertyChanged(nameof(ShowPrimaryKnob));
        OnPropertyChanged(nameof(IsVoltageSourceSelected));
        OnPropertyChanged(nameof(IsCurrentSourceSelected));
        OnPropertyChanged(nameof(IsResistorSelected));
        OnPropertyChanged(nameof(IsCapacitorSelected));
        OnPropertyChanged(nameof(IsInductorSelected));
        OnPropertyChanged(nameof(IsLedSelected));
        NotifyLedPropertiesChanged();

        DeviceParameters.Clear();

        if (value == null) return;

        var specs = DeviceRegistry.GetParameters(value);
        foreach (var pSpec in specs)
        {
            DeviceParameters.Add(new DeviceParameterViewModel(value, pSpec, () =>
            {
                _canvas?.InvalidateVisual();
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
    public void ProbeSelectedWireCh1()
    {
        if (SelectedWire == null) return;
        ProbedNodeCh1 = SelectedWire.NodeId;
        if (_canvas != null) _canvas.ProbedNodeCh1 = SelectedWire.NodeId;
        StatusText = $"CH1 (Gold) Probing Node #{SelectedWire.NodeId}";
        _canvas?.InvalidateVisual();
    }

    [RelayCommand]
    public void ProbeSelectedWireCh2()
    {
        if (SelectedWire == null) return;
        ProbedNodeCh2 = SelectedWire.NodeId;
        if (_canvas != null) _canvas.ProbedNodeCh2 = SelectedWire.NodeId;
        StatusText = $"CH2 (Cyan) Probing Node #{SelectedWire.NodeId}";
        _canvas?.InvalidateVisual();
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

        _canvas?.InvalidateVisual();
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

        _canvas?.ParticleEngine.Update(1.0 / 60.0, SimulationSpeed);
        if (HasSelectedWire)
        {
            UpdateSelectedWireMeasurements();
        }
        UpdateOscilloscope();
        _canvas?.InvalidateVisual();
    }

    private void UpdateOscilloscope()
    {
        if (_oscilloscope == null || CurrentCircuit == null) return;

        // Channel 1: Probed node or input voltage/node 1
        double ch1 = 0.0;
        if (ProbedNodeCh1.HasValue && CurrentCircuit.Nodes.TryGetValue(ProbedNodeCh1.Value, out var pn1))
        {
            ch1 = pn1.Voltage;
        }
        else
        {
            var vsrc = CurrentCircuit.Devices.OfType<VoltageSourceDevice>().FirstOrDefault();
            if (vsrc != null)
            {
                ch1 = vsrc.VoltageDrop;
            }
            else if (CurrentCircuit.Nodes.TryGetValue(1, out var n1))
            {
                ch1 = n1.Voltage;
            }
        }

        // Channel 2: Probed node or output voltage/node 2
        double ch2 = 0.0;
        if (ProbedNodeCh2.HasValue && CurrentCircuit.Nodes.TryGetValue(ProbedNodeCh2.Value, out var pn2))
        {
            ch2 = pn2.Voltage;
        }
        else
        {
            var loadRes = CurrentCircuit.Devices.OfType<ResistorDevice>().LastOrDefault();
            if (loadRes != null)
            {
                ch2 = loadRes.VoltageDrop;
            }
            else if (CurrentCircuit.Nodes.TryGetValue(2, out var n2))
            {
                ch2 = n2.Voltage;
            }
        }

        _oscilloscope.PushSamples(ch1, ch2);
    }

    [RelayCommand]
    public void Undo()
    {
        if (History.Undo())
        {
            if (CurrentCircuit != null) _canvas?.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            _canvas?.InvalidateVisual();
            StatusText = $"Undone: {History.NextRedoDescription ?? "action"}";
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (History.Redo())
        {
            if (CurrentCircuit != null) _canvas?.ParticleEngine.SyncWithCircuit(CurrentCircuit);
            _canvas?.InvalidateVisual();
            StatusText = $"Redone: {History.NextUndoDescription ?? "action"}";
        }
    }

    [RelayCommand]
    public void StartProbing(string ch)
    {
        var mode = ch == "1" ? CircuitToolMode.ProbeCh1 : CircuitToolMode.ProbeCh2;
        ActiveToolMode = mode;
        if (_canvas != null)
        {
            _canvas.ToolMode = mode;
            _canvas.PlacingDeviceType = null;
        }
        StatusText = $"Probe Mode: Click any pin, wire or component to attach Oscilloscope Channel {ch}. (Esc to cancel)";
    }

    [RelayCommand]
    public void ClearProbes()
    {
        ProbedNodeCh1 = null;
        ProbedNodeCh2 = null;
        ProbedCh1Label = "CH1: Auto (Input)";
        ProbedCh2Label = "CH2: Auto (Output)";
        if (_canvas != null)
        {
            _canvas.ProbedNodeCh1 = null;
            _canvas.ProbedNodeCh2 = null;
            _canvas.InvalidateVisual();
        }
        StatusText = "Cleared oscilloscope probes to Auto";
    }

    public void SetProbedNode(int channel, int nodeId)
    {
        if (channel == 1)
        {
            ProbedNodeCh1 = nodeId;
            ProbedCh1Label = $"CH1: Probed Node #{nodeId}";
            StatusText = $"Oscilloscope CH1 attached to Node #{nodeId}";
        }
        else
        {
            ProbedNodeCh2 = nodeId;
            ProbedCh2Label = $"CH2: Probed Node #{nodeId}";
            StatusText = $"Oscilloscope CH2 attached to Node #{nodeId}";
        }
        ActiveToolMode = CircuitToolMode.Select;
        if (_canvas != null)
        {
            _canvas.ToolMode = CircuitToolMode.Select;
            _canvas.InvalidateVisual();
        }
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
        if (_canvas != null)
        {
            _canvas.ToolMode = mode;
            _canvas.PlacingDeviceType = null;
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
        if (_canvas != null)
        {
            _canvas.ToolMode = CircuitToolMode.Select;
            _canvas.PlacingDeviceType = type;
        }

        StatusText = $"Placing {type.ToUpperInvariant()} - Click on canvas to drop. Press 'R' to rotate, Esc to cancel.";
    }

    [RelayCommand]
    public void RotateSelected()
    {
        _canvas?.RotateSelected(clockwise: true);
        StatusText = "Rotated 90° Clockwise";
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        _canvas?.DeleteSelected();
        StatusText = "Deleted selection";
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

        _canvas?.InvalidateVisual();
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
            _canvas?.InvalidateVisual();
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
        _canvas?.ParticleEngine.Update(0.00025, SimulationSpeed);
        UpdateOscilloscope();
        _canvas?.InvalidateVisual();
        StatusText = $"Stepped to {_solver.CurrentTime * 1000.0:F2} ms";
    }

    [RelayCommand]
    private void ResetSimulation()
    {
        _solver?.Reset();
        _canvas?.InvalidateVisual();
        StatusText = "Simulation Reset to 0.0 ms";
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        _canvas?.ZoomToFit();
        StatusText = "View centered to fit circuit";
    }

    partial void OnSelectedPresetChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && value != CurrentCircuit?.Name)
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
                    new("FryCircuit Schematic (*.frycircuit; *.circuit)")
                    {
                        Patterns = new[] { "*.frycircuit", "*.circuit", "*.xml" }
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
                ? (string.IsNullOrWhiteSpace(CurrentCircuit.Name) ? "circuit.frycircuit" : $"{CurrentCircuit.Name.Replace(" ", "_").ToLowerInvariant()}.frycircuit")
                : Path.GetFileName(CurrentFilePath);

            if (!suggested.EndsWith(".frycircuit", StringComparison.OrdinalIgnoreCase) &&
                !suggested.EndsWith(".circuit", StringComparison.OrdinalIgnoreCase))
            {
                suggested += ".frycircuit";
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Circuit As",
                DefaultExtension = "frycircuit",
                SuggestedFileName = suggested,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("FryCircuit File (*.frycircuit)")
                    {
                        Patterns = new[] { "*.frycircuit" }
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
                string xml = CircuitSerializer.Serialize(CurrentCircuit);
                await using (var stream = await file.OpenWriteAsync())
                await using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
                {
                    await writer.WriteAsync(xml);
                }

                CurrentFilePath = localPath;
                IsDirty = false;
                StatusText = $"Saved circuit to {Path.GetFileName(localPath)}";
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
            CircuitSerializer.SaveToFile(CurrentCircuit, filePath);
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
            var circuit = CircuitSerializer.LoadFromFile(filePath);
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

    public string PlayPauseButtonText => IsSimulating ? "⏸ Pause" : "▶ Play";

    partial void OnIsSimulatingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseButtonText));
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

    private void SetCircuit(Circuit circuit)
    {
        CurrentCircuit = circuit;
        circuit.EnsureWiresSynthesized();
        SelectedPreset = circuit.Name;
        _solver = new MnaSolver(circuit)
        {
            TemperatureKelvin = OperatingTemperatureKelvin
        };
        if (_canvas != null)
        {
            _canvas.Circuit = circuit;
            _canvas.ParticleEngine.SyncWithCircuit(circuit);
            _canvas.ZoomToFit();
        }
        SelectedDevice = circuit.Devices.FirstOrDefault(d => d.Type != "ground") ?? circuit.Devices.FirstOrDefault();
        UpdateTitle();
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

        _timer.Stop();
        _timer.Tick -= OnSimulationTick;

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

        _canvas = null;
        _oscilloscope = null;
    }
}

public record LedColorPresetItem(string Name, string Hex, double Wavelength, string Label);

