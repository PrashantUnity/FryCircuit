using System.Collections.ObjectModel;
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
    private string _selectedPaletteCategory = "All";

    public bool IsPassivesVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Passives";
    public bool IsSourcesVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Sources";
    public bool IsSemiconductorsVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Semiconductors";
    public bool IsQuantumVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Quantum & RF";
    public bool IsLogicVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Logic & ICs";
    public bool IsMetersVisible => SelectedPaletteCategory == "All" || SelectedPaletteCategory == "Meters & Switches";

    [RelayCommand]
    public void SelectPaletteCategory(string category)
    {
        SelectedPaletteCategory = category;
        OnPropertyChanged(nameof(IsPassivesVisible));
        OnPropertyChanged(nameof(IsSourcesVisible));
        OnPropertyChanged(nameof(IsSemiconductorsVisible));
        OnPropertyChanged(nameof(IsQuantumVisible));
        OnPropertyChanged(nameof(IsLogicVisible));
        OnPropertyChanged(nameof(IsMetersVisible));
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
        };
        History.HistoryChanged += _historyHandler;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // 60 FPS
        };
        _timer.Tick += OnSimulationTick;

        LoadAvailablePresets();

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

    partial void OnSelectedDeviceChanged(Device? value)
    {
        HasSelectedDevice = value != null;
        if (value != null)
        {
            SelectedWire = null;
            HasSelectedWire = false;
        }

        OnPropertyChanged(nameof(IsVoltageSourceSelected));
        OnPropertyChanged(nameof(IsCurrentSourceSelected));
        OnPropertyChanged(nameof(IsResistorSelected));
        OnPropertyChanged(nameof(IsCapacitorSelected));
        OnPropertyChanged(nameof(IsInductorSelected));

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
    public void SaveCircuit(string filePath)
    {
        if (CurrentCircuit == null) return;
        try
        {
            CircuitSerializer.SaveToFile(CurrentCircuit, filePath);
            StatusText = $"Saved circuit to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenCircuit(string filePath)
    {
        try
        {
            var circuit = CircuitSerializer.LoadFromFile(filePath);
            SetCircuit(circuit);
            CircuitTitle = $"FryCircuit - {circuit.Name}";
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
