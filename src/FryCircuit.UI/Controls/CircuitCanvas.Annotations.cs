using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.UI.Controls;

public partial class CircuitCanvas
{
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

    private void DrawCurrentAnnotations(DrawingContext context)
    {
        if (Circuit == null) return;

        var occupiedPositions = new List<Point>();

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
                // For voltage sources and batteries, positive current means flowing OUT of pin 0 (from pin 1 to pin 0)
                int flowDir = (dev is VoltageSourceDevice or BatteryCellDevice)
                    ? (current >= 0 ? -dirSign : dirSign)
                    : (current >= 0 ? dirSign : -dirSign);

                double xOff = dev.Type switch
                {
                    "capacitor" => -0.66,
                    "vsource" or "isource" or "battery" => -0.66,
                    _ => -0.56
                };
                var badgePos = WorldToScreen(cx + xOff, cy);
                occupiedPositions.Add(badgePos);
                DrawBranchCurrentBadge(context, badgePos, new Point(0, flowDir), text, isZero);
            }
            else
            {
                int dirSign = dx >= 0 ? 1 : -1;
                int flowDir = (dev is VoltageSourceDevice or BatteryCellDevice)
                    ? (current >= 0 ? -dirSign : dirSign)
                    : (current >= 0 ? dirSign : -dirSign);

                var badgePos = WorldToScreen(cx, cy - 0.62);
                occupiedPositions.Add(badgePos);
                DrawBranchCurrentBadge(context, badgePos, new Point(flowDir, 0), text, isZero);
            }
        }

        // 2. Return rails and distribution buses (wires not bridging two already-annotated components)
        var annotatedCurrentNodes = new HashSet<int>();

        // Find candidate wires that have valid physical current
        var candidateWires = new List<(CircuitWire Wire, Device Dev1, Device Dev2, double Current, double MaxSegLen, (double X, double Y) SegA, (double X, double Y) SegB, bool IsSourceReturn, bool IsGroundStub)>();

        foreach (var wire in Circuit.Wires)
        {
            var dev1 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dev2 = Circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (dev1 == null || dev2 == null) continue;

            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2) continue;

            bool isGroundStub = dev1.Type is "ground" or "netlabel" || dev2.Type is "ground" or "netlabel";
            bool isSourceReturn = (dev1 is VoltageSourceDevice or BatteryCellDevice && wire.FromPinIndex == 1) ||
                                  (dev2 is VoltageSourceDevice or BatteryCellDevice && wire.ToPinIndex == 1);

            bool dev1HasBadge = dev1.Type is not "ground" and not "netlabel" && dev1 is not LogicGateDevice && dev1.Type is not "voltmeter" and not "ammeter" and not "ohmmeter" && dev1.Pins.Count == 2;
            bool dev2HasBadge = dev2.Type is not "ground" and not "netlabel" && dev2 is not LogicGateDevice && dev2.Type is not "voltmeter" and not "ammeter" and not "ohmmeter" && dev2.Pins.Count == 2;

            if (dev1HasBadge && dev2HasBadge && !isSourceReturn) continue;

            double wireCurrent = ParticleFlowEngine.CalculateWireCurrent(Circuit, wire);
            // Strictly exclude zero-current wires (e.g. dead-end stubs to Ground symbols or open pins)
            if (Math.Abs(wireCurrent) < 1e-6) continue;

            // Find longest straight segment in this wire
            double maxSegLen = 0.0;
            (double X, double Y) segA = (0, 0);
            (double X, double Y) segB = (0, 0);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                double sx = pts[i + 1].X - pts[i].X;
                double sy = pts[i + 1].Y - pts[i].Y;
                double slen = Math.Sqrt(sx * sx + sy * sy);
                if (slen > maxSegLen)
                {
                    maxSegLen = slen;
                    segA = pts[i];
                    segB = pts[i + 1];
                }
            }

            if (maxSegLen < 2.0) continue;

            candidateWires.Add((wire, dev1, dev2, wireCurrent, maxSegLen, segA, segB, isSourceReturn, isGroundStub));
        }

        // Sort candidates:
        // 1. Prefer wires connected directly to power source return terminal (Pin 1)
        // 2. Prefer wires not terminating at ground symbols
        // 3. Prefer highest current (main trunk)
        // 4. Prefer longest straight segment
        var sortedCandidates = candidateWires
            .OrderByDescending(c => c.IsSourceReturn ? 1 : 0)
            .ThenByDescending(c => c.IsGroundStub ? 0 : 1)
            .ThenByDescending(c => Math.Abs(c.Current))
            .ThenByDescending(c => c.MaxSegLen);

        foreach (var cand in sortedCandidates)
        {
            if (annotatedCurrentNodes.Contains(cand.Wire.NodeId)) continue;

            double mx = (cand.SegA.X + cand.SegB.X) / 2.0;
            double my = (cand.SegA.Y + cand.SegB.Y) / 2.0;
            double sdx = cand.SegB.X - cand.SegA.X;
            double sdy = cand.SegB.Y - cand.SegA.Y;
            bool isHoriz = Math.Abs(sdx) >= Math.Abs(sdy);

            // Flow direction along the wire from segA towards segB
            int currentSign = cand.Current >= 0 ? 1 : -1;
            Point flowDir = isHoriz
                ? new Point(currentSign * Math.Sign(sdx), 0)
                : new Point(0, currentSign * Math.Sign(sdy));

            // Offset badge from wire line: below wire for horizontal, left of wire for vertical
            Point badgeWorldPos = isHoriz
                ? new Point(mx, my + 0.62)
                : new Point(mx - 0.66, my);

            var badgePos = WorldToScreen(badgeWorldPos.X, badgeWorldPos.Y);

            // Ensure not colliding with existing device badges
            if (occupiedPositions.Any(p => Math.Sqrt((p.X - badgePos.X) * (p.X - badgePos.X) + (p.Y - badgePos.Y) * (p.Y - badgePos.Y)) < 60 * Zoom))
                continue;

            occupiedPositions.Add(badgePos);
            annotatedCurrentNodes.Add(cand.Wire.NodeId);
            string text = FormatCurrentLabel(Math.Abs(cand.Current));
            DrawBranchCurrentBadge(context, badgePos, flowDir, text, false);
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
}
