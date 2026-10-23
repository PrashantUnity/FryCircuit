using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.UI.Controls;

public partial class CircuitCanvas
{
    private void DrawWires(DrawingContext context)
    {
        if (Circuit == null) return;

        // 1. Build net-aware incident directions map
        // Key: (Math.Round(X, 1), Math.Round(Y, 1)) -> (NodeId -> Set of incident directions (dx, dy))
        var netDirections = new Dictionary<(double X, double Y), Dictionary<int, HashSet<(int dx, int dy)>>>();
        var junctionColors = new Dictionary<(double X, double Y), (Color Glow, Color Core)>();

        // Ensure all wires have valid path points
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

            for (int i = 0; i < pts.Count; i++)
            {
                var key = (Math.Round(pts[i].X, 1), Math.Round(pts[i].Y, 1));
                if (!netDirections.TryGetValue(key, out var nodeMap))
                {
                    nodeMap = new Dictionary<int, HashSet<(int, int)>>();
                    netDirections[key] = nodeMap;
                }
                if (!nodeMap.TryGetValue(wire.NodeId, out var dirSet))
                {
                    dirSet = new HashSet<(int, int)>();
                    nodeMap[wire.NodeId] = dirSet;
                }

                junctionColors[key] = (vGlow, vCore);

                // Incident direction from previous point
                if (i > 0)
                {
                    int pdx = Math.Sign(pts[i - 1].X - pts[i].X);
                    int pdy = Math.Sign(pts[i - 1].Y - pts[i].Y);
                    if (pdx != 0 || pdy != 0) dirSet.Add((pdx, pdy));
                }
                // Incident direction to next point
                if (i < pts.Count - 1)
                {
                    int ndx = Math.Sign(pts[i + 1].X - pts[i].X);
                    int ndy = Math.Sign(pts[i + 1].Y - pts[i].Y);
                    if (ndx != 0 || ndy != 0) dirSet.Add((ndx, ndy));
                }
            }
        }

        // Add device pin incident directions per node
        foreach (var dev in Circuit.Devices)
        {
            for (int p = 0; p < dev.Pins.Count; p++)
            {
                var pin = dev.Pins[p];
                var (px, py) = dev.GetAbsolutePinPosition(p);
                var key = (Math.Round(px, 1), Math.Round(py, 1));

                if (netDirections.TryGetValue(key, out var nodeMap) && nodeMap.TryGetValue(pin.NodeId, out var dirSet))
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

        // 2. Identify true solder junctions (>= 3 incident directions on the SAME electrical net)
        var trueJunctionNodeIds = new Dictionary<(double X, double Y), int>();
        var trueDirectionsAtPoint = new Dictionary<(double X, double Y), HashSet<(int dx, int dy)>>();

        foreach (var (key, nodeMap) in netDirections)
        {
            foreach (var (nodeId, dirSet) in nodeMap)
            {
                if (dirSet.Count >= 3)
                {
                    trueJunctionNodeIds[key] = nodeId;
                    trueDirectionsAtPoint[key] = dirSet;
                    break;
                }
            }
        }

        // 3. Segment collection for exact geometric crossing detection
        var verticalSegments = new List<(double X, double MinY, double MaxY, int NodeId, CircuitWire Wire)>();
        foreach (var wire in Circuit.Wires)
        {
            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2) continue;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (Math.Abs(pts[i].X - pts[i + 1].X) < 1e-4)
                {
                    double x = pts[i].X;
                    double minY = Math.Min(pts[i].Y, pts[i + 1].Y);
                    double maxY = Math.Max(pts[i].Y, pts[i + 1].Y);
                    verticalSegments.Add((x, minY, maxY, wire.NodeId, wire));
                }
            }
        }

        // 4. Draw wire segments (with bridge hop arcs for unconnected orthogonal crossings)
        double hopR = Math.Clamp(5.5 * Zoom, 3.5, 9.0);

        foreach (var wire in Circuit.Wires)
        {
            var pts = wire.PathPoints;
            if (pts == null || pts.Count < 2) continue;

            bool isSelected = wire == SelectedWire || SelectedWires.Contains(wire);
            Circuit.Nodes.TryGetValue(wire.NodeId, out var node);
            double voltage = node?.Voltage ?? 0.0;
            bool isGround = node?.IsGround == true;

            bool isHovered = wire == _hoveredWire;
            var probedChannel = ProbedChannels?.FirstOrDefault(c => c.TargetNodeId.HasValue && c.TargetNodeId.Value == wire.NodeId);
            bool isProbed = probedChannel != null || (ProbedNodeCh1.HasValue && wire.NodeId == ProbedNodeCh1.Value) || (ProbedNodeCh2.HasValue && wire.NodeId == ProbedNodeCh2.Value);
            Color probedColor = probedChannel?.Color ?? (ProbedNodeCh1.HasValue && wire.NodeId == ProbedNodeCh1.Value ? Color.FromRgb(245, 158, 11) : Color.FromRgb(6, 182, 212));

            var (glowColor, coreColor) = isSelected
                ? (Color.FromArgb(80, 56, 189, 248), Color.FromRgb(56, 189, 248))
                : isProbed
                    ? (Color.FromArgb(90, probedColor.R, probedColor.G, probedColor.B), probedColor)
                    : isHovered
                        ? (Color.FromArgb(70, 56, 189, 248), Color.FromRgb(186, 230, 253))
                        : GetVoltageColors(voltage, isGround);

            bool hasAura = isSelected || isProbed || isHovered;
            double glowWidth = (isSelected || isProbed ? 4.5 : 3.5) * Zoom;
            double coreWidth = (isSelected || isProbed ? 2.3 : 1.9) * Zoom;

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

                // Guard: if segment is non-orthogonal (diagonal), render as orthogonal dogleg step
                if (Math.Abs(y1 - y2) >= 1e-4)
                {
                    var s1 = WorldToScreen(x1, y1);
                    var midS = WorldToScreen(x2, y1);
                    var s2 = WorldToScreen(x2, y2);
                    if (hasAura && glowPen != null)
                    {
                        context.DrawLine(glowPen, s1, midS);
                        context.DrawLine(glowPen, midS, s2);
                    }
                    context.DrawLine(corePen, s1, midS);
                    context.DrawLine(corePen, midS, s2);
                    continue;
                }

                // Horizontal segments: detect crossings with unconnected vertical segments
                double minX = Math.Min(x1, x2);
                double maxX = Math.Max(x1, x2);
                var crossings = new List<double>();

                foreach (var vs in verticalSegments)
                {
                    if (vs.Wire == wire) continue;

                    // Check strict interior intersection (not at endpoints)
                    if (vs.X > minX + 0.15 && vs.X < maxX - 0.15 && y1 > vs.MinY + 0.15 && y1 < vs.MaxY - 0.15)
                    {
                        var crossKey = (Math.Round(vs.X, 1), Math.Round(y1, 1));
                        bool isConnectedJoint = trueJunctionNodeIds.TryGetValue(crossKey, out int jnId) && jnId == wire.NodeId && vs.NodeId == wire.NodeId;

                        if (!isConnectedJoint && !crossings.Contains(vs.X))
                        {
                            crossings.Add(vs.X);
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

            // Mini Digital Waveform Glyph for 555 timer digital clock outputs
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

        // 5. Render high-contrast solid solder junction beads ONLY for verified >= 3 same-net connections
        foreach (var (key, jNodeId) in trueJunctionNodeIds)
        {
            var screenPt = WorldToScreen(key.X, key.Y);
            var juncProbedCh = ProbedChannels?.FirstOrDefault(c => c.TargetNodeId.HasValue && c.TargetNodeId.Value == jNodeId);
            bool isJuncProbed = juncProbedCh != null || (ProbedNodeCh1.HasValue && jNodeId == ProbedNodeCh1.Value) || (ProbedNodeCh2.HasValue && jNodeId == ProbedNodeCh2.Value);
            Color ringCol = juncProbedCh?.Color ?? (ProbedNodeCh1.HasValue && jNodeId == ProbedNodeCh1.Value ? Color.FromRgb(245, 158, 11) : Color.FromRgb(6, 182, 212));

            if (isJuncProbed)
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, ringCol.R, ringCol.G, ringCol.B)), null, screenPt, 7.5 * Zoom, 7.5 * Zoom);
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(240, 15, 23, 42)), new Pen(new SolidColorBrush(ringCol), 1.8 * Zoom), screenPt, 5.2 * Zoom, 5.2 * Zoom);
                context.DrawEllipse(new SolidColorBrush(ringCol), null, screenPt, 3.2 * Zoom, 3.2 * Zoom);
            }
            else
            {
                var (vGlow, vCore) = junctionColors.GetValueOrDefault(key, (Color.FromArgb(40, 148, 163, 184), Color.FromRgb(148, 163, 184)));
                var beadGlowBrush = new SolidColorBrush(Color.FromArgb(50, vCore.R, vCore.G, vCore.B));
                var beadCoreBrush = new SolidColorBrush(vCore);
                var beadDarkPen = new Pen(new SolidColorBrush(Color.FromArgb(245, 10, 15, 26)), 1.5 * Zoom);

                // High-contrast EDA solder bead
                context.DrawEllipse(beadGlowBrush, null, screenPt, 6.0 * Zoom, 6.0 * Zoom);
                context.DrawEllipse(beadCoreBrush, beadDarkPen, screenPt, 3.8 * Zoom, 3.8 * Zoom);

                // 3D specular highlight dot on top-left of bead
                var specPt = new Point(screenPt.X - 1.0 * Zoom, screenPt.Y - 1.0 * Zoom);
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null, specPt, 0.9 * Zoom, 0.9 * Zoom);
            }
        }

        // 6. Draw interactive wire drag & vertex edit handles for selected/hovered wire
        DrawWireEditHandles(context);

        _lastDirectionsAtPoint = trueDirectionsAtPoint;
        _lastJunctionNodeIds = trueJunctionNodeIds;
    }

    private void DrawWireEditHandles(DrawingContext context)
    {
        if (Circuit == null) return;

        var activeWire = _draggedWire ?? SelectedWire ?? _hoveredWire;
        if (activeWire == null || activeWire.PathPoints == null || activeWire.PathPoints.Count < 2) return;

        bool isWireActive = activeWire == SelectedWire || activeWire == _draggedWire || activeWire == _hoveredWire;
        if (!isWireActive) return;

        // Draw diamond grab handles at bend corners
        for (int i = 1; i < activeWire.PathPoints.Count - 1; i++)
        {
            var p = activeWire.PathPoints[i];
            var screenPt = WorldToScreen(p.X, p.Y);
            bool isHoveredVertex = _hoveredWireVertex?.Wire == activeWire && _hoveredWireVertex?.VertexIndex == i;

            double diamondR = (isHoveredVertex ? 5.5 : 3.8) * Zoom;
            var fillBrush = isHoveredVertex
                ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) // Emerald on hover
                : new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Cyan default

            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)), 1.2 * Zoom);

            var diamondGeom = new StreamGeometry();
            using (var ctx = diamondGeom.Open())
            {
                ctx.BeginFigure(new Point(screenPt.X, screenPt.Y - diamondR), isFilled: true);
                ctx.LineTo(new Point(screenPt.X + diamondR, screenPt.Y));
                ctx.LineTo(new Point(screenPt.X, screenPt.Y + diamondR));
                ctx.LineTo(new Point(screenPt.X - diamondR, screenPt.Y));
                ctx.EndFigure(isClosed: true);
            }
            context.DrawGeometry(fillBrush, borderPen, diamondGeom);
        }

        // Draw segment drag indicator badge when hovering or dragging a segment
        var activeSeg = _hoveredWireSegment;
        if (_isDraggingWire && _draggedWire != null && _draggedSegmentIndex >= 0)
        {
            activeSeg = (_draggedWire, _draggedSegmentIndex, _isDraggingHorizontalSegment);
        }

        if (activeSeg != null && activeSeg.Value.Wire == activeWire)
        {
            int sIdx = activeSeg.Value.SegmentIndex;
            if (sIdx >= 0 && sIdx < activeWire.PathPoints.Count - 1)
            {
                var p1 = WorldToScreen(activeWire.PathPoints[sIdx].X, activeWire.PathPoints[sIdx].Y);
                var p2 = WorldToScreen(activeWire.PathPoints[sIdx + 1].X, activeWire.PathPoints[sIdx + 1].Y);
                var mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);

                string arrowText = activeSeg.Value.IsHorizontal ? "↕" : "↔";
                var ft = new FormattedText(
                    arrowText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                    10.0 * Zoom,
                    new SolidColorBrush(Color.FromRgb(56, 189, 248))
                );

                double badgeW = 16.0 * Zoom;
                double badgeH = 14.0 * Zoom;
                var badgeRect = new Rect(mid.X - badgeW / 2.0, mid.Y - badgeH / 2.0, badgeW, badgeH);

                context.FillRectangle(new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)), badgeRect, (float)(3 * Zoom));
                context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.2 * Zoom), badgeRect, (float)(3 * Zoom));
                context.DrawText(ft, new Point(mid.X - ft.Width / 2.0, mid.Y - ft.Height / 2.0));
            }
        }
    }

    private static StreamGeometry CreateHopGeometry(Point start, Point end, double hopR)
    {
        var geom = new StreamGeometry();
        // SweepDirection: ensure arc always bows upwards (towards negative Y in screen space)
        var sweep = (start.X < end.X) ? SweepDirection.CounterClockwise : SweepDirection.Clockwise;
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.ArcTo(end, new Size(hopR, hopR), 0, false, sweep);
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
}
