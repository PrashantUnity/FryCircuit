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
                var juncProbedCh = ProbedChannels?.FirstOrDefault(c => c.TargetNodeId.HasValue && c.TargetNodeId.Value == jNodeId);
                bool isJuncProbed = juncProbedCh != null || (ProbedNodeCh1.HasValue && jNodeId == ProbedNodeCh1.Value) || (ProbedNodeCh2.HasValue && jNodeId == ProbedNodeCh2.Value);
                Color ringCol = juncProbedCh?.Color ?? (ProbedNodeCh1.HasValue && jNodeId == ProbedNodeCh1.Value ? Color.FromRgb(245, 158, 11) : Color.FromRgb(6, 182, 212));

                if (isJuncProbed)
                {
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
}
