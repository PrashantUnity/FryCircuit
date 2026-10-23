using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using FryCircuit.UI.Rendering;

namespace FryCircuit.UI.Controls;

public partial class CircuitCanvas
{
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
                    var (p0x, p0y) = dev.GetAbsolutePinPosition(0);
                    var (p1x, p1y) = dev.GetAbsolutePinPosition(1);
                    isVerticalTwoPin = Math.Abs(p1x - p0x) < 0.1 && Math.Abs(p1y - p0y) > 0.1;
                }

                if (isVerticalTwoPin)
                {
                    labelX = center.X + (devW / 2.0) + 12 * Zoom;
                    labelY = center.Y - (ft.Height / 2.0);
                }
                else if (dev.Type == "vsource" || dev.Type == "isource")
                {
                    labelX = center.X + (devW / 2.0) + 12 * Zoom;
                    labelY = center.Y - (ft.Height / 2.0);
                }
                else
                {
                    labelX = center.X - (ft.Width / 2.0);
                    labelY = center.Y + (devH / 2.0) + 10 * Zoom;
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

        // Highlight colliding device footprint if in collision
        if (_isPlacementCollision && _placementCollisionDevice != null)
        {
            var (cMinX, cMaxX, cMinY, cMaxY) = _placementCollisionDevice.GetBoundingBox(padding: 0.15);
            var p1 = WorldToScreen(cMinX, cMinY);
            var p2 = WorldToScreen(cMaxX, cMaxY);
            var cRect = new Rect(p1, p2);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(45, 244, 63, 94)), cRect, (float)(4 * Zoom));
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.5 * Zoom) { DashStyle = DashStyle.Dash }, cRect, (float)(4 * Zoom));
        }

        var renderCtx = new DeviceRenderContext
        {
            Zoom = Zoom,
            GridSize = GridSize,
            IsPlacingGhost = true,
            IsDragCollision = _isPlacementCollision
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
        var ringColor = _isPlacementCollision
            ? Color.FromRgb(244, 63, 94) // Crimson warning red
            : Color.FromArgb(120, 56, 189, 248); // Cyan

        var ghostRingPen = new Pen(new SolidColorBrush(ringColor), 1.3)
        {
            DashStyle = DashStyle.Dash
        };
        double ringR = GridSize * Zoom * 0.9;
        context.DrawEllipse(null, ghostRingPen, center, ringR, ringR);

        // Cyber crosshair ticks
        var tickPen = new Pen(new SolidColorBrush(ringColor), 1.3);
        double tickLen = 5 * Zoom;
        context.DrawLine(tickPen, new Point(center.X - ringR - tickLen, center.Y), new Point(center.X - ringR + 2, center.Y));
        context.DrawLine(tickPen, new Point(center.X + ringR - 2, center.Y), new Point(center.X + ringR + tickLen, center.Y));
        context.DrawLine(tickPen, new Point(center.X, center.Y - ringR - tickLen), new Point(center.X, center.Y - ringR + 2));
        context.DrawLine(tickPen, new Point(center.X, center.Y + ringR - 2), new Point(center.X, center.Y + ringR + tickLen));

        // Placement HUD Tooltip
        string hudText;
        Color hudBg;
        Color hudBorder;
        Color hudTextCol;

        if (_isPlacementCollision)
        {
            string collName = _placementCollisionDevice?.Type.ToUpperInvariant() ?? "COMPONENT";
            hudText = $"⚠ Cannot Place: Overlaps {collName}";
            hudBg = Color.FromArgb(245, 69, 10, 10);
            hudBorder = Color.FromRgb(244, 63, 94);
            hudTextCol = Color.FromRgb(254, 205, 211);
        }
        else
        {
            hudText = $"X: {_placementCursorPos.Wx:F0}  Y: {_placementCursorPos.Wy:F0}  [R: {(int)_placementRotation}°]  •  Space: Rotate";
            hudBg = Color.FromArgb(235, 15, 23, 42);
            hudBorder = Color.FromArgb(100, 56, 189, 248);
            hudTextCol = Color.FromRgb(56, 189, 248);
        }

        var ftHud = new FormattedText(
            hudText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            10.0,
            new SolidColorBrush(hudTextCol)
        );
        double hudW = ftHud.Width + 14;
        double hudH = ftHud.Height + 8;
        var hudRect = new Rect(center.X + ringR + 8, center.Y - hudH / 2.0, hudW, hudH);
        context.FillRectangle(new SolidColorBrush(hudBg), hudRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(hudBorder), 1.2), hudRect, 4);
        context.DrawText(ftHud, new Point(hudRect.X + 7, hudRect.Y + 4));
    }

    private void DrawCollisionOverlays(DrawingContext context)
    {
        // 1. Device dragging collision overlay
        if (_isDraggingDevice && _isDragCollision && _dragCollisionDevice != null)
        {
            var (cMinX, cMaxX, cMinY, cMaxY) = _dragCollisionDevice.GetBoundingBox(padding: 0.15);
            var p1 = WorldToScreen(cMinX, cMinY);
            var p2 = WorldToScreen(cMaxX, cMaxY);
            var cRect = new Rect(p1, p2);

            context.FillRectangle(new SolidColorBrush(Color.FromArgb(45, 244, 63, 94)), cRect, (float)(4 * Zoom));
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.5 * Zoom) { DashStyle = DashStyle.Dash }, cRect, (float)(4 * Zoom));

            // Floating warning chip above dragged component
            if (_draggedDevice != null)
            {
                var dCenter = WorldToScreen(_draggedDevice.LocationX, _draggedDevice.LocationY);
                string text = $"⚠ Cannot Place (Overlaps {_dragCollisionDevice.Type.ToUpperInvariant()})";
                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                    10.5,
                    new SolidColorBrush(Color.FromRgb(254, 205, 211))
                );

                double bW = ft.Width + 14;
                double bH = ft.Height + 8;
                var bRect = new Rect(dCenter.X - bW / 2.0, dCenter.Y - 32 * Zoom - bH, bW, bH);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(245, 69, 10, 10)), bRect, 4);
                context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.2), bRect, 4);
                context.DrawText(ft, new Point(bRect.X + 7, bRect.Y + 4));
            }
        }

        // 2. Wire dragging collision overlay
        if (_isDraggingWire && _wireCollisionDevice != null)
        {
            var (cMinX, cMaxX, cMinY, cMaxY) = _wireCollisionDevice.GetBoundingBox(padding: 0.15);
            var p1 = WorldToScreen(cMinX, cMinY);
            var p2 = WorldToScreen(cMaxX, cMaxY);
            var cRect = new Rect(p1, p2);

            context.FillRectangle(new SolidColorBrush(Color.FromArgb(45, 244, 63, 94)), cRect, (float)(4 * Zoom));
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.5 * Zoom) { DashStyle = DashStyle.Dash }, cRect, (float)(4 * Zoom));

            var curPos = WorldToScreen(_lastCursorWorldPos.Wx, _lastCursorWorldPos.Wy);
            string text = $"⚠ Wire crosses {_wireCollisionDevice.Type.ToUpperInvariant()}";
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                10.5,
                new SolidColorBrush(Color.FromRgb(254, 205, 211))
            );

            double bW = ft.Width + 14;
            double bH = ft.Height + 8;
            var bRect = new Rect(curPos.X - bW / 2.0, curPos.Y - 28 * Zoom - bH, bW, bH);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(245, 69, 10, 10)), bRect, 4);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.2), bRect, 4);
            context.DrawText(ft, new Point(bRect.X + 7, bRect.Y + 4));
        }
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

    private static string GetDeviceLabel(Device dev)
    {
        return DeviceRegistry.FormatLabel(dev);
    }
}
