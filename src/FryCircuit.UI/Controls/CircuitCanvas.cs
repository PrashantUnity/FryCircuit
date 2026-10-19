using System;
using System.Collections.Generic;
using System.Linq;
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

public partial class CircuitCanvas : Control
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

    internal bool _needsZoomToFit = true;
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

        CircuitProperty.Changed.AddClassHandler<CircuitCanvas>((canvas, e) =>
        {
            var newCircuit = e.GetNewValue<Circuit?>();
            if (newCircuit != null)
            {
                void Update()
                {
                    canvas.ParticleEngine.SyncWithCircuit(newCircuit);
                    canvas._needsZoomToFit = true;
                    canvas.ZoomToFit();
                    canvas.InvalidateVisual();
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
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CircuitProperty)
        {
            var newCircuit = change.GetNewValue<Circuit?>();
            if (newCircuit != null)
            {
                void Update()
                {
                    ParticleEngine.SyncWithCircuit(newCircuit);
                    _needsZoomToFit = true;
                    ZoomToFit();
                    InvalidateVisual();
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
        }
    }

    public static HashSet<CircuitCanvas> ActiveCanvases { get; } = new();

    public CircuitCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        FryCircuit.UI.Theming.CircuitThemeService.Instance.TokensChanged += _ => InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActiveCanvases.Add(this);
        if (Circuit != null)
        {
            ParticleEngine.SyncWithCircuit(Circuit);
            _needsZoomToFit = true;
            ZoomToFit();
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActiveCanvases.Remove(this);
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

        var affectedNodes = devsToDelete.SelectMany(d => d.Pins.Select(p => p.NodeId)).Distinct().Where(n => n >= 0).ToList();

        foreach (var dev in devsToDelete)
        {
            var attachedWires = Circuit.Wires.Where(w => w.FromDeviceId == dev.Id || w.ToDeviceId == dev.Id).ToList();
            actions.Add(new RemoveDeviceAction(Circuit, dev, attachedWires));
            Circuit.RemoveDevice(dev);
        }
        SelectedDevices.Clear();
        SelectedDevice = null;

        foreach (var nid in affectedNodes)
        {
            Circuit.ResynthesizeNode(nid);
        }
        Circuit.RouteAllWires();

        if (actions.Count == 1)
            HistoryManager?.Record(actions[0]);
        else if (actions.Count > 1)
            HistoryManager?.Record(new BatchCircuitAction($"Delete {actions.Count} elements", actions));

        ParticleEngine.SyncWithCircuit(Circuit);
        InvalidateVisual();
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
}
