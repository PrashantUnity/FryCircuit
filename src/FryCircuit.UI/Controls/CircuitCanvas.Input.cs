using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.History;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using FryCircuit.UI.ViewModels;

namespace FryCircuit.UI.Controls;

public partial class CircuitCanvas
{
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
                    var affectedNodes = devHit.Pins.Select(p => p.NodeId).Distinct().Where(n => n >= 0).ToList();
                    var attached = Circuit.Wires.Where(w => w.FromDeviceId == devHit.Id || w.ToDeviceId == devHit.Id).ToList();
                    HistoryManager?.Record(new RemoveDeviceAction(Circuit, devHit, attached));
                    Circuit.RemoveDevice(devHit);
                    foreach (var nid in affectedNodes)
                    {
                        Circuit.ResynthesizeNode(nid);
                    }
                    Circuit.RouteAllWires();
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

                bool anyActuallyMoved = movedDevs.Any(d =>
                    _multiDragStartPositions.TryGetValue(d, out var startPos)
                        ? (d.LocationX != startPos.X || d.LocationY != startPos.Y)
                        : (d.LocationX != _deviceDragStartPos.X || d.LocationY != _deviceDragStartPos.Y));

                if (anyActuallyMoved)
                {
                    var affectedNodes = movedDevs.SelectMany(d => d.Pins.Select(p => p.NodeId)).Distinct().Where(n => n >= 0);
                    foreach (var nid in affectedNodes)
                    {
                        Circuit.ResynthesizeNode(nid);
                    }
                    Circuit.RouteAllWires();
                    ParticleEngine.SyncWithCircuit(Circuit);
                }
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

        if (e.Handled)
            return;

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

        if (isCtrlOrCmd && e.Key == Key.R)
        {
            MainWindowViewModel.ActiveInstance?.ResetSimulation();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F1)
        {
            MainWindowViewModel.ActiveInstance?.ToggleCadRibbonCommand.Execute(null);
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
                case "fuse":
                    hitHalfW = 0.48;
                    hitHalfH = 1.15;
                    break;
                case "lamp":
                    hitHalfW = 0.70;
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
}
