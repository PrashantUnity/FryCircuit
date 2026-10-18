using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.History;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using FryCircuit.UI.Rendering;

namespace FryCircuit.UI.Controls;

public enum CircuitToolMode
{
    Select,
    Wire,
    Cut,
    Pan,
    ProbeCh1,
    ProbeCh2
}

public class CircuitCanvas : Control
{
    public static readonly StyledProperty<Circuit?> CircuitProperty =
        AvaloniaProperty.Register<CircuitCanvas, Circuit?>(nameof(Circuit));

    public static readonly StyledProperty<Device?> SelectedDeviceProperty =
        AvaloniaProperty.Register<CircuitCanvas, Device?>(nameof(SelectedDevice), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<CircuitWire?> SelectedWireProperty =
        AvaloniaProperty.Register<CircuitCanvas, CircuitWire?>(nameof(SelectedWire), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<CircuitToolMode> ToolModeProperty =
        AvaloniaProperty.Register<CircuitCanvas, CircuitToolMode>(nameof(ToolMode), defaultValue: CircuitToolMode.Select);

    public static readonly StyledProperty<string?> PlacingDeviceTypeProperty =
        AvaloniaProperty.Register<CircuitCanvas, string?>(nameof(PlacingDeviceType));

    public static readonly StyledProperty<bool> ShowCurrentAnnotationsProperty =
        AvaloniaProperty.Register<CircuitCanvas, bool>(nameof(ShowCurrentAnnotations), defaultValue: true);

    public static readonly StyledProperty<bool> ShowVoltageAnnotationsProperty =
        AvaloniaProperty.Register<CircuitCanvas, bool>(nameof(ShowVoltageAnnotations), defaultValue: true);

    public Circuit? Circuit
    {
        get => GetValue(CircuitProperty);
        set => SetValue(CircuitProperty, value);
    }

    public Device? SelectedDevice
    {
        get => GetValue(SelectedDeviceProperty);
        set => SetValue(SelectedDeviceProperty, value);
    }

    public CircuitWire? SelectedWire
    {
        get => GetValue(SelectedWireProperty);
        set => SetValue(SelectedWireProperty, value);
    }

    public CircuitToolMode ToolMode
    {
        get => GetValue(ToolModeProperty);
        set => SetValue(ToolModeProperty, value);
    }

    public string? PlacingDeviceType
    {
        get => GetValue(PlacingDeviceTypeProperty);
        set => SetValue(PlacingDeviceTypeProperty, value);
    }

    public bool ShowCurrentAnnotations
    {
        get => GetValue(ShowCurrentAnnotationsProperty);
        set => SetValue(ShowCurrentAnnotationsProperty, value);
    }

    public bool ShowVoltageAnnotations
    {
        get => GetValue(ShowVoltageAnnotationsProperty);
        set => SetValue(ShowVoltageAnnotationsProperty, value);
    }

    public ParticleFlowEngine ParticleEngine { get; } = new();

    // Canvas navigation
    public double PanX { get; set; } = 200.0;
    public double PanY { get; set; } = 150.0;
    public double Zoom { get; set; } = 1.0;
    public double GridSize { get; set; } = 50.0;

    private bool _needsZoomToFit = true;
    private bool _isPanning;
    private Point _lastPanPoint;

    // Device dragging
    private bool _isDraggingDevice;
    private Device? _draggedDevice;
    private Point _deviceDragOffset;
    private (double X, double Y) _deviceDragStartPos;
    private bool _isDragCollision;

    // Undo / Redo Manager reference
    public UndoRedoManager? HistoryManager { get; set; }

    // Oscilloscope Interactive Probing
    public int? ProbedNodeCh1 { get; set; }
    public int? ProbedNodeCh2 { get; set; }
    public Action<int, int>? OnNodeProbed { get; set; }
    private (double Wx, double Wy) _probeCursorPos;

    // Interactive Wire Connection
    private bool _isWiring;
    private Device? _wiringStartDevice;
    private int _wiringStartPinIndex;
    private (double Wx, double Wy) _wiringCursorPos;
    private (Device Device, Pin Pin, int PinIndex)? _snapTargetPin;
    private CircuitWire? _snapTargetWire;

    // Topology and Solder Junction Tracking for Annotations
    private Dictionary<(double X, double Y), HashSet<(int dx, int dy)>> _lastDirectionsAtPoint = new();
    private Dictionary<(double X, double Y), int> _lastJunctionNodeIds = new();

    // Device Placement preview
    private (double Wx, double Wy) _placementCursorPos;
    private DeviceRotation _placementRotation = DeviceRotation.Up;

    // Multi-Selection support
    public HashSet<Device> SelectedDevices { get; } = new();
    public HashSet<CircuitWire> SelectedWires { get; } = new();

    // Marquee / Box Selection
    private bool _isMarqueeSelecting;
    private Point _marqueeStart;
    private Point _marqueeEnd;
    private Dictionary<Device, (double X, double Y)> _multiDragStartPositions = new();

    // Hover and Navigation state
    private bool _isSpaceDown;
    private (Device Device, Pin Pin, int PinIndex)? _hoveredPin;
    private CircuitWire? _hoveredWire;
    private (double Wx, double Wy) _lastCursorWorldPos;

    static CircuitCanvas()
    {
        AffectsRender<CircuitCanvas>(
            CircuitProperty,
            SelectedDeviceProperty,
            SelectedWireProperty,
            ToolModeProperty,
            PlacingDeviceTypeProperty,
            ShowCurrentAnnotationsProperty,
            ShowVoltageAnnotationsProperty);
    }

    public CircuitCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        FryCircuit.UI.Theming.CircuitThemeService.Instance.TokensChanged += _ => InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 50 && e.NewSize.Height > 50)
        {
            ZoomToFit();
        }
    }

    public Point WorldToScreen(double wx, double wy)
    {
        return new Point(PanX + wx * GridSize * Zoom, PanY + wy * GridSize * Zoom);
    }

    public (double X, double Y) ScreenToWorld(Point screen)
    {
        double wx = (screen.X - PanX) / (GridSize * Zoom);
        double wy = (screen.Y - PanY) / (GridSize * Zoom);
        return (wx, wy);
    }

    public void ZoomToFit()
    {
        if (Circuit == null || Circuit.Devices.Count == 0) return;

        double minX = Circuit.Devices.Min(d => d.LocationX);
        double maxX = Circuit.Devices.Max(d => d.LocationX);
        double minY = Circuit.Devices.Min(d => d.LocationY);
        double maxY = Circuit.Devices.Max(d => d.LocationY);

        if (Circuit.Wires.Count > 0)
        {
            foreach (var wire in Circuit.Wires)
            {
                foreach (var pt in wire.PathPoints)
                {
                    minX = Math.Min(minX, pt.X);
                    maxX = Math.Max(maxX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
        }

        double spanX = Math.Max(8, maxX - minX + 6.0);
        double spanY = Math.Max(6, maxY - minY + 4.5);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 50 || h < 50)
        {
            _needsZoomToFit = true;
            return;
        }
        _needsZoomToFit = false;

        Zoom = Math.Clamp(Math.Min(w / (spanX * GridSize), h / (spanY * GridSize)), 0.65, 1.4);
        PanX = (w / 2.0) - ((minX + maxX) / 2.0) * GridSize * Zoom;
        PanY = (h / 2.0) - ((minY + maxY) / 2.0) * GridSize * Zoom;

        InvalidateVisual();
    }

    public void RotateSelected(bool clockwise = true)
    {
        if (SelectedDevices.Count > 0)
        {
            var actions = new List<IUndoableAction>();
            int step = clockwise ? 90 : 270;
            foreach (var dev in SelectedDevices)
            {
                var oldRot = dev.Transform.Rotation;
                int cur = (int)oldRot;
                var newRot = (DeviceRotation)((cur + step) % 360);
                dev.Transform.Rotation = newRot;
                actions.Add(new RotateDeviceAction(dev, oldRot, newRot));
            }

            if (actions.Count == 1)
                HistoryManager?.Record(actions[0]);
            else if (actions.Count > 1)
                HistoryManager?.Record(new BatchCircuitAction($"Rotate {actions.Count} components", actions));

            if (Circuit != null) ParticleEngine.SyncWithCircuit(Circuit);
            InvalidateVisual();
        }
        else if (SelectedDevice != null)
        {
            var oldRot = SelectedDevice.Transform.Rotation;
            int cur = (int)oldRot;
            int step = clockwise ? 90 : 270;
            var newRot = (DeviceRotation)((cur + step) % 360);
            SelectedDevice.Transform.Rotation = newRot;
            HistoryManager?.Record(new RotateDeviceAction(SelectedDevice, oldRot, newRot));
            if (Circuit != null) ParticleEngine.SyncWithCircuit(Circuit);
            InvalidateVisual();
        }
        else if (!string.IsNullOrEmpty(PlacingDeviceType))
        {
            int cur = (int)_placementRotation;
            _placementRotation = (DeviceRotation)((cur + 90) % 360);
            InvalidateVisual();
        }
    }

    public void DeleteSelected()
    {
        if (Circuit == null) return;

        var actions = new List<IUndoableAction>();

        // Delete selected wires
        var wiresToDelete = SelectedWires.ToList();
        if (SelectedWire != null && !wiresToDelete.Contains(SelectedWire))
        {
            wiresToDelete.Add(SelectedWire);
        }

        foreach (var wire in wiresToDelete)
        {
            actions.Add(new RemoveWireAction(Circuit, wire));
            Circuit.RemoveWire(wire);
        }
        SelectedWires.Clear();
        SelectedWire = null;

        // Delete selected devices and their attached wires
        var devsToDelete = SelectedDevices.ToList();
        if (SelectedDevice != null && !devsToDelete.Contains(SelectedDevice))
        {
            devsToDelete.Add(SelectedDevice);
        }

        foreach (var dev in devsToDelete)
        {
            var attachedWires = Circuit.Wires.Where(w => w.FromDeviceId == dev.Id || w.ToDeviceId == dev.Id).ToList();
            actions.Add(new RemoveDeviceAction(Circuit, dev, attachedWires));
            Circuit.RemoveDevice(dev);
        }
        SelectedDevices.Clear();
        SelectedDevice = null;

        if (actions.Count == 1)
            HistoryManager?.Record(actions[0]);
        else if (actions.Count > 1)
            HistoryManager?.Record(new BatchCircuitAction($"Delete {actions.Count} elements", actions));

        ParticleEngine.SyncWithCircuit(Circuit);
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var mousePos = e.GetPosition(this);
        var (worldX, worldY) = ScreenToWorld(mousePos);

        // macOS trackpad pinch-to-zoom sends Ctrl modifier, or user holding Ctrl/Cmd key
        bool isPinchOrZoom = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (isPinchOrZoom)
        {
            double factor = e.Delta.Y > 0 ? 1.15 : 0.85;
            double newZoom = Math.Clamp(Zoom * factor, 0.15, 6.0);

            PanX = mousePos.X - worldX * GridSize * newZoom;
            PanY = mousePos.Y - worldY * GridSize * newZoom;
            Zoom = newZoom;
        }
        else
        {
            // Smooth infinite canvas panning:
            // Two-finger trackpad or wheel scroll in X and Y
            double scrollFactor = 22.0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Shift + Wheel = horizontal scroll
                PanX += e.Delta.Y * scrollFactor;
            }
            else
            {
                PanX += e.Delta.X * scrollFactor;
                PanY += e.Delta.Y * scrollFactor;
            }
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);
        var pos = point.Position;
        var (wx, wy) = ScreenToWorld(pos);

        // Spacebar + Left Click Pan
        if (_isSpaceDown && point.Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = pos;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // Right button: cancel current wiring/placement or pan
        if (point.Properties.IsRightButtonPressed)
        {
            if (_isWiring)
            {
                _isWiring = false;
                _wiringStartDevice = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            if (!string.IsNullOrEmpty(PlacingDeviceType))
            {
                PlacingDeviceType = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            _isPanning = true;
            _lastPanPoint = pos;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = pos;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed && Circuit != null)
        {
            // 0. OSCILLOSCOPE PROBE MODE
            if (ToolMode == CircuitToolMode.ProbeCh1 || ToolMode == CircuitToolMode.ProbeCh2)
            {
                int targetNode = -1;
                var pTest = Circuit.FindPinAt(wx, wy, tolerance: 0.6);
                if (pTest != null)
                {
                    targetNode = pTest.Value.Pin.NodeId;
                }
                else
                {
                    var wTest = FindWireAt(wx, wy, tolerance: 0.5);
                    if (wTest != null)
                    {
                        targetNode = wTest.NodeId;
                    }
                    else
                    {
                        var dTest = FindDeviceAt(wx, wy);
                        if (dTest != null && dTest.Pins.Count > 0)
                        {
                            targetNode = dTest.Pins[0].NodeId;
                        }
                    }
                }

                if (targetNode >= 0)
                {
                    int ch = (ToolMode == CircuitToolMode.ProbeCh1) ? 1 : 2;
                    if (ch == 1) ProbedNodeCh1 = targetNode;
                    else ProbedNodeCh2 = targetNode;

                    OnNodeProbed?.Invoke(ch, targetNode);
                    ToolMode = CircuitToolMode.Select;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            // 1. PLACEMENT MODE: User clicked on canvas to drop new component
            if (!string.IsNullOrEmpty(PlacingDeviceType))
            {
                double placeX = Math.Round(wx);
                double placeY = Math.Round(wy);

                int nextId = (Circuit.Devices.Count > 0 ? Circuit.Devices.Max(d => d.Id) : 0) + 1;
                var newDev = DeviceFactory.CreateDevice(PlacingDeviceType, nextId, placeX, placeY);
                newDev.Transform = new DeviceTransform(_placementRotation);

                int maxNodeId = Circuit.Nodes.Count > 0 ? Circuit.Nodes.Keys.Max() : 0;
                foreach (var pin in newDev.Pins)
                {
                    pin.NodeId = ++maxNodeId;
                    Circuit.GetOrCreateNode(pin.NodeId, isGround: newDev.Type == "ground");
                }

                Circuit.AddDevice(newDev);

                // Auto-connect pins dropped directly onto existing pins or wires
                for (int i = 0; i < newDev.Pins.Count; i++)
                {
                    var (px, py) = newDev.GetAbsolutePinPosition(i);
                    // 1. Check if dropped onto an existing pin of another device
                    var existingPinHit = Circuit.FindPinAt(px, py, tolerance: 0.35);
                    if (existingPinHit != null && existingPinHit.Value.Device.Id != newDev.Id)
                    {
                        var createdWire = Circuit.ConnectPins(newDev, i, existingPinHit.Value.Device, existingPinHit.Value.PinIndex);
                        HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                        continue;
                    }

                    // 2. Check if dropped onto an existing wire
                    var wireHit = FindWireAt(px, py, tolerance: 0.45);
                    if (wireHit != null)
                    {
                        var createdWire = Circuit.ConnectPinToWire(newDev, i, wireHit);
                        if (createdWire != null)
                        {
                            HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                        }
                    }
                }

                // For 1-pin devices like Ground, also check if placed near a wire
                if (newDev.Pins.Count == 1 && newDev.Type == "ground")
                {
                    var (gx, gy) = newDev.GetAbsolutePinPosition(0);
                    var gWireHit = FindWireAt(gx, gy, tolerance: 0.65);
                    if (gWireHit != null && !Circuit.Wires.Any(w => w.ConnectsToPin(newDev.Id, 0)))
                    {
                        var createdWire = Circuit.ConnectPinToWire(newDev, 0, gWireHit);
                        if (createdWire != null)
                        {
                            HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                        }
                    }
                }

                HistoryManager?.Record(new AddDeviceAction(Circuit, newDev));
                SelectedDevice = newDev;
                SelectedWire = null;
                PlacingDeviceType = null;
                ParticleEngine.SyncWithCircuit(Circuit);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // 2. CHECK PIN AND WIRE HIT TESTING (FOR CONNECTING WIRES)
            var pinHit = Circuit.FindPinAt(wx, wy, tolerance: 0.55);

            if (_isWiring)
            {
                if (_snapTargetPin != null)
                {
                    if (_wiringStartDevice != null &&
                        !(ReferenceEquals(_wiringStartDevice, _snapTargetPin.Value.Device) && _wiringStartPinIndex == _snapTargetPin.Value.PinIndex))
                    {
                        var createdWire = Circuit.ConnectPins(_wiringStartDevice, _wiringStartPinIndex, _snapTargetPin.Value.Device, _snapTargetPin.Value.PinIndex);
                        HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                        ParticleEngine.SyncWithCircuit(Circuit);
                    }
                }
                else if (pinHit != null)
                {
                    if (_wiringStartDevice != null &&
                        !(ReferenceEquals(_wiringStartDevice, pinHit.Value.Device) && _wiringStartPinIndex == pinHit.Value.PinIndex))
                    {
                        var createdWire = Circuit.ConnectPins(_wiringStartDevice, _wiringStartPinIndex, pinHit.Value.Device, pinHit.Value.PinIndex);
                        HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                        ParticleEngine.SyncWithCircuit(Circuit);
                    }
                }
                else if (_snapTargetWire != null)
                {
                    if (_wiringStartDevice != null)
                    {
                        var createdWire = Circuit.ConnectPinToWire(_wiringStartDevice, _wiringStartPinIndex, _snapTargetWire);
                        if (createdWire != null)
                        {
                            HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                            ParticleEngine.SyncWithCircuit(Circuit);
                        }
                    }
                }
                else
                {
                    var wireHit = FindWireAt(wx, wy, tolerance: 0.5);
                    if (wireHit != null && _wiringStartDevice != null)
                    {
                        var createdWire = Circuit.ConnectPinToWire(_wiringStartDevice, _wiringStartPinIndex, wireHit);
                        if (createdWire != null)
                        {
                            HistoryManager?.Record(new AddWireAction(Circuit, createdWire));
                            ParticleEngine.SyncWithCircuit(Circuit);
                        }
                    }
                }

                _isWiring = false;
                _wiringStartDevice = null;
                _snapTargetPin = null;
                _snapTargetWire = null;
                e.Pointer.Capture(null);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (ToolMode == CircuitToolMode.Wire || pinHit != null)
            {
                if (pinHit != null)
                {
                    // Start wire connection from this pin!
                    _isWiring = true;
                    _wiringStartDevice = pinHit.Value.Device;
                    _wiringStartPinIndex = pinHit.Value.PinIndex;
                    var (pX, pY) = pinHit.Value.Device.GetAbsolutePinPosition(pinHit.Value.PinIndex);
                    _wiringCursorPos = (pX, pY);
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                else if (ToolMode == CircuitToolMode.Wire)
                {
                    var wireHit = FindWireAt(wx, wy, tolerance: 0.45);
                    if (wireHit != null)
                    {
                        var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wireHit.FromDeviceId);
                        var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wireHit.ToDeviceId);
                        if (dev1 != null && dev2 != null)
                        {
                            var pos1 = dev1.GetAbsolutePinPosition(wireHit.FromPinIndex);
                            var pos2 = dev2.GetAbsolutePinPosition(wireHit.ToPinIndex);
                            double d1 = (pos1.X - wx) * (pos1.X - wx) + (pos1.Y - wy) * (pos1.Y - wy);
                            double d2 = (pos2.X - wx) * (pos2.X - wx) + (pos2.Y - wy) * (pos2.Y - wy);
                            var startDev = d1 <= d2 ? dev1 : dev2;
                            int startPin = d1 <= d2 ? wireHit.FromPinIndex : wireHit.ToPinIndex;

                            _isWiring = true;
                            _wiringStartDevice = startDev;
                            _wiringStartPinIndex = startPin;
                            _wiringCursorPos = GetClosestPointOnWire(wireHit, wx, wy);
                            e.Pointer.Capture(this);
                            InvalidateVisual();
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            // 3. CUT TOOL MODE: Disconnect pin, cut wire, or delete device
            if (ToolMode == CircuitToolMode.Cut)
            {
                if (pinHit != null)
                {
                    var dev = pinHit.Value.Device;
                    int pIdx = pinHit.Value.PinIndex;
                    var attachedWires = Circuit.Wires.Where(w =>
                        (w.FromDeviceId == dev.Id && w.FromPinIndex == pIdx) ||
                        (w.ToDeviceId == dev.Id && w.ToPinIndex == pIdx)).ToList();
                    foreach (var w in attachedWires)
                    {
                        HistoryManager?.Record(new RemoveWireAction(Circuit, w));
                    }
                    Circuit.DisconnectPin(dev, pIdx);
                    ParticleEngine.SyncWithCircuit(Circuit);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                var wireHit = FindWireAt(wx, wy, tolerance: 0.5);
                if (wireHit != null)
                {
                    HistoryManager?.Record(new RemoveWireAction(Circuit, wireHit));
                    Circuit.RemoveWire(wireHit);
                    ParticleEngine.SyncWithCircuit(Circuit);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                var devHit = FindDeviceAt(wx, wy);
                if (devHit != null)
                {
                    var attached = Circuit.Wires.Where(w => w.FromDeviceId == devHit.Id || w.ToDeviceId == devHit.Id).ToList();
                    HistoryManager?.Record(new RemoveDeviceAction(Circuit, devHit, attached));
                    Circuit.RemoveDevice(devHit);
                    ParticleEngine.SyncWithCircuit(Circuit);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            // 4. SELECT / MOVE MODE:
            // Check if clicked any Device
            var clickedDev = FindDeviceAt(wx, wy);
            if (clickedDev != null)
            {
                // Check if device handles interaction (e.g. switch toggle)
                if (DeviceRegistry.HandleClick(clickedDev))
                {
                    SelectedDevice = clickedDev;
                    SelectedWire = null;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                bool isMultiModifier = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
                if (isMultiModifier)
                {
                    if (SelectedDevices.Contains(clickedDev))
                    {
                        SelectedDevices.Remove(clickedDev);
                        SelectedDevice = SelectedDevices.FirstOrDefault();
                    }
                    else
                    {
                        SelectedDevices.Add(clickedDev);
                        SelectedDevice = clickedDev;
                    }
                }
                else
                {
                    if (!SelectedDevices.Contains(clickedDev))
                    {
                        SelectedDevices.Clear();
                        SelectedWires.Clear();
                        SelectedDevices.Add(clickedDev);
                    }
                    SelectedDevice = clickedDev;
                }

                SelectedWire = null;
                _isDraggingDevice = true;
                _draggedDevice = clickedDev;
                _deviceDragStartPos = (clickedDev.LocationX, clickedDev.LocationY);
                _deviceDragOffset = new Point(clickedDev.LocationX - wx, clickedDev.LocationY - wy);
                _multiDragStartPositions = SelectedDevices.ToDictionary(d => d, d => (d.LocationX, d.LocationY));
                e.Pointer.Capture(this);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Check if clicked a Wire
            var clickedWire = FindWireAt(wx, wy, tolerance: 0.4);
            if (clickedWire != null)
            {
                SelectedWire = clickedWire;
                SelectedDevice = null;
                SelectedDevices.Clear();
                SelectedWires.Clear();
                SelectedWires.Add(clickedWire);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Clicked empty space -> start Marquee Box Multi-Selection
            bool isShiftOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            if (!isShiftOrCtrl)
            {
                SelectedDevice = null;
                SelectedWire = null;
                SelectedDevices.Clear();
                SelectedWires.Clear();
            }

            _isMarqueeSelecting = true;
            _marqueeStart = pos;
            _marqueeEnd = pos;
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var curPos = e.GetPosition(this);
        var (wx, wy) = ScreenToWorld(curPos);
        _lastCursorWorldPos = (wx, wy);

        if (_isPanning)
        {
            PanX += curPos.X - _lastPanPoint.X;
            PanY += curPos.Y - _lastPanPoint.Y;
            _lastPanPoint = curPos;
            InvalidateVisual();
            return;
        }

        if (_isMarqueeSelecting)
        {
            _marqueeEnd = curPos;
            InvalidateVisual();
            return;
        }

        if (_isDraggingDevice && _draggedDevice != null)
        {
            double targetX = Math.Round(wx + _deviceDragOffset.X);
            double targetY = Math.Round(wy + _deviceDragOffset.Y);

            _isDragCollision = Circuit != null && Circuit.Devices.Any(d =>
                !ReferenceEquals(d, _draggedDevice) &&
                !SelectedDevices.Contains(d) &&
                Math.Abs(d.LocationX - targetX) < 0.6 &&
                Math.Abs(d.LocationY - targetY) < 0.6);

            if (_draggedDevice.LocationX != targetX || _draggedDevice.LocationY != targetY)
            {
                double dx = targetX - _deviceDragStartPos.X;
                double dy = targetY - _deviceDragStartPos.Y;

                if (SelectedDevices.Contains(_draggedDevice) && SelectedDevices.Count > 1)
                {
                    foreach (var kvp in _multiDragStartPositions)
                    {
                        kvp.Key.LocationX = Math.Round(kvp.Value.X + dx);
                        kvp.Key.LocationY = Math.Round(kvp.Value.Y + dy);
                    }
                }
                else
                {
                    _draggedDevice.LocationX = targetX;
                    _draggedDevice.LocationY = targetY;
                }

                if (Circuit != null)
                {
                    var movedDevs = SelectedDevices.Contains(_draggedDevice) && SelectedDevices.Count > 1
                        ? (IEnumerable<Device>)SelectedDevices
                        : new[] { _draggedDevice };

                    // Auto-connect any unconnected pins that land on an existing wire or pin
                    foreach (var dev in movedDevs)
                    {
                        for (int i = 0; i < dev.Pins.Count; i++)
                        {
                            if (!Circuit.Wires.Any(w => w.ConnectsToPin(dev.Id, i)))
                            {
                                var (px, py) = dev.GetAbsolutePinPosition(i);
                                var existingPinHit = Circuit.FindPinAt(px, py, tolerance: 0.35);
                                if (existingPinHit != null && existingPinHit.Value.Device.Id != dev.Id)
                                {
                                    Circuit.ConnectPins(dev, i, existingPinHit.Value.Device, existingPinHit.Value.PinIndex);
                                    continue;
                                }

                                double wireTol = (dev.Pins.Count == 1 && dev.Type == "ground") ? 0.65 : 0.45;
                                var wireHit = FindWireAt(px, py, tolerance: wireTol);
                                if (wireHit != null)
                                {
                                    Circuit.ConnectPinToWire(dev, i, wireHit);
                                }
                            }
                        }
                    }

                    var affectedNodes = movedDevs.SelectMany(d => d.Pins.Select(p => p.NodeId)).Distinct().Where(n => n >= 0);
                    foreach (var nid in affectedNodes)
                    {
                        Circuit.ResynthesizeNode(nid);
                    }
                    Circuit.RouteAllWires();
                    ParticleEngine.SyncWithCircuit(Circuit);
                }
                InvalidateVisual();
            }
            return;
        }

        if (_isWiring)
        {
            // 1. Check if hovering near a valid target pin
            var targetPin = Circuit?.FindPinAt(wx, wy, tolerance: 0.55);
            if (targetPin != null &&
                !(ReferenceEquals(targetPin.Value.Device, _wiringStartDevice) && targetPin.Value.PinIndex == _wiringStartPinIndex))
            {
                var (tX, tY) = targetPin.Value.Device.GetAbsolutePinPosition(targetPin.Value.PinIndex);
                _wiringCursorPos = (tX, tY);
                _snapTargetPin = targetPin;
                _snapTargetWire = null;
            }
            else
            {
                _snapTargetPin = null;

                // 2. Check if hovering near an existing wire (T-junction snap)
                var targetWire = FindWireAt(wx, wy, tolerance: 0.5);
                if (targetWire != null && Circuit != null)
                {
                    _wiringCursorPos = GetClosestPointOnWire(targetWire, wx, wy);
                    _snapTargetWire = targetWire;
                }
                else
                {
                    _snapTargetWire = null;
                    _wiringCursorPos = (Math.Round(wx), Math.Round(wy));
                }
            }
            InvalidateVisual();
            return;
        }

        if (ToolMode == CircuitToolMode.ProbeCh1 || ToolMode == CircuitToolMode.ProbeCh2)
        {
            _probeCursorPos = (wx, wy);
            _hoveredPin = Circuit?.FindPinAt(wx, wy, tolerance: 0.6);
            _hoveredWire = _hoveredPin == null ? FindWireAt(wx, wy, tolerance: 0.5) : null;
            InvalidateVisual();
            return;
        }

        if (!string.IsNullOrEmpty(PlacingDeviceType))
        {
            _placementCursorPos = (Math.Round(wx), Math.Round(wy));
            InvalidateVisual();
            return;
        }

        // Hover tracking for pins and wires
        var newHoveredPin = Circuit?.FindPinAt(wx, wy, tolerance: 0.55);
        var newHoveredWire = newHoveredPin == null ? FindWireAt(wx, wy, tolerance: 0.4) : null;
        if (newHoveredPin?.Device != _hoveredPin?.Device || newHoveredPin?.PinIndex != _hoveredPin?.PinIndex || newHoveredWire != _hoveredWire)
        {
            _hoveredPin = newHoveredPin;
            _hoveredWire = newHoveredWire;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = _isSpaceDown ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
        }

        if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            e.Pointer.Capture(null);

            double x1 = Math.Min(_marqueeStart.X, _marqueeEnd.X);
            double x2 = Math.Max(_marqueeStart.X, _marqueeEnd.X);
            double y1 = Math.Min(_marqueeStart.Y, _marqueeEnd.Y);
            double y2 = Math.Max(_marqueeStart.Y, _marqueeEnd.Y);

            if (Math.Abs(x2 - x1) > 4 && Math.Abs(y2 - y1) > 4 && Circuit != null)
            {
                var (wx1, wy1) = ScreenToWorld(new Point(x1, y1));
                var (wx2, wy2) = ScreenToWorld(new Point(x2, y2));
                double minWx = Math.Min(wx1, wx2);
                double maxWx = Math.Max(wx1, wx2);
                double minWy = Math.Min(wy1, wy2);
                double maxWy = Math.Max(wy1, wy2);

                foreach (var dev in Circuit.Devices)
                {
                    if (dev.LocationX >= minWx - 0.5 && dev.LocationX <= maxWx + 0.5 &&
                        dev.LocationY >= minWy - 0.5 && dev.LocationY <= maxWy + 0.5)
                    {
                        SelectedDevices.Add(dev);
                    }
                }

                foreach (var wire in Circuit.Wires)
                {
                    var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
                    var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
                    if (dev1 != null && dev2 != null)
                    {
                        if (SelectedDevices.Contains(dev1) && SelectedDevices.Contains(dev2))
                        {
                            SelectedWires.Add(wire);
                        }
                    }
                }

                if (SelectedDevices.Count > 0)
                {
                    SelectedDevice = SelectedDevices.First();
                }
            }
            InvalidateVisual();
        }

        if (_isDraggingDevice)
        {
            if (_draggedDevice != null && Circuit != null)
            {
                var collidingDev = Circuit.Devices.FirstOrDefault(d =>
                    !ReferenceEquals(d, _draggedDevice) &&
                    !SelectedDevices.Contains(d) &&
                    Math.Abs(d.LocationX - _draggedDevice.LocationX) < 0.6 &&
                    Math.Abs(d.LocationY - _draggedDevice.LocationY) < 0.6);

                if (collidingDev != null)
                {
                    (int Ox, int Oy)[] searchOffsets = { (0, 2), (0, -2), (2, 0), (-2, 0), (2, 2), (-2, -2) };
                    foreach (var (ox, oy) in searchOffsets)
                    {
                        double testX = _draggedDevice.LocationX + ox;
                        double testY = _draggedDevice.LocationY + oy;
                        if (!Circuit.Devices.Any(d => !ReferenceEquals(d, _draggedDevice) && Math.Abs(d.LocationX - testX) < 0.6 && Math.Abs(d.LocationY - testY) < 0.6))
                        {
                            _draggedDevice.LocationX = testX;
                            _draggedDevice.LocationY = testY;
                            break;
                        }
                    }
                    Circuit.RouteAllWires();
                    ParticleEngine.SyncWithCircuit(Circuit);
                }
            }
            _isDragCollision = false;

            if (SelectedDevices.Count > 1)
            {
                var moveActions = new List<IUndoableAction>();
                foreach (var kvp in _multiDragStartPositions)
                {
                    if (kvp.Key.LocationX != kvp.Value.X || kvp.Key.LocationY != kvp.Value.Y)
                    {
                        moveActions.Add(new MoveDeviceAction(kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.LocationX, kvp.Key.LocationY));
                    }
                }
                if (moveActions.Count > 0)
                {
                    HistoryManager?.Record(new BatchCircuitAction($"Move {moveActions.Count} components", moveActions));
                }
            }
            if (_draggedDevice != null && (_draggedDevice.LocationX != _deviceDragStartPos.X || _draggedDevice.LocationY != _deviceDragStartPos.Y))
            {
                HistoryManager?.Record(new MoveDeviceAction(_draggedDevice, _deviceDragStartPos.X, _deviceDragStartPos.Y, _draggedDevice.LocationX, _draggedDevice.LocationY));
            }

            if (Circuit != null)
            {
                var movedDevs = (_draggedDevice != null && SelectedDevices.Contains(_draggedDevice) && SelectedDevices.Count > 1)
                    ? (IEnumerable<Device>)SelectedDevices
                    : (_draggedDevice != null ? new[] { _draggedDevice } : Array.Empty<Device>());

                var affectedNodes = movedDevs.SelectMany(d => d.Pins.Select(p => p.NodeId)).Distinct().Where(n => n >= 0);
                foreach (var nid in affectedNodes)
                {
                    Circuit.ResynthesizeNode(nid);
                }
                Circuit.RouteAllWires();
                ParticleEngine.SyncWithCircuit(Circuit);
            }
            InvalidateVisual();

            _isDraggingDevice = false;
            _draggedDevice = null;
            _multiDragStartPositions.Clear();
            e.Pointer.Capture(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        bool isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (isCtrlOrCmd && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                HistoryManager?.Redo();
            }
            else
            {
                HistoryManager?.Undo();
            }
            if (Circuit != null) ParticleEngine.SyncWithCircuit(Circuit);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (isCtrlOrCmd && e.Key == Key.Y)
        {
            HistoryManager?.Redo();
            if (Circuit != null) ParticleEngine.SyncWithCircuit(Circuit);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.R:
                RotateSelected(clockwise: true);
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                DeleteSelected();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isWiring)
                {
                    _isWiring = false;
                    _wiringStartDevice = null;
                }
                PlacingDeviceType = null;
                SelectedDevice = null;
                SelectedWire = null;
                ToolMode = CircuitToolMode.Select;
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.W:
                ToolMode = CircuitToolMode.Wire;
                e.Handled = true;
                break;

            case Key.V:
                ToolMode = CircuitToolMode.Select;
                e.Handled = true;
                break;

            case Key.X:
                ToolMode = CircuitToolMode.Cut;
                e.Handled = true;
                break;

            case Key.F:
                ZoomToFit();
                e.Handled = true;
                break;

            case Key.Space:
                _isSpaceDown = true;
                Cursor = new Cursor(StandardCursorType.Hand);
                e.Handled = true;
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _isSpaceDown = false;
            Cursor = new Cursor(StandardCursorType.Arrow);
            if (_isPanning)
            {
                _isPanning = false;
            }
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 10 || h <= 10) return;

        if (_needsZoomToFit && w > 50 && h > 50)
        {
            ZoomToFit();
        }

        // 1. Sleek Theme-Aware PCB Canvas
        var tokens = FryCircuit.UI.Theming.CircuitThemeService.Instance.Tokens;
        var bgBrush = tokens.CanvasBgBrush;
        context.FillRectangle(bgBrush, new Rect(0, 0, w, h));

        // 2. High-precision dot grid
        DrawGrid(context, w, h);

        if (Circuit == null) return;

        Circuit.EnsureWiresSynthesized();

        // 3. Wires & Connections
        DrawWires(context);

        // 4. Circuit Components
        DrawComponents(context);

        // 5. Animated Current Particles
        DrawParticles(context);

        // 5b. Live Circuit Annotations (Current Direction Arrows & Magnitude Badges)
        if (ShowCurrentAnnotations)
        {
            DrawCurrentAnnotations(context);
        }

        // 5c. Live Circuit Annotations (Solder Junction & Rail Voltage Badges)
        if (ShowVoltageAnnotations)
        {
            DrawVoltageAnnotations(context);
        }

        // 6. Active Live Wire Drawing Preview
        if (_isWiring && _wiringStartDevice != null)
        {
            DrawWiringPreview(context);
        }

        // 7. Active Component Placement Ghost
        if (!string.IsNullOrEmpty(PlacingDeviceType))
        {
            DrawPlacementGhost(context);
        }

        // 8. Hovered Pin Target Reticle
        if (_hoveredPin != null && !_isWiring)
        {
            DrawPinHoverReticle(context, _hoveredPin.Value);
        }

        // 9. Oscilloscope Probed Channel Badges
        DrawProbeBadges(context);

        // 10. Active Oscilloscope Probe Cursor Reticle
        DrawProbeCursor(context);

        // 11. Marquee / Box Selection overlay
        if (_isMarqueeSelecting)
        {
            double x1 = Math.Min(_marqueeStart.X, _marqueeEnd.X);
            double x2 = Math.Max(_marqueeStart.X, _marqueeEnd.X);
            double y1 = Math.Min(_marqueeStart.Y, _marqueeEnd.Y);
            double y2 = Math.Max(_marqueeStart.Y, _marqueeEnd.Y);
            double mw = Math.Abs(x2 - x1);
            double mh = Math.Abs(y2 - y1);
            if (mw > 2 && mh > 2)
            {
                var mRect = new Rect(x1, y1, mw, mh);
                var fillBrush = new SolidColorBrush(Color.FromArgb(25, 56, 189, 248));
                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), 1.5)
                {
                    DashStyle = DashStyle.Dash
                };
                context.FillRectangle(fillBrush, mRect, 3);
                context.DrawRectangle(borderPen, mRect, 3);
            }
        }

        // 12. Industrial EDA CAD HUD Overlay
        DrawEdaHud(context, w, h);
    }

    private void DrawProbeBadges(DrawingContext context)
    {
        if (Circuit == null) return;

        if (ProbedNodeCh1.HasValue)
        {
            var pt = FindNodeDisplayPosition(ProbedNodeCh1.Value);
            if (pt.HasValue)
            {
                DrawChannelBadge(context, pt.Value, "CH1", Color.FromRgb(245, 158, 11));
            }
        }

        if (ProbedNodeCh2.HasValue)
        {
            var pt = FindNodeDisplayPosition(ProbedNodeCh2.Value);
            if (pt.HasValue)
            {
                DrawChannelBadge(context, pt.Value, "CH2", Color.FromRgb(6, 182, 212));
            }
        }
    }

    private Point? FindNodeDisplayPosition(int nodeId)
    {
        if (Circuit == null) return null;

        // If a wire is selected and matches this node, anchor the probe test clip directly on it!
        if (SelectedWire != null && SelectedWire.NodeId == nodeId)
        {
            var pt = GetWireMidpoint(SelectedWire);
            if (pt.HasValue) return WorldToScreen(pt.Value.X, pt.Value.Y);
        }

        // Try to find a wire with this nodeId
        var wire = Circuit.Wires.FirstOrDefault(w => w.NodeId == nodeId);
        if (wire != null)
        {
            var pt = GetWireMidpoint(wire);
            if (pt.HasValue) return WorldToScreen(pt.Value.X, pt.Value.Y);
        }

        // Try to find a pin with this nodeId
        foreach (var dev in Circuit.Devices)
        {
            for (int i = 0; i < dev.Pins.Count; i++)
            {
                if (dev.Pins[i].NodeId == nodeId)
                {
                    var (wx, wy) = dev.GetAbsolutePinPosition(i);
                    return WorldToScreen(wx, wy);
                }
            }
        }

        return null;
    }

    private Point? GetWireMidpoint(CircuitWire wire)
    {
        if (wire.PathPoints != null && wire.PathPoints.Count >= 2)
        {
            int midIdx = wire.PathPoints.Count / 2;
            if (wire.PathPoints.Count % 2 == 0)
            {
                var p1 = wire.PathPoints[midIdx - 1];
                var p2 = wire.PathPoints[midIdx];
                return new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
            }
            return new Point(wire.PathPoints[midIdx].X, wire.PathPoints[midIdx].Y);
        }

        if (Circuit != null)
        {
            var d1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var d2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (d1 != null && d2 != null)
            {
                var p1 = d1.GetAbsolutePinPosition(wire.FromPinIndex);
                var p2 = d2.GetAbsolutePinPosition(wire.ToPinIndex);
                return new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
            }
        }

        return null;
    }

    private void DrawChannelBadge(DrawingContext context, Point screenPt, string channel, Color color)
    {
        double badgeW = 34 * Math.Max(0.8, Zoom);
        double badgeH = 18 * Math.Max(0.8, Zoom);
        double bx = screenPt.X - badgeW / 2.0;
        double by = screenPt.Y - badgeH - 12 * Zoom;

        var bgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42));
        var borderPen = new Pen(new SolidColorBrush(color), 1.5);
        var badgeRect = new Rect(bx, by, badgeW, badgeH);

        // Radiant probe concentric target at screenPt
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B)), null, screenPt, 8.0 * Zoom, 8.0 * Zoom);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), new Pen(new SolidColorBrush(color), 1.8 * Zoom), screenPt, 5.0 * Zoom, 5.0 * Zoom);
        context.DrawEllipse(new SolidColorBrush(color), null, screenPt, 2.5 * Zoom, 2.5 * Zoom);

        // Pointer line to terminal
        context.DrawLine(borderPen, new Point(screenPt.X, by + badgeH), new Point(screenPt.X, screenPt.Y - 5.0 * Zoom));

        // Badge pill
        context.DrawRectangle(bgBrush, borderPen, badgeRect, 4, 4);

        var ft = new FormattedText(
            channel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.ExtraBold),
            Math.Max(9, 10.5 * Zoom),
            new SolidColorBrush(color)
        );
        context.DrawText(ft, new Point(bx + (badgeW - ft.Width) / 2.0, by + (badgeH - ft.Height) / 2.0));
    }

    private void DrawSnapBadge(DrawingContext context, Point screenPt, string text, Color color)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(9, 10.5 * Zoom),
            new SolidColorBrush(color)
        );

        double badgeW = ft.Width + 14 * Zoom;
        double badgeH = ft.Height + 6 * Zoom;
        double bx = screenPt.X - badgeW / 2.0;
        double by = screenPt.Y - badgeH - 12 * Zoom;

        var bgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42));
        var borderPen = new Pen(new SolidColorBrush(color), 1.2 * Zoom);
        var badgeRect = new Rect(bx, by, badgeW, badgeH);

        context.DrawRectangle(bgBrush, borderPen, badgeRect, 4, 4);
        context.DrawText(ft, new Point(bx + 7 * Zoom, by + 3 * Zoom));
    }

    private void DrawProbeCursor(DrawingContext context)
    {
        if (ToolMode != CircuitToolMode.ProbeCh1 && ToolMode != CircuitToolMode.ProbeCh2) return;

        var center = WorldToScreen(_probeCursorPos.Wx, _probeCursorPos.Wy);
        bool isCh1 = ToolMode == CircuitToolMode.ProbeCh1;
        var probeColor = isCh1 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(6, 182, 212);
        string chLabel = isCh1 ? "PROBE CH 1" : "PROBE CH 2";

        // Interactive Target Acquisition: snap to pin or wire under probe cursor
        int targetNode = -1;
        double targetV = 0.0;
        Point? snapPt = null;
        var pTest = Circuit?.FindPinAt(_probeCursorPos.Wx, _probeCursorPos.Wy, tolerance: 0.6);
        if (pTest != null)
        {
            targetNode = pTest.Value.Pin.NodeId;
            var (px, py) = pTest.Value.Device.GetAbsolutePinPosition(pTest.Value.PinIndex);
            snapPt = WorldToScreen(px, py);
        }
        else
        {
            var wTest = FindWireAt(_probeCursorPos.Wx, _probeCursorPos.Wy, tolerance: 0.5);
            if (wTest != null)
            {
                targetNode = wTest.NodeId;
            }
        }

        if (targetNode >= 0 && Circuit?.Nodes.TryGetValue(targetNode, out var n) == true)
        {
            targetV = n.Voltage;
            chLabel = $"{chLabel} [N{targetNode}: {FormatVoltageLabel(targetV)}]";
        }

        if (snapPt.HasValue)
        {
            // Locked Target Ring
            var lockPt = snapPt.Value;
            var lockPen = new Pen(new SolidColorBrush(probeColor), 2.2 * Zoom);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, probeColor.R, probeColor.G, probeColor.B)), lockPen, lockPt, 10 * Zoom, 10 * Zoom);
            context.DrawLine(lockPen, new Point(lockPt.X - 14 * Zoom, lockPt.Y), new Point(lockPt.X - 6 * Zoom, lockPt.Y));
            context.DrawLine(lockPen, new Point(lockPt.X + 6 * Zoom, lockPt.Y), new Point(lockPt.X + 14 * Zoom, lockPt.Y));
            context.DrawLine(lockPen, new Point(lockPt.X, lockPt.Y - 14 * Zoom), new Point(lockPt.X, lockPt.Y - 6 * Zoom));
            context.DrawLine(lockPen, new Point(lockPt.X, lockPt.Y + 6 * Zoom), new Point(lockPt.X, lockPt.Y + 14 * Zoom));
        }

        var ringPen = new Pen(new SolidColorBrush(probeColor), 2.0)
        {
            DashStyle = DashStyle.Dash
        };
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, probeColor.R, probeColor.G, probeColor.B)), ringPen, center, 14 * Zoom, 14 * Zoom);
        context.DrawEllipse(new SolidColorBrush(probeColor), null, center, 3.5 * Zoom, 3.5 * Zoom);

        var ft = new FormattedText(
            chLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10.5,
            new SolidColorBrush(probeColor)
        );

        var labelRect = new Rect(center.X + 16, center.Y - 10, ft.Width + 8, ft.Height + 4);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 10, 15, 24)), labelRect, 3);
        context.DrawRectangle(new Pen(new SolidColorBrush(probeColor), 1), labelRect, 3);
        context.DrawText(ft, new Point(center.X + 20, center.Y - 8));
    }

    private void DrawGrid(DrawingContext context, double width, double height)
    {
        double baseStep = GridSize * Zoom;
        double step = baseStep;
        while (step < 20.0)
        {
            step *= 2.0;
        }

        double startX = PanX % step;
        if (startX < 0) startX += step;
        double startY = PanY % step;
        if (startY < 0) startY += step;

        var tokens = FryCircuit.UI.Theming.CircuitThemeService.Instance.Tokens;
        var minorDotBrush = tokens.CanvasDotMinorBrush;
        var majorDotBrush = tokens.CanvasDotMajorBrush;

        for (double x = startX; x < width; x += step)
        {
            for (double y = startY; y < height; y += step)
            {
                var (wx, wy) = ScreenToWorld(new Point(x, y));
                bool isMajor = (Math.Abs(Math.Round(wx)) % 5 == 0) && (Math.Abs(Math.Round(wy)) % 5 == 0);
                if (isMajor)
                {
                    context.DrawEllipse(majorDotBrush, null, new Point(x, y), 1.75, 1.75);
                }
                else
                {
                    context.DrawEllipse(minorDotBrush, null, new Point(x, y), 1.1, 1.1);
                }
            }
        }

        // Origin marker (0, 0)
        var origin = WorldToScreen(0, 0);
        if (origin.X >= -50 && origin.X <= width + 50 && origin.Y >= -50 && origin.Y <= height + 50)
        {
            var originPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1.0);
            context.DrawLine(originPen, new Point(origin.X - 8, origin.Y), new Point(origin.X + 8, origin.Y));
            context.DrawLine(originPen, new Point(origin.X, origin.Y - 8), new Point(origin.X, origin.Y + 8));
        }
    }

    private void DrawWires(DrawingContext context)
    {
        if (Circuit == null) return;

        // 1. Compute IEEE 315 standard solder junction beads and incident directions
        var directionsAtPoint = new Dictionary<(double X, double Y), HashSet<(int dx, int dy)>>();
        var junctionColors = new Dictionary<(double X, double Y), (Color Glow, Color Core)>();
        var junctionNodeIds = new Dictionary<(double X, double Y), int>();
        var verticalSegments = new List<(double X, double MinY, double MaxY, int NodeId, CircuitWire Wire)>();

        foreach (var wire in Circuit.Wires)
        {
            if (wire.PathPoints == null || wire.PathPoints.Count < 2)
            {
                var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
                var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
                if (dev1 != null && dev2 != null)
                {
                    wire.PathPoints = SchematicWireRouter.RouteWire(Circuit, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
                }
            }

            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2) continue;

            Circuit.Nodes.TryGetValue(wire.NodeId, out var node);
            double v = node?.Voltage ?? 0.0;
            bool isGnd = node?.IsGround == true;
            var (vGlow, vCore) = GetVoltageColors(v, isGnd);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                double x1 = pts[i].X;
                double y1 = pts[i].Y;
                double x2 = pts[i + 1].X;
                double y2 = pts[i + 1].Y;

                if (Math.Abs(y1 - y2) < 1e-4)
                {
                    double y = y1;
                    double minX = Math.Min(x1, x2);
                    double maxX = Math.Max(x1, x2);
                    for (double x = minX; x <= maxX + 1e-4; x += 0.5)
                    {
                        var key = (Math.Round(x, 1), Math.Round(y, 1));
                        if (!directionsAtPoint.TryGetValue(key, out var dirSet))
                        {
                            dirSet = new HashSet<(int, int)>();
                            directionsAtPoint[key] = dirSet;
                        }
                        junctionColors[key] = (vGlow, vCore);
                        junctionNodeIds[key] = wire.NodeId;

                        if (x > minX + 1e-4) dirSet.Add((-1, 0)); // Wire extends left
                        if (x < maxX - 1e-4) dirSet.Add((1, 0));  // Wire extends right
                    }
                }
                else if (Math.Abs(x1 - x2) < 1e-4)
                {
                    double x = x1;
                    double minY = Math.Min(y1, y2);
                    double maxY = Math.Max(y1, y2);
                    verticalSegments.Add((x, minY, maxY, wire.NodeId, wire));

                    for (double y = minY; y <= maxY + 1e-4; y += 0.5)
                    {
                        var key = (Math.Round(x, 1), Math.Round(y, 1));
                        if (!directionsAtPoint.TryGetValue(key, out var dirSet))
                        {
                            dirSet = new HashSet<(int, int)>();
                            directionsAtPoint[key] = dirSet;
                        }
                        junctionColors[key] = (vGlow, vCore);
                        junctionNodeIds[key] = wire.NodeId;

                        if (y > minY + 1e-4) dirSet.Add((0, -1)); // Wire extends up
                        if (y < maxY - 1e-4) dirSet.Add((0, 1));  // Wire extends down
                    }
                }
            }
        }

        // Add device pin directions: a pin connects into the point from the device body
        foreach (var dev in Circuit.Devices)
        {
            for (int p = 0; p < dev.Pins.Count; p++)
            {
                var (px, py) = dev.GetAbsolutePinPosition(p);
                var key = (Math.Round(px, 1), Math.Round(py, 1));
                if (directionsAtPoint.TryGetValue(key, out var dirSet))
                {
                    var (nx, ny) = dev.GetPinNormal(p);
                    int pdx = -Math.Sign(nx);
                    int pdy = -Math.Sign(ny);
                    if (pdx != 0 || pdy != 0)
                    {
                        dirSet.Add((pdx, pdy));
                    }
                }
            }
        }

        // 2. Draw wire segments (with bridge hop arcs for unconnected orthogonal crossings)
        double hopR = Math.Min(5.5 * Zoom, GridSize * Zoom * 0.22);

        foreach (var wire in Circuit.Wires)
        {
            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2) continue;

            bool isSelected = wire == SelectedWire || SelectedWires.Contains(wire);
            Circuit.Nodes.TryGetValue(wire.NodeId, out var node);
            double voltage = node?.Voltage ?? 0.0;
            bool isGround = node?.IsGround == true;

            bool isHovered = wire == _hoveredWire;
            bool isProbedCh1 = ProbedNodeCh1.HasValue && wire.NodeId == ProbedNodeCh1.Value;
            bool isProbedCh2 = ProbedNodeCh2.HasValue && wire.NodeId == ProbedNodeCh2.Value;

            var (glowColor, coreColor) = isSelected
                ? (Color.FromArgb(80, 56, 189, 248), Color.FromRgb(56, 189, 248))
                : isProbedCh1
                    ? (Color.FromArgb(90, 245, 158, 11), Color.FromRgb(245, 158, 11))
                    : isProbedCh2
                        ? (Color.FromArgb(90, 6, 182, 212), Color.FromRgb(6, 182, 212))
                        : isHovered
                            ? (Color.FromArgb(70, 56, 189, 248), Color.FromRgb(186, 230, 253))
                            : GetVoltageColors(voltage, isGround);

            bool hasAura = isSelected || isProbedCh1 || isProbedCh2 || isHovered;
            double glowWidth = (isSelected || isProbedCh1 || isProbedCh2 ? 4.5 : 3.5) * Zoom;
            double coreWidth = (isSelected || isProbedCh1 || isProbedCh2 ? 2.3 : 1.9) * Zoom;

            var glowPen = hasAura ? new Pen(new SolidColorBrush(glowColor), glowWidth)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            } : null;
            var corePen = new Pen(new SolidColorBrush(coreColor), coreWidth)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            for (int i = 0; i < pts.Count - 1; i++)
            {
                double x1 = pts[i].X;
                double y1 = pts[i].Y;
                double x2 = pts[i + 1].X;
                double y2 = pts[i + 1].Y;

                // Vertical segments are drawn straight through underneath
                if (Math.Abs(x1 - x2) < 1e-4)
                {
                    var s1 = WorldToScreen(x1, y1);
                    var s2 = WorldToScreen(x2, y2);
                    if (hasAura && glowPen != null) context.DrawLine(glowPen, s1, s2);
                    context.DrawLine(corePen, s1, s2);
                    continue;
                }

                // Horizontal segments: detect crossings with unconnected vertical segments
                double minX = Math.Min(x1, x2);
                double maxX = Math.Max(x1, x2);
                var crossings = new List<double>();

                foreach (var vs in verticalSegments)
                {
                    if (vs.Wire == wire || vs.NodeId == wire.NodeId) continue;
                    if (vs.X > minX + 0.25 && vs.X < maxX - 0.25)
                    {
                        if (y1 > vs.MinY + 0.25 && y1 < vs.MaxY - 0.25)
                        {
                            var key = (Math.Round(vs.X, 1), Math.Round(y1, 1));
                            if (!directionsAtPoint.TryGetValue(key, out var dirSet) || dirSet.Count < 3)
                            {
                                if (!crossings.Contains(vs.X)) crossings.Add(vs.X);
                            }
                        }
                    }
                }

                if (crossings.Count == 0)
                {
                    var s1 = WorldToScreen(x1, y1);
                    var s2 = WorldToScreen(x2, y2);
                    if (hasAura && glowPen != null) context.DrawLine(glowPen, s1, s2);
                    context.DrawLine(corePen, s1, s2);
                }
                else
                {
                    // Sort crossings in traversal direction
                    if (x2 > x1) crossings.Sort();
                    else crossings.Sort((a, b) => b.CompareTo(a));

                    int dirSign = Math.Sign(x2 - x1);
                    var currScreen = WorldToScreen(x1, y1);

                    foreach (var vx in crossings)
                    {
                        var crossScreen = WorldToScreen(vx, y1);
                        var hopStart = new Point(crossScreen.X - dirSign * hopR, crossScreen.Y);
                        var hopEnd = new Point(crossScreen.X + dirSign * hopR, crossScreen.Y);

                        if (Math.Abs(hopStart.X - currScreen.X) > 1.0)
                        {
                            if (hasAura && glowPen != null) context.DrawLine(glowPen, currScreen, hopStart);
                            context.DrawLine(corePen, currScreen, hopStart);
                        }

                        var hopGeom = CreateHopGeometry(hopStart, hopEnd, hopR);
                        if (hasAura && glowPen != null) context.DrawGeometry(null, glowPen, hopGeom);
                        context.DrawGeometry(null, corePen, hopGeom);

                        currScreen = hopEnd;
                    }

                    var endScreen = WorldToScreen(x2, y2);
                    if (Math.Abs(endScreen.X - currScreen.X) > 1.0)
                    {
                        if (hasAura && glowPen != null) context.DrawLine(glowPen, currScreen, endScreen);
                        context.DrawLine(corePen, currScreen, endScreen);
                    }
                }
            }

            // Mini Digital Waveform / Pulse Activity Glyph (only for 555 timer digital clock outputs)
            var dFrom = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dTo = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            bool isClockWire = !isGround && (
                               (dFrom is Timer555Device && wire.FromPinIndex == 2)
                            || (dTo is Timer555Device && wire.ToPinIndex == 2));

            if (isClockWire && pts.Count >= 2)
            {
                double maxLen = -1;
                Point bestMid = new Point();
                for (int s = 0; s < pts.Count - 1; s++)
                {
                    var pA = WorldToScreen(pts[s].X, pts[s].Y);
                    var pB = WorldToScreen(pts[s + 1].X, pts[s + 1].Y);
                    double len = Math.Sqrt((pA.X - pB.X) * (pA.X - pB.X) + (pA.Y - pB.Y) * (pA.Y - pB.Y));
                    if (len > maxLen)
                    {
                        maxLen = len;
                        bestMid = new Point((pA.X + pB.X) / 2.0, (pA.Y + pB.Y) / 2.0);
                    }
                }

                if (maxLen > 24.0)
                {
                    DrawMiniPulseGlyph(context, bestMid, Zoom, Color.FromRgb(160, 180, 255));
                }
            }
        }

        // 3. Render solid solder junction beads for all points with at least 3 distinct incident directions
        foreach (var (key, dirSet) in directionsAtPoint)
        {
            if (dirSet.Count >= 3)
            {
                var screenPt = WorldToScreen(key.X, key.Y);
                int jNodeId = junctionNodeIds.GetValueOrDefault(key, -1);
                bool isJuncProbedCh1 = ProbedNodeCh1.HasValue && jNodeId == ProbedNodeCh1.Value;
                bool isJuncProbedCh2 = ProbedNodeCh2.HasValue && jNodeId == ProbedNodeCh2.Value;

                if (isJuncProbedCh1 || isJuncProbedCh2)
                {
                    var ringCol = isJuncProbedCh1 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(6, 182, 212);
                    context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, ringCol.R, ringCol.G, ringCol.B)), null, screenPt, 7.5 * Zoom, 7.5 * Zoom);
                    context.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), new Pen(new SolidColorBrush(ringCol), 1.8 * Zoom), screenPt, 5.0 * Zoom, 5.0 * Zoom);
                    context.DrawEllipse(new SolidColorBrush(ringCol), null, screenPt, 2.8 * Zoom, 2.8 * Zoom);
                }
                else
                {
                    var (vGlow, vCore) = junctionColors.GetValueOrDefault(key, (Color.FromArgb(40, 148, 163, 184), Color.FromRgb(148, 163, 184)));
                    var beadGlowBrush = new SolidColorBrush(Color.FromArgb(50, vCore.R, vCore.G, vCore.B));
                    var beadCoreBrush = new SolidColorBrush(vCore);
                    var beadRingPen = new Pen(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), 1.2 * Zoom);

                    context.DrawEllipse(beadGlowBrush, null, screenPt, 4.2 * Zoom, 4.2 * Zoom);
                    context.DrawEllipse(beadCoreBrush, beadRingPen, screenPt, 2.8 * Zoom, 2.8 * Zoom);
                }
            }
        }

        _lastDirectionsAtPoint = directionsAtPoint;
        _lastJunctionNodeIds = junctionNodeIds;
    }

    private static StreamGeometry CreateHopGeometry(Point start, Point end, double hopR)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.ArcTo(end, new Size(hopR, hopR), 0, false, SweepDirection.CounterClockwise);
        }
        return geom;
    }

    private static void DrawMiniPulseGlyph(DrawingContext context, Point midPt, double zoom, Color color)
    {
        double bw = 18.0 * Math.Max(0.75, zoom);
        double bh = 11.0 * Math.Max(0.75, zoom);
        var rect = new Rect(midPt.X - bw / 2.0, midPt.Y - bh / 2.0, bw, bh);

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 14, 20, 30)), rect, 2.5f);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)), 1), rect, 2.5f);

        // Draw 3 square pulses
        var pulsePen = new Pen(new SolidColorBrush(color), 1.2 * Math.Max(0.75, zoom));
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            double left = rect.Left + 2.5 * zoom;
            double top = rect.Top + 2.5 * zoom;
            double bot = rect.Bottom - 2.5 * zoom;
            double step = (rect.Width - 5.0 * zoom) / 6.0;

            g.BeginFigure(new Point(left, bot), false);
            g.LineTo(new Point(left + step, bot));
            g.LineTo(new Point(left + step, top));
            g.LineTo(new Point(left + 2 * step, top));
            g.LineTo(new Point(left + 2 * step, bot));
            g.LineTo(new Point(left + 3 * step, bot));
            g.LineTo(new Point(left + 3 * step, top));
            g.LineTo(new Point(left + 4 * step, top));
            g.LineTo(new Point(left + 4 * step, bot));
            g.LineTo(new Point(left + 5 * step, bot));
            g.LineTo(new Point(left + 5 * step, top));
            g.LineTo(new Point(left + 6 * step, top));
            g.LineTo(new Point(left + 6 * step, bot));
        }
        context.DrawGeometry(null, pulsePen, geom);
    }

    private void DrawComponents(DrawingContext context)
    {
        if (Circuit == null) return;

        var renderCtx = new DeviceRenderContext
        {
            Zoom = Zoom,
            GridSize = GridSize,
            IsPlacingGhost = false
        };

        foreach (var dev in Circuit.Devices)
        {
            var center = WorldToScreen(dev.LocationX, dev.LocationY);
            bool isSelected = dev == SelectedDevice || SelectedDevices.Contains(dev);

            // Dynamic selection bounding box based on device descriptor and rotation
            var desc = DeviceRegistry.GetDescriptor(dev);
            double baseW = desc?.BoundingBoxSize.Width ?? 2.0;
            double baseH = desc?.BoundingBoxSize.Height ?? 2.0;
            bool isRot90or270 = ((int)dev.Transform.Rotation % 180) != 0;
            double devW = (isRot90or270 ? baseH : baseW) * GridSize * Zoom;
            double devH = (isRot90or270 ? baseW : baseH) * GridSize * Zoom;

            // Push rotation transform
            double rotAngle = (int)dev.Transform.Rotation;
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                         Matrix.CreateRotation(rotAngle * Math.PI / 180.0) *
                                         Matrix.CreateTranslation(center.X, center.Y)))
            {
                var renderer = DeviceRendererRegistry.GetRenderer(dev.Type);

                // Render core device shape
                var devCtx = new DeviceRenderContext
                {
                    Zoom = Zoom,
                    GridSize = GridSize,
                    IsPlacingGhost = false,
                    IsSelected = isSelected,
                    IsDragCollision = _isDragCollision && dev == _draggedDevice,
                    IsGlowPass = false
                };
                renderer.Render(context, dev, center, GridSize * Zoom, isSelected, devCtx);
            }

            // Draw component parameter value label
            string label = GetDeviceLabel(dev);
            if (!string.IsNullOrEmpty(label))
            {
                Color labelTextColor = Color.FromRgb(220, 235, 255);
                if (dev is LedDevice ledDev)
                {
                    try { labelTextColor = Color.Parse(ledDev.ColorHex); } catch { }
                }

                var ft = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                    Math.Max(10, 11 * Zoom),
                    new SolidColorBrush(labelTextColor)
                );

                double labelX;
                double labelY;

                bool isVerticalTwoPin = false;
                if (dev.Pins.Count == 2)
                {
                    var p0 = dev.GetAbsolutePinPosition(0);
                    var p1 = dev.GetAbsolutePinPosition(1);
                    if (Math.Abs(p0.X - p1.X) < 0.1 && Math.Abs(p0.Y - p1.Y) > 0.5)
                    {
                        isVerticalTwoPin = true;
                    }
                }

                if (isVerticalTwoPin)
                {
                    // Physical half-width from center to avoid pushing label too far to the right
                    double physicalHalfW = dev.Type switch
                    {
                        "resistor" => 0.35 * GridSize * Zoom,
                        "capacitor" => 0.52 * GridSize * Zoom,
                        "inductor" => 0.38 * GridSize * Zoom,
                        "diode" or "zener" or "tunneldiode" => 0.42 * GridSize * Zoom,
                        "led" => 0.95 * GridSize * Zoom,
                        "vsource" or "isource" => 0.55 * GridSize * Zoom,
                        "battery" => 0.52 * GridSize * Zoom,
                        "lamp" => 0.48 * GridSize * Zoom,
                        "spst" => 0.38 * GridSize * Zoom,
                        "josephson" => 0.45 * GridSize * Zoom,
                        "cosmicipulse" => 0.52 * GridSize * Zoom,
                        _ => devW / 2.0
                    };
                    labelX = center.X + physicalHalfW + 5 * Zoom;
                    labelY = center.Y - ft.Height / 2.0;
                }
                else if (dev.Type == "potentiometer")
                {
                    // Potentiometer has terminals at Top, Bottom, and Wiper on Right.
                    // Position label cleanly to the Left where there are zero pins or wires.
                    labelX = center.X - devW / 2.0 - ft.Width - 6 * Zoom;
                    labelY = center.Y - ft.Height / 2.0;
                }
                else if (dev.Type is "nmos" or "pmos" or "npn" or "pnp")
                {
                    // Transistors have Gate/Base at Left, Drain/Collector at Top-Right, Source/Emitter at Bottom-Right.
                    // When unrotated (Up), position label in the open Top-Left quadrant to avoid bottom Source rail wires.
                    if (dev.Transform.Rotation == DeviceRotation.Up)
                    {
                        labelX = center.X - devW * 0.35 - ft.Width / 2.0;
                        labelY = center.Y - devH * 0.35 - ft.Height / 2.0;
                    }
                    else
                    {
                        labelX = center.X - ft.Width / 2.0;
                        labelY = center.Y + devH / 2.0 + 4 * Zoom;
                    }
                }
                else
                {
                    // Position badge cleanly below the device boundary
                    labelX = center.X - ft.Width / 2.0;
                    labelY = center.Y + devH / 2.0 + 4 * Zoom;
                }

                var badgeRect = new Rect(labelX - 4 * Zoom, labelY - 2 * Zoom, ft.Width + 8 * Zoom, ft.Height + 4 * Zoom);
                context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 12, 16, 24)), new Pen(new SolidColorBrush(Color.FromArgb(50, 100, 150, 200)), 0.8), badgeRect, 4, 4);
                context.DrawText(ft, new Point(labelX, labelY));
            }
        }
    }

    private void DrawWiringPreview(DrawingContext context)
    {
        if (_wiringStartDevice == null) return;

        var (sx, sy) = _wiringStartDevice.GetAbsolutePinPosition(_wiringStartPinIndex);
        var (dx, dy) = _wiringStartDevice.GetPinNormal(_wiringStartPinIndex);

        var previewRoute = SchematicWireRouter.RouteWire(
            Circuit,
            sx, sy, dx, dy,
            _wiringCursorPos.Wx, _wiringCursorPos.Wy, 0, 0,
            _wiringStartDevice, null);

        var previewPenGlow = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 240, 255)), 6.0 * Zoom)
        {
            LineCap = PenLineCap.Round
        };
        var previewPenCore = new Pen(new SolidColorBrush(Color.FromRgb(0, 240, 255)), 2.8 * Zoom)
        {
            LineCap = PenLineCap.Round,
            DashStyle = DashStyle.Dash
        };

        for (int i = 0; i < previewRoute.Count - 1; i++)
        {
            var s1 = WorldToScreen(previewRoute[i].X, previewRoute[i].Y);
            var s2 = WorldToScreen(previewRoute[i + 1].X, previewRoute[i + 1].Y);
            context.DrawLine(previewPenGlow, s1, s2);
            context.DrawLine(previewPenCore, s1, s2);
        }

        var pStart = WorldToScreen(sx, sy);
        var pEnd = WorldToScreen(_wiringCursorPos.Wx, _wiringCursorPos.Wy);

        // Start pin halo
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)), null, pStart, 6.5 * Zoom, 6.5 * Zoom);

        if (_snapTargetPin != null)
        {
            // Emerald snap target for valid pin connection
            var snapPen = new Pen(new SolidColorBrush(Color.FromRgb(52, 211, 153)), 2.0 * Zoom);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 52, 211, 153)), snapPen, pEnd, 8.5 * Zoom, 8.5 * Zoom);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(241, 245, 249)), null, pEnd, 3.0 * Zoom, 3.0 * Zoom);
            DrawSnapBadge(context, pEnd, "SNAP PIN", Color.FromRgb(52, 211, 153));
        }
        else if (_snapTargetWire != null)
        {
            // Amber solder bead preview for T-junction tap
            var snapPen = new Pen(new SolidColorBrush(Color.FromRgb(251, 191, 36)), 2.0 * Zoom);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 251, 191, 36)), snapPen, pEnd, 7.5 * Zoom, 7.5 * Zoom);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(254, 243, 199)), null, pEnd, 3.5 * Zoom, 3.5 * Zoom);
            DrawSnapBadge(context, pEnd, "T-JUNCTION TAP", Color.FromRgb(251, 191, 36));
        }
        else
        {
            // Current cursor reticle
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), null, pEnd, 4.5 * Zoom, 4.5 * Zoom);
        }
    }

    private void DrawPlacementGhost(DrawingContext context)
    {
        if (string.IsNullOrEmpty(PlacingDeviceType)) return;

        var center = WorldToScreen(_placementCursorPos.Wx, _placementCursorPos.Wy);
        var ghostDev = DeviceFactory.CreateDevice(PlacingDeviceType, 0, _placementCursorPos.Wx, _placementCursorPos.Wy);
        ghostDev.Transform = new DeviceTransform(_placementRotation);

        var renderCtx = new DeviceRenderContext
        {
            Zoom = Zoom,
            GridSize = GridSize,
            IsPlacingGhost = true
        };

        double rotAngle = (int)_placementRotation;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                     Matrix.CreateRotation(rotAngle * Math.PI / 180.0) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            var renderer = DeviceRendererRegistry.GetRenderer(PlacingDeviceType);
            renderer.Render(context, ghostDev, center, GridSize * Zoom, false, renderCtx);
        }

        // Snapped grid target ring & crosshair ticks
        var ghostRingPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 56, 189, 248)), 1.2)
        {
            DashStyle = DashStyle.Dash
        };
        double ringR = GridSize * Zoom * 0.9;
        context.DrawEllipse(null, ghostRingPen, center, ringR, ringR);

        // Cyber crosshair ticks
        var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), 1.2);
        double tickLen = 5 * Zoom;
        context.DrawLine(tickPen, new Point(center.X - ringR - tickLen, center.Y), new Point(center.X - ringR + 2, center.Y));
        context.DrawLine(tickPen, new Point(center.X + ringR - 2, center.Y), new Point(center.X + ringR + tickLen, center.Y));
        context.DrawLine(tickPen, new Point(center.X, center.Y - ringR - tickLen), new Point(center.X, center.Y - ringR + 2));
        context.DrawLine(tickPen, new Point(center.X, center.Y + ringR - 2), new Point(center.X, center.Y + ringR + tickLen));

        // Placement HUD Tooltip: (X, Y) • [Space: Rotate]
        string hudText = $"X: {_placementCursorPos.Wx:F0}  Y: {_placementCursorPos.Wy:F0}  [R: {(int)_placementRotation}°]  •  Space: Rotate";
        var ftHud = new FormattedText(
            hudText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            9.5,
            new SolidColorBrush(Color.FromRgb(56, 189, 248))
        );
        double hudW = ftHud.Width + 12;
        double hudH = ftHud.Height + 6;
        var hudRect = new Rect(center.X + ringR + 8, center.Y - hudH / 2.0, hudW, hudH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), hudRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(100, 56, 189, 248)), 1), hudRect, 4);
        context.DrawText(ftHud, new Point(hudRect.X + 6, hudRect.Y + 3));
    }

    private void DrawPinHoverReticle(DrawingContext context, (Device Device, Pin Pin, int PinIndex) pinInfo)
    {
        var (px, py) = pinInfo.Device.GetAbsolutePinPosition(pinInfo.PinIndex);
        var screenPt = WorldToScreen(px, py);

        var haloBrush = new SolidColorBrush(Color.FromArgb(70, 56, 189, 248));
        var ringPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.8 * Zoom);
        context.DrawEllipse(haloBrush, ringPen, screenPt, 7.0 * Zoom, 7.0 * Zoom);

        if (Circuit == null) return;
        int nodeId = pinInfo.Pin.NodeId;
        Circuit.Nodes.TryGetValue(nodeId, out var node);
        double v = node?.Voltage ?? 0.0;
        bool isGnd = node?.IsGround == true;

        string vStr = isGnd ? "GND (0.0 V)" : (Math.Abs(v) < 1e-3 ? $"{v * 1e6:F1} µV" : Math.Abs(v) < 1.0 ? $"{v * 1e3:F2} mV" : $"{v:F4} V");
        string title = $"{pinInfo.Device.Type.ToUpperInvariant()} #{pinInfo.Device.Id} • Pin {pinInfo.PinIndex}";
        string info = $"Node #{nodeId} | V = {vStr}";

        var ftTitle = new FormattedText(
            title,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10.0,
            new SolidColorBrush(Color.FromRgb(56, 189, 248))
        );
        var ftInfo = new FormattedText(
            info,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            9.5,
            new SolidColorBrush(Color.FromRgb(241, 245, 249))
        );

        double boxW = Math.Max(ftTitle.Width, ftInfo.Width) + 16;
        double boxH = ftTitle.Height + ftInfo.Height + 8;
        double boxX = screenPt.X + 12;
        double boxY = screenPt.Y - boxH / 2.0;

        var tipRect = new Rect(boxX, boxY, boxW, boxH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), tipRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(100, 56, 189, 248)), 1), tipRect, 4);
        context.DrawText(ftTitle, new Point(boxX + 8, boxY + 3));
        context.DrawText(ftInfo, new Point(boxX + 8, boxY + 5 + ftTitle.Height));
    }

    private void DrawEdaHud(DrawingContext context, double width, double height)
    {
        if (Circuit == null) return;

        double gx = Math.Round(_lastCursorWorldPos.Wx * 10.0) / 10.0;
        double gy = Math.Round(_lastCursorWorldPos.Wy * 10.0) / 10.0;

        var ftIcon = new FormattedText(
            "⚡ CAD",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            new SolidColorBrush(Color.FromRgb(56, 189, 248))
        );

        string coords = $"X {gx:F1}  Y {gy:F1}";
        var ftCoords = new FormattedText(
            coords,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas,SF Mono,Menlo,monospace", FontStyle.Normal, FontWeight.SemiBold),
            10.0,
            new SolidColorBrush(Color.FromRgb(241, 245, 249))
        );

        string gridZoom = $"50 mil • {(int)(Zoom * 100)}%";
        var ftGrid = new FormattedText(
            gridZoom,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            9.5,
            new SolidColorBrush(Color.FromRgb(148, 163, 184))
        );

        string stats = $"DEV {Circuit.Devices.Count} • NODE {Circuit.Nodes.Count} • WIRE {Circuit.Wires.Count}";
        var ftStats = new FormattedText(
            stats,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Medium),
            9.5,
            new SolidColorBrush(Color.FromRgb(100, 116, 139))
        );

        double padX = 10.0;
        double sepW = 12.0;
        double totalW = padX * 2 + ftIcon.Width + sepW + ftCoords.Width + sepW + ftGrid.Width + sepW + ftStats.Width;
        double totalH = 24.0;

        var hudRect = new Rect(12, 12, totalW, totalH);
        // Backdrop
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), hudRect, 6);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(80, 51, 65, 85)), 1), hudRect, 6);

        double curX = hudRect.X + padX;
        context.DrawText(ftIcon, new Point(curX, hudRect.Y + (totalH - ftIcon.Height) / 2.0));
        curX += ftIcon.Width + sepW / 2.0;

        var sepPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1);
        context.DrawLine(sepPen, new Point(curX, hudRect.Y + 5), new Point(curX, hudRect.Y + totalH - 5));
        curX += sepW / 2.0;

        context.DrawText(ftCoords, new Point(curX, hudRect.Y + (totalH - ftCoords.Height) / 2.0));
        curX += ftCoords.Width + sepW / 2.0;

        context.DrawLine(sepPen, new Point(curX, hudRect.Y + 5), new Point(curX, hudRect.Y + totalH - 5));
        curX += sepW / 2.0;

        context.DrawText(ftGrid, new Point(curX, hudRect.Y + (totalH - ftGrid.Height) / 2.0));
        curX += ftGrid.Width + sepW / 2.0;

        context.DrawLine(sepPen, new Point(curX, hudRect.Y + 5), new Point(curX, hudRect.Y + totalH - 5));
        curX += sepW / 2.0;

        context.DrawText(ftStats, new Point(curX, hudRect.Y + (totalH - ftStats.Height) / 2.0));
    }

    private void DrawParticles(DrawingContext context)
    {
        var particleBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        var particleGlow = new SolidColorBrush(Color.FromArgb(60, 251, 191, 36));

        foreach (var branch in ParticleEngine.Branches)
        {
            double cur = branch.CurrentProvider != null ? branch.CurrentProvider() : (branch.Device?.Current ?? 0.0);
            if (Math.Abs(cur) < 1e-9) continue;

            foreach (var p in branch.Particles)
            {
                var (wx, wy) = branch.GetParticlePosition(p);
                var screenPt = WorldToScreen(wx, wy);

                context.DrawEllipse(particleGlow, null, screenPt, 3.5 * Zoom, 3.5 * Zoom);
                context.DrawEllipse(particleBrush, null, screenPt, 1.8 * Zoom, 1.8 * Zoom);
            }
        }
    }

    private (double X, double Y) GetClosestPointOnWire(CircuitWire wire, double wx, double wy)
    {
        if (Circuit == null)
            return (Math.Round(wx), Math.Round(wy));

        var pts = wire.PathPoints;
        if (pts == null || pts.Count < 2)
        {
            var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (dev1 != null && dev2 != null)
            {
                pts = SchematicWireRouter.RouteWire(Circuit, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
                wire.PathPoints = pts;
            }
        }

        if (pts == null || pts.Count < 2)
            return (Math.Round(wx), Math.Round(wy));

        double bestDist = double.MaxValue;
        (double X, double Y) bestPoint = (Math.Round(wx), Math.Round(wy));

        for (int i = 0; i < pts.Count - 1; i++)
        {
            double x1 = pts[i].X;
            double y1 = pts[i].Y;
            double x2 = pts[i + 1].X;
            double y2 = pts[i + 1].Y;

            double l2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
            if (l2 < 1e-9) continue;

            double t = Math.Clamp(((wx - x1) * (x2 - x1) + (wy - y1) * (y2 - y1)) / l2, 0.0, 1.0);
            double projX = Math.Round(x1 + t * (x2 - x1));
            double projY = Math.Round(y1 + t * (y2 - y1));

            double d = (wx - projX) * (wx - projX) + (wy - projY) * (wy - projY);
            if (d < bestDist)
            {
                bestDist = d;
                bestPoint = (projX, projY);
            }
        }

        return bestPoint;
    }

    public Device? FindDeviceAt(double wx, double wy)
    {
        if (Circuit == null) return null;
        return Circuit.Devices.FirstOrDefault(d =>
        {
            // Transform (wx, wy) into device's unrotated local coordinates:
            double dx = wx - d.LocationX;
            double dy = wy - d.LocationY;
            double rad = -(int)d.Transform.Rotation * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;

            // Tight shape hit testing based on device descriptor and type
            double hitHalfW;
            double hitHalfH;

            switch (d.Type.ToLowerInvariant())
            {
                case "resistor":
                case "inductor":
                    hitHalfW = 0.48;
                    hitHalfH = 1.15;
                    break;
                case "diode":
                case "zener":
                case "led":
                    hitHalfW = 0.55;
                    hitHalfH = 1.15;
                    break;
                case "capacitor":
                    hitHalfW = 0.65;
                    hitHalfH = 1.15;
                    break;
                case "vsource":
                case "isource":
                    hitHalfW = 0.70;
                    hitHalfH = 1.15;
                    break;
                case "ground":
                    hitHalfW = 0.45;
                    hitHalfH = 0.80;
                    break;
                case "switch":
                case "spst":
                    hitHalfW = 0.45;
                    hitHalfH = 1.15;
                    break;
                case "spdt":
                    hitHalfW = 1.15;
                    hitHalfH = 1.15;
                    break;
                default:
                    var desc = DeviceRegistry.GetDescriptor(d);
                    hitHalfW = (desc?.BoundingBoxSize.Width ?? 2.0) / 2.0 + 0.10;
                    hitHalfH = (desc?.BoundingBoxSize.Height ?? 2.0) / 2.0 + 0.10;
                    break;
            }

            return Math.Abs(localX) <= hitHalfW && Math.Abs(localY) <= hitHalfH;
        });
    }

    private CircuitWire? FindWireAt(double wx, double wy, double tolerance = 0.4)
    {
        if (Circuit == null) return null;

        foreach (var wire in Circuit.Wires)
        {
            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2)
            {
                var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
                var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
                if (dev1 == null || dev2 == null) continue;
                pts = SchematicWireRouter.RouteWire(Circuit, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
                wire.PathPoints = pts;
            }

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (PointToLineDistance(wx, wy, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y) <= tolerance)
                {
                    return wire;
                }
            }
        }
        return null;
    }

    private static double PointToLineDistance(double px, double py, double x1, double y1, double x2, double y2)
    {
        double l2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
        if (l2 < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        double t = Math.Clamp(((px - x1) * (x2 - x1) + (py - y1) * (y2 - y1)) / l2, 0.0, 1.0);
        double projX = x1 + t * (x2 - x1);
        double projY = y1 + t * (y2 - y1);
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    public static (Color Glow, Color Core) GetVoltageColors(double v, bool isGround)
    {
        if (isGround || Math.Abs(v) < 1e-3)
        {
            // Ground / 0V: Cool Slate Silver (#94A3B8)
            return (Color.FromArgb(40, 148, 163, 184), Color.FromRgb(148, 163, 184));
        }

        if (v < -0.05)
        {
            // Negative potential: Soft Lavender-Violet (#A78BFA)
            return (Color.FromArgb(45, 167, 139, 250), Color.FromRgb(167, 139, 250));
        }

        // Positive potential tiers:
        if (v <= 1.2)
        {
            // Tier 1 (0.001V - 1.2V): Mint Emerald (#34D399)
            return (Color.FromArgb(45, 52, 211, 153), Color.FromRgb(52, 211, 153));
        }
        if (v <= 3.3)
        {
            // Tier 2 (1.2V - 3.3V): Sky Blue / Cyan (#38BDF8)
            return (Color.FromArgb(45, 56, 189, 248), Color.FromRgb(56, 189, 248));
        }
        if (v <= 5.5)
        {
            // Tier 3 (3.3V - 5.5V): Warm Amber (#FBBF24)
            return (Color.FromArgb(45, 251, 191, 36), Color.FromRgb(251, 191, 36));
        }
        if (v <= 12.0)
        {
            // Tier 4 (5.5V - 12.0V): Coral Orange (#FB923C)
            return (Color.FromArgb(45, 251, 146, 60), Color.FromRgb(251, 146, 60));
        }

        // Tier 5 (> 12V): Soft Rose (#F43F5E)
        return (Color.FromArgb(50, 244, 63, 94), Color.FromRgb(244, 63, 94));
    }

    public static string FormatVoltageLabel(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "0 V";
        double abs = Math.Abs(v);
        if (abs < 1e-6) return "0 V";
        if (abs >= 1000.0) return $"{v / 1000.0:G3} kV";
        if (abs >= 10.0) return $"{v:F1} V";
        if (abs >= 0.995)
        {
            double rounded = Math.Round(v, 2);
            if (Math.Abs(rounded - Math.Round(rounded)) < 0.02)
                return $"{Math.Round(rounded):F0} V";
            return $"{rounded:F2} V";
        }
        if (abs >= 1e-3)
        {
            double mV = v * 1000.0;
            if (Math.Abs(mV) >= 10.0)
                return $"{mV:F0} mV";
            return $"{mV:F2} mV";
        }
        return $"{v * 1e6:F1} µV";
    }

    public static string FormatCurrentLabel(double i)
    {
        if (double.IsNaN(i) || double.IsInfinity(i)) return "0 A";
        double abs = Math.Abs(i);
        if (abs < 1e-6) return "0 A";
        if (abs >= 0.995)
        {
            double rounded = Math.Round(abs, 2);
            if (Math.Abs(rounded - Math.Round(rounded)) < 0.02)
                return $"{Math.Round(rounded):F0} A";
            return $"{rounded:F2} A";
        }
        if (abs >= 1e-3)
        {
            double mA = abs * 1000.0;
            if (mA >= 100.0)
            {
                return $"{Math.Round(mA):F0} mA";
            }
            if (mA >= 10.0)
            {
                double rMA = Math.Round(mA, 1);
                if (Math.Abs(rMA - Math.Round(rMA)) < 0.05)
                    return $"{Math.Round(rMA):F0} mA";
                return $"{rMA:F1} mA";
            }
            return $"{mA:F1} mA";
        }
        if (abs >= 1e-6)
        {
            double uA = abs * 1e6;
            return $"{uA:F1} µA";
        }
        return $"{abs * 1e9:F0} nA";
    }

    private void DrawCurrentAnnotations(DrawingContext context)
    {
        if (Circuit == null) return;

        foreach (var dev in Circuit.Devices)
        {
            if (dev.Type == "ground" || dev.Type == "netlabel" || dev is LogicGateDevice || dev.Type is "voltmeter" or "ammeter" or "ohmmeter" || dev.Pins.Count != 2) continue;

            var (p0x, p0y) = dev.GetAbsolutePinPosition(0);
            var (p1x, p1y) = dev.GetAbsolutePinPosition(1);

            double cx = (p0x + p1x) / 2.0;
            double cy = (p0y + p1y) / 2.0;

            double dx = p1x - p0x;
            double dy = p1y - p0y;

            double current = dev.Current;
            bool isZero = Math.Abs(current) < 1e-6;
            string text = FormatCurrentLabel(Math.Abs(current));

            bool isVertical = Math.Abs(dy) >= Math.Abs(dx);
            if (isVertical)
            {
                int dirSign = dy >= 0 ? 1 : -1;
                // For voltage sources, positive current means flowing OUT of pin 0 (from pin 1 to pin 0)
                int flowDir = (dev is VoltageSourceDevice)
                    ? (current >= 0 ? -dirSign : dirSign)
                    : (current >= 0 ? dirSign : -dirSign);

                double xOff = dev.Type switch
                {
                    "capacitor" => -0.66,
                    "vsource" or "isource" => -0.66,
                    _ => -0.56
                };
                var badgePos = WorldToScreen(cx + xOff, cy);
                DrawBranchCurrentBadge(context, badgePos, new Point(0, flowDir), text, isZero);
            }
            else
            {
                int dirSign = dx >= 0 ? 1 : -1;
                int flowDir = (dev is VoltageSourceDevice)
                    ? (current >= 0 ? -dirSign : dirSign)
                    : (current >= 0 ? dirSign : -dirSign);

                var badgePos = WorldToScreen(cx, cy - 0.62);
                DrawBranchCurrentBadge(context, badgePos, new Point(flowDir, 0), text, isZero);
            }
        }
    }

    private void DrawBranchCurrentBadge(DrawingContext context, Point screenPt, Point dir, string text, bool isZero)
    {
        var textBrush = isZero
            ? new SolidColorBrush(Color.FromRgb(148, 163, 184))
            : new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Refined Mint/Emerald #34D399

        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas,SF Mono,Menlo,monospace", FontStyle.Normal, FontWeight.SemiBold),
            8.5 * Zoom,
            textBrush
        );

        double arrowLen = isZero ? 0.0 : 8.0 * Zoom;
        double spacing = isZero ? 0.0 : 3.0 * Zoom;
        double padX = 4.0 * Zoom;
        double padY = 1.5 * Zoom;
        double totalW = arrowLen + spacing + ft.Width + padX * 2;
        double totalH = Math.Max(arrowLen, ft.Height) + padY * 2;

        var badgeRect = new Rect(screenPt.X - totalW / 2.0, screenPt.Y - totalH / 2.0, totalW, totalH);
        float r = (float)(3.5 * Zoom);

        var bgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42));
        var borderPen = isZero
            ? new Pen(new SolidColorBrush(Color.FromArgb(70, 51, 65, 85)), 1.0 * Zoom)
            : new Pen(new SolidColorBrush(Color.FromArgb(80, 52, 211, 153)), 1.0 * Zoom);

        context.DrawRectangle(bgBrush, borderPen, badgeRect, r, r);

        if (!isZero)
        {
            double arrowCenterX = badgeRect.X + padX + arrowLen / 2.0;
            double arrowCenterY = screenPt.Y;
            DrawDirectionArrow(context, new Point(arrowCenterX, arrowCenterY), dir, arrowLen, Color.FromRgb(52, 211, 153));
        }

        double textX = badgeRect.X + padX + arrowLen + spacing;
        double textY = screenPt.Y - ft.Height / 2.0;
        context.DrawText(ft, new Point(textX, textY));
    }

    private void DrawDirectionArrow(DrawingContext context, Point center, Point dir, double length, Color color)
    {
        double len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (len < 1e-4) return;
        double nx = dir.X / len;
        double ny = dir.Y / len;

        double halfLen = length * 0.45;
        var pStart = new Point(center.X - nx * halfLen, center.Y - ny * halfLen);
        var pEnd = new Point(center.X + nx * halfLen, center.Y + ny * halfLen);

        var stemPen = new Pen(new SolidColorBrush(color), 1.6 * Zoom)
        {
            LineCap = PenLineCap.Round
        };
        context.DrawLine(stemPen, pStart, pEnd);

        double headSize = 4.2 * Zoom;
        double perpX = -ny;
        double perpY = nx;

        var tip = pEnd;
        var left = new Point(tip.X - nx * headSize + perpX * (headSize * 0.75), tip.Y - ny * headSize + perpY * (headSize * 0.75));
        var right = new Point(tip.X - nx * headSize - perpX * (headSize * 0.75), tip.Y - ny * headSize - perpY * (headSize * 0.75));

        var headGeom = new StreamGeometry();
        using (var ctx = headGeom.Open())
        {
            ctx.BeginFigure(tip, isFilled: true);
            ctx.LineTo(left);
            ctx.LineTo(right);
            ctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(color), null, headGeom);
    }

    private void DrawVoltageAnnotations(DrawingContext context)
    {
        if (Circuit == null) return;

        var placedRects = new List<Rect>();
        var annotatedNodes = new HashSet<int>();

        // 1. Solder junctions (points where 3 or more wires meet)
        foreach (var (key, dirSet) in _lastDirectionsAtPoint)
        {
            if (dirSet.Count >= 3 && _lastJunctionNodeIds.TryGetValue(key, out int nodeId))
            {
                Circuit.Nodes.TryGetValue(nodeId, out var node);
                bool isGnd = node?.IsGround == true;

                // If ground node already has an explicit ground symbol on the circuit, skip solder junction 0V badge
                if (isGnd && Circuit.Devices.Any(d => d.Type == "ground" && d.Pins.Any(p => p.NodeId == nodeId)))
                {
                    annotatedNodes.Add(nodeId);
                    continue;
                }

                // Deduplicate: avoid placing multiple identical badges for the same electrical node
                if (annotatedNodes.Contains(nodeId))
                {
                    continue;
                }

                double v = node?.Voltage ?? 0.0;

                // Pick an open candidate direction that does not have an incident wire
                double offX = 0;
                double offY = 0;
                if (!dirSet.Contains((0, -1)))
                {
                    offY = -0.65;      // Top is open
                    if (dirSet.Contains((-1, 0))) offX = 0.55;
                }
                else if (!dirSet.Contains((1, 0))) offX = 0.85;   // Right is open
                else if (!dirSet.Contains((-1, 0))) offX = -0.85; // Left is open
                else if (!dirSet.Contains((0, 1)))
                {
                    offY = 0.65;   // Bottom is open
                    if (dirSet.Contains((-1, 0))) offX = 0.55;
                }
                else offY = -0.65;

                var screenPos = WorldToScreen(key.X + offX, key.Y + offY);

                // Check collision against already placed voltage labels
                if (placedRects.Any(r => Math.Abs(r.Center.X - screenPos.X) < 42 * Zoom && Math.Abs(r.Center.Y - screenPos.Y) < 24 * Zoom))
                {
                    continue;
                }

                var rect = DrawNodeVoltageBadge(context, screenPos, v, isGnd);
                if (rect.HasValue)
                {
                    placedRects.Add(rect.Value);
                    annotatedNodes.Add(nodeId);
                }
            }
        }

        // 2. Intermediate series nodes (e.g. between cascaded LEDs or ladder dividers)
        foreach (var (nodeId, node) in Circuit.Nodes)
        {
            if (annotatedNodes.Contains(nodeId)) continue;
            if (node.IsGround && Circuit.Devices.Any(d => d.Type == "ground" && d.Pins.Any(p => p.NodeId == nodeId)))
            {
                annotatedNodes.Add(nodeId);
                continue;
            }

            Point? bestCandidate = null;
            double bestLen = 0.0;

            // Strategy A: Find longest wire segment belonging to this electrical node
            foreach (var wire in Circuit.Wires)
            {
                if (wire.NodeId != nodeId) continue;

                var pts = wire.PathPoints;
                if (pts == null || pts.Count < 2)
                {
                    // Fall back to pin endpoints if PathPoints not populated
                    var d1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
                    var d2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
                    if (d1 != null && d2 != null)
                    {
                        var (p1x, p1y) = d1.GetAbsolutePinPosition(wire.FromPinIndex);
                        var (p2x, p2y) = d2.GetAbsolutePinPosition(wire.ToPinIndex);
                        pts = new List<(double X, double Y)> { (p1x, p1y), (p2x, p2y) };
                    }
                }

                if (pts == null || pts.Count < 2) continue;

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var pA = pts[i];
                    var pB = pts[i + 1];
                    double dx = pB.X - pA.X;
                    double dy = pB.Y - pA.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);

                    bool isHoriz = Math.Abs(dx) >= Math.Abs(dy);
                    double effectiveLen = isHoriz ? len * 1.25 : len;
                    if (effectiveLen > bestLen && len >= 1.0)
                    {
                        bestLen = effectiveLen;
                        double mx = (pA.X + pB.X) / 2.0;
                        double my = (pA.Y + pB.Y) / 2.0;

                        if (isHoriz)
                        {
                            // Horizontal segment: place badge cleanly above the wire
                            bestCandidate = new Point(mx, my - 0.65);
                        }
                        else
                        {
                            // Vertical segment: place badge cleanly to the right of the wire
                            bestCandidate = new Point(mx + 0.75, my);
                        }
                    }
                }
            }

            // Strategy B: Fallback to connected pins average if no wires found
            if (!bestCandidate.HasValue)
            {
                var connectedPins = new List<Point>();
                foreach (var dev in Circuit.Devices)
                {
                    for (int p = 0; p < dev.Pins.Count; p++)
                    {
                        if (dev.Pins[p].NodeId == nodeId)
                        {
                            var (px, py) = dev.GetAbsolutePinPosition(p);
                            connectedPins.Add(new Point(px, py));
                        }
                    }
                }

                if (connectedPins.Count >= 2)
                {
                    bestCandidate = new Point(connectedPins.Average(p => p.X) + 0.6, connectedPins.Average(p => p.Y) - 0.5);
                }
            }

            if (bestCandidate.HasValue)
            {
                var screenPos = WorldToScreen(bestCandidate.Value.X, bestCandidate.Value.Y);
                bool collides = placedRects.Any(r => Math.Abs(r.Center.X - screenPos.X) < 42 * Zoom && Math.Abs(r.Center.Y - screenPos.Y) < 24 * Zoom);
                if (!collides)
                {
                    var rect = DrawNodeVoltageBadge(context, screenPos, node.Voltage, node.IsGround);
                    if (rect.HasValue)
                    {
                        placedRects.Add(rect.Value);
                        annotatedNodes.Add(nodeId);
                    }
                }
            }
        }
    }

    private Rect? DrawNodeVoltageBadge(DrawingContext context, Point screenPt, double voltage, bool isGround)
    {
        var (glowColor, coreColor) = GetVoltageColors(voltage, isGround);
        string text = FormatVoltageLabel(voltage);

        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas,SF Mono,Menlo,monospace", FontStyle.Normal, FontWeight.SemiBold),
            9.0 * Zoom,
            new SolidColorBrush(coreColor)
        );

        double padX = 5.0 * Zoom;
        double padY = 2.0 * Zoom;
        double w = ft.Width + padX * 2;
        double h = ft.Height + padY * 2;

        var rect = new Rect(screenPt.X - w / 2.0, screenPt.Y - h / 2.0, w, h);
        float r = (float)(3.5 * Zoom);

        var bgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 51, 65, 85)), 1.0 * Zoom);
        context.DrawRectangle(bgBrush, borderPen, rect, r, r);

        context.DrawText(ft, new Point(rect.X + padX, rect.Y + padY));
        return rect;
    }

    private static string GetDeviceLabel(Device dev)
    {
        return DeviceRegistry.FormatLabel(dev);
    }
}
