using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Models;

/// <summary>
/// Professional EDA Orthogonal Wire Routing Engine.
/// Computes clean, minimal-bend Manhattan polyline paths that respect pin normal escape directions
/// and route around component bodies using A* pathfinding on the schematic grid.
/// </summary>
public static class SchematicWireRouter
{
    private readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    private class Node
    {
        public GridPoint Point;
        public (int Dx, int Dy) Direction;
        public double GScore;
        public double FScore;
        public Node? Parent;

        public Node(GridPoint pt, (int Dx, int Dy) dir, double g, double f, Node? parent)
        {
            Point = pt;
            Direction = dir;
            GScore = g;
            FScore = f;
            Parent = parent;
        }
    }

    public static List<(double X, double Y)> RouteWire(Circuit circuit, Device dev1, int pinIndex1, Device dev2, int pinIndex2)
    {
        var (x1, y1) = dev1.GetAbsolutePinPosition(pinIndex1);
        var (x2, y2) = dev2.GetAbsolutePinPosition(pinIndex2);
        var (dx1, dy1) = dev1.GetPinNormal(pinIndex1);
        var (dx2, dy2) = dev2.GetPinNormal(pinIndex2);

        return RouteWire(circuit, x1, y1, dx1, dy1, x2, y2, dx2, dy2, dev1, dev2);
    }

    public static List<(double X, double Y)> RouteWire(
        Circuit? circuit,
        double x1, double y1, int dx1, int dy1,
        double x2, double y2, int dx2, int dy2,
        Device? ignoreDev1 = null, Device? ignoreDev2 = null)
    {
        // 1. Direct straight connection check
        if (Math.Abs(x1 - x2) < 0.05 && Math.Abs(y1 - y2) < 0.05)
        {
            return new List<(double X, double Y)> { (x1, y1), (x2, y2) };
        }

        // Collinear and non-inward facing check
        if (Math.Abs(y1 - y2) < 0.05)
        {
            bool validDir = (x1 < x2 && dx1 >= 0 && dx2 <= 0) || (x1 > x2 && dx1 <= 0 && dx2 >= 0);
            if (validDir && !HasObstacleBetween(circuit, x1, y1, x2, y2, ignoreDev1, ignoreDev2))
            {
                return new List<(double X, double Y)> { (x1, y1), (x2, y2) };
            }
        }

        if (Math.Abs(x1 - x2) < 0.05)
        {
            bool validDir = (y1 < y2 && dy1 >= 0 && dy2 <= 0) || (y1 > y2 && dy1 <= 0 && dy2 >= 0);
            if (validDir && !HasObstacleBetween(circuit, x1, y1, x2, y2, ignoreDev1, ignoreDev2))
            {
                return new List<(double X, double Y)> { (x1, y1), (x2, y2) };
            }
        }

        // 2. High-performance A* search on 0.5-unit fine grid (scale factor = 2)
        int startX = (int)Math.Round(x1 * 2.0);
        int startY = (int)Math.Round(y1 * 2.0);
        int goalX = (int)Math.Round(x2 * 2.0);
        int goalY = (int)Math.Round(y2 * 2.0);

        var aStarPath = FindPathAStar(circuit, startX, startY, dx1, dy1, goalX, goalY, dx2, dy2, ignoreDev1, ignoreDev2, x1, y1, x2, y2);
        if (aStarPath != null && aStarPath.Count >= 2)
        {
            return aStarPath;
        }

        // 3. Fallback: Analytical clean 2-bend/3-bend escape route
        return FallbackOrthogonalRoute(x1, y1, dx1, dy1, x2, y2, dx2, dy2);
    }

    private static List<(double X, double Y)>? FindPathAStar(
        Circuit? circuit,
        int startX, int startY, int dx1, int dy1,
        int goalX, int goalY, int dx2, int dy2,
        Device? ignoreDev1, Device? ignoreDev2,
        double origX1, double origY1, double origX2, double origY2)
    {
        // Define bounding area for search in half-grid units
        int minX = Math.Min(startX, goalX) - 10;
        int maxX = Math.Max(startX, goalX) + 10;
        int minY = Math.Min(startY, goalY) - 10;
        int maxY = Math.Max(startY, goalY) + 10;

        if (circuit != null)
        {
            foreach (var d in circuit.Devices)
            {
                minX = Math.Min(minX, (int)Math.Floor(d.LocationX * 2.0) - 8);
                maxX = Math.Max(maxX, (int)Math.Ceiling(d.LocationX * 2.0) + 8);
                minY = Math.Min(minY, (int)Math.Floor(d.LocationY * 2.0) - 8);
                maxY = Math.Max(maxY, (int)Math.Ceiling(d.LocationY * 2.0) + 8);
            }
        }

        var startPt = new GridPoint(startX, startY);
        var goalPt = new GridPoint(goalX, goalY);

        if (startPt.Equals(goalPt))
        {
            return new List<(double X, double Y)> { (origX1, origY1), (origX2, origY2) };
        }

        // Precompute obstacles and pin corridor keep-out zones in half-grid units for ALL components
        var obstacles = new HashSet<GridPoint>();
        var pinCorridorPoints = new HashSet<GridPoint>();

        if (circuit != null)
        {
            foreach (var d in circuit.Devices)
            {
                var (bMinX, bMaxX, bMinY, bMaxY) = d.GetBoundingBox(padding: 0.05);
                int minGx = (int)Math.Round(bMinX * 2.0);
                int maxGx = (int)Math.Round(bMaxX * 2.0);
                int minGy = (int)Math.Round(bMinY * 2.0);
                int maxGy = (int)Math.Round(bMaxY * 2.0);

                if (minGx <= maxGx && minGy <= maxGy)
                {
                    for (int gx = minGx; gx <= maxGx; gx++)
                    {
                        for (int gy = minGy; gy <= maxGy; gy++)
                        {
                            var pBody = new GridPoint(gx, gy);
                            if (!pBody.Equals(startPt) && !pBody.Equals(goalPt))
                            {
                                obstacles.Add(pBody);
                            }
                        }
                    }
                }
                else
                {
                    int cx = (int)Math.Round(d.LocationX * 2.0);
                    int cy = (int)Math.Round(d.LocationY * 2.0);
                    var centerPt = new GridPoint(cx, cy);
                    if (!centerPt.Equals(startPt) && !centerPt.Equals(goalPt))
                    {
                        obstacles.Add(centerPt);
                    }
                }

                // Protect pins of other devices and reserve their front departure corridor
                for (int pi = 0; pi < d.Pins.Count; pi++)
                {
                    var (apx, apy) = d.GetAbsolutePinPosition(pi);
                    var (pNormX, pNormY) = d.GetPinNormal(pi);
                    int pinGx = (int)Math.Round(apx * 2.0);
                    int pinGy = (int)Math.Round(apy * 2.0);
                    var pPin = new GridPoint(pinGx, pinGy);

                    if (!pPin.Equals(startPt) && !pPin.Equals(goalPt))
                    {
                        obstacles.Add(pPin);

                        // Corridor point immediately in front of pin
                        if (pNormX != 0 || pNormY != 0)
                        {
                            var pCorridor = new GridPoint(pinGx + pNormX, pinGy + pNormY);
                            if (!pCorridor.Equals(startPt) && !pCorridor.Equals(goalPt))
                            {
                                pinCorridorPoints.Add(pCorridor);
                            }
                        }
                    }
                }
            }
        }

        // Wire track congestion avoidance: track points already occupied by existing wires
        var wireOccupiedPoints = new HashSet<GridPoint>();
        if (circuit != null)
        {
            foreach (var w in circuit.Wires)
            {
                if (w.PathPoints == null || w.PathPoints.Count < 2) continue;
                for (int i = 0; i < w.PathPoints.Count - 1; i++)
                {
                    int px1 = (int)Math.Round(w.PathPoints[i].X * 2.0);
                    int py1 = (int)Math.Round(w.PathPoints[i].Y * 2.0);
                    int px2 = (int)Math.Round(w.PathPoints[i + 1].X * 2.0);
                    int py2 = (int)Math.Round(w.PathPoints[i + 1].Y * 2.0);

                    if (px1 == px2)
                    {
                        int minPy = Math.Min(py1, py2);
                        int maxPy = Math.Max(py1, py2);
                        for (int y = minPy; y <= maxPy; y++)
                            wireOccupiedPoints.Add(new GridPoint(px1, y));
                    }
                    else if (py1 == py2)
                    {
                        int minPx = Math.Min(px1, px2);
                        int maxPx = Math.Max(px1, px2);
                        for (int x = minPx; x <= maxPx; x++)
                            wireOccupiedPoints.Add(new GridPoint(x, py1));
                    }
                }
            }
        }

        var openSet = new PriorityQueue<Node, double>();
        var closedSet = new Dictionary<(GridPoint, (int, int)), double>();

        var startNode = new Node(startPt, (dx1, dy1), 0, Heuristic(startX, startY, goalX, goalY), null);

        (int Dx, int Dy)[] candidateStarts;
        if (dx1 != 0 || dy1 != 0)
        {
            int escX = startX + dx1;
            int escY = startY + dy1;
            var escPt = new GridPoint(escX, escY);
            if (!obstacles.Contains(escPt))
            {
                candidateStarts = new[] { (dx1, dy1) };
            }
            else
            {
                var list = new List<(int Dx, int Dy)>();
                if (dx1 != 0)
                {
                    list.Add((0, -1));
                    list.Add((0, 1));
                }
                else if (dy1 != 0)
                {
                    list.Add((-1, 0));
                    list.Add((1, 0));
                }
                candidateStarts = list.ToArray();
            }
        }
        else
        {
            candidateStarts = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
        }

        foreach (var (cdx, cdy) in candidateStarts)
        {
            int escX = startX + cdx;
            int escY = startY + cdy;
            var escPt = new GridPoint(escX, escY);
            if (!obstacles.Contains(escPt))
            {
                double turnCost = (cdx == dx1 && cdy == dy1) ? 0.0 : 0.5;
                var escNode = new Node(escPt, (cdx, cdy), 1.0 + turnCost, 1.0 + turnCost + Heuristic(escX, escY, goalX, goalY), startNode);
                openSet.Enqueue(escNode, escNode.FScore);
            }
        }

        (int Dx, int Dy)[] directions = { (0, -1), (1, 0), (0, 1), (-1, 0) };

        int iterations = 0;
        int maxIterations = 3500;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            var current = openSet.Dequeue();

            if (current.Point.Equals(goalPt))
            {
                return ReconstructPath(current, origX1, origY1, origX2, origY2);
            }

            var stateKey = (current.Point, current.Direction);
            if (closedSet.TryGetValue(stateKey, out double existingG) && existingG <= current.GScore)
            {
                continue;
            }
            closedSet[stateKey] = current.GScore;

            foreach (var (nextDx, nextDy) in directions)
            {
                // Disallow reversing directly into previous step
                if (nextDx == -current.Direction.Dx && nextDy == -current.Direction.Dy)
                    continue;

                int nx = current.Point.X + nextDx;
                int ny = current.Point.Y + nextDy;
                var nextPt = new GridPoint(nx, ny);

                if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                    continue;

                // If not goal, avoid obstacles
                if (!nextPt.Equals(goalPt) && obstacles.Contains(nextPt))
                    continue;

                // Cost: 1.0 per unit distance + bend penalty
                bool isTurn = (nextDx != current.Direction.Dx || nextDy != current.Direction.Dy);
                double stepCost = 1.0 + (isTurn ? 2.5 : 0.0);

                // When approaching goal pin, enforce front-entry approach
                if (nextPt.Equals(goalPt))
                {
                    if (dx2 != 0 || dy2 != 0)
                    {
                        // Disallow entering through the device body
                        if ((dx2 != 0 && nextDx == dx2) || (dy2 != 0 && nextDy == dy2))
                        {
                            continue;
                        }

                        // Strongly prefer entering straight from pin normal front face
                        if (nextDx != -dx2 || nextDy != -dy2)
                        {
                            stepCost += 8.0;
                        }
                    }
                }

                // Congestion penalty if occupying track of an existing wire
                if (wireOccupiedPoints.Contains(nextPt) && !nextPt.Equals(startPt) && !nextPt.Equals(goalPt))
                {
                    stepCost += 1.2;
                }

                // Corridor penalty if crossing in front of another component's pin
                if (pinCorridorPoints.Contains(nextPt) && !nextPt.Equals(startPt) && !nextPt.Equals(goalPt))
                {
                    stepCost += 4.5;
                }

                double tentativeG = current.GScore + stepCost;
                double h = Heuristic(nx, ny, goalX, goalY);
                double f = tentativeG + h;

                var neighbor = new Node(nextPt, (nextDx, nextDy), tentativeG, f, current);
                openSet.Enqueue(neighbor, f);
            }
        }

        return null;
    }

    private static double Heuristic(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    private static List<(double X, double Y)> ReconstructPath(Node endNode, double origX1, double origY1, double origX2, double origY2)
    {
        var rawPoints = new List<GridPoint>();
        var curr = endNode;
        while (curr != null)
        {
            rawPoints.Add(curr.Point);
            curr = curr.Parent;
        }
        rawPoints.Reverse();

        var simplified = new List<(double X, double Y)>();
        if (rawPoints.Count == 0) return simplified;

        simplified.Add((origX1, origY1));

        for (int i = 1; i < rawPoints.Count - 1; i++)
        {
            var prev = rawPoints[i - 1];
            var p = rawPoints[i];
            var next = rawPoints[i + 1];

            // If direction changes, keep corner vertex
            bool isCollinear = (prev.X == p.X && p.X == next.X) || (prev.Y == p.Y && p.Y == next.Y);
            if (!isCollinear)
            {
                simplified.Add((p.X / 2.0, p.Y / 2.0));
            }
        }

        simplified.Add((origX2, origY2));

        // Clean up redundant collinear points near boundaries
        if (simplified.Count >= 3)
        {
            var cleaned = new List<(double X, double Y)> { simplified[0] };
            for (int i = 1; i < simplified.Count - 1; i++)
            {
                var prev = cleaned[^1];
                var currPt = simplified[i];
                var next = simplified[i + 1];

                bool isCollinear = (Math.Abs(prev.X - currPt.X) < 1e-4 && Math.Abs(currPt.X - next.X) < 1e-4) ||
                                   (Math.Abs(prev.Y - currPt.Y) < 1e-4 && Math.Abs(currPt.Y - next.Y) < 1e-4);
                if (!isCollinear)
                {
                    cleaned.Add(currPt);
                }
            }
            cleaned.Add(simplified[^1]);
            return cleaned;
        }

        return simplified;
    }

    public static bool HasObstacleBetween(Circuit? circuit, double x1, double y1, double x2, double y2, Device? ignoreDev1 = null, Device? ignoreDev2 = null)
    {
        if (circuit == null) return false;
        return FindCollidingDeviceForSegment(circuit, x1, y1, x2, y2, ignoreDev1, ignoreDev2) != null;
    }

    /// <summary>
    /// Checks if a wire segment (x1, y1) -> (x2, y2) intersects or penetrates any component body in the circuit.
    /// Even for connected devices, segments departing inward into the device body are flagged as collisions.
    /// </summary>
    public static Device? FindCollidingDeviceForSegment(Circuit circuit, double x1, double y1, double x2, double y2, Device? ignoreDev1 = null, Device? ignoreDev2 = null)
    {
        foreach (var dev in circuit.Devices)
        {
            if (SegmentPenetratesDevice(x1, y1, x2, y2, dev, ignoreDev1, ignoreDev2))
            {
                return dev;
            }
        }

        return null;
    }

    /// <summary>
    /// Tests whether a segment penetrates the interior body of a device,
    /// allowing valid outward departures from pin locations along escape normals.
    /// </summary>
    public static bool SegmentPenetratesDevice(double x1, double y1, double x2, double y2, Device dev, Device? ignoreDev1 = null, Device? ignoreDev2 = null)
    {
        double minX = Math.Min(x1, x2);
        double maxX = Math.Max(x1, x2);
        double minY = Math.Min(y1, y2);
        double maxY = Math.Max(y1, y2);

        var (devLeft, devRight, devTop, devBottom) = dev.GetBoundingBox(padding: 0.05);

        // Check if segment touches any pin on this device
        int matchedPin1 = -1;
        int matchedPin2 = -1;
        for (int p = 0; p < dev.Pins.Count; p++)
        {
            var (px, py) = dev.GetAbsolutePinPosition(p);
            if (Math.Abs(px - x1) < 1e-3 && Math.Abs(py - y1) < 1e-3) matchedPin1 = p;
            if (Math.Abs(px - x2) < 1e-3 && Math.Abs(py - y2) < 1e-3) matchedPin2 = p;
        }

        bool isConnectedDevice = ReferenceEquals(dev, ignoreDev1) || ReferenceEquals(dev, ignoreDev2);

        // If neither endpoint is a pin on this device:
        if (matchedPin1 < 0 && matchedPin2 < 0)
        {
            if (isConnectedDevice)
            {
                // Intermediate segment of a wire connected to dev cannot cross through dev
                if (Math.Abs(minX - maxX) < 0.05)
                    return minX >= devLeft && minX <= devRight && maxY > devTop && minY < devBottom;
                if (Math.Abs(minY - maxY) < 0.05)
                    return minY >= devTop && minY <= devBottom && maxX > devLeft && minX < devRight;
                return dev.LocationX >= minX - 0.2 && dev.LocationX <= maxX + 0.2 &&
                       dev.LocationY >= minY - 0.2 && dev.LocationY <= maxY + 0.2;
            }

            if (Math.Abs(minX - maxX) < 0.05) // vertical segment at minX
            {
                if (minX >= devLeft && minX <= devRight && maxY > devTop && minY < devBottom)
                {
                    return true;
                }
            }
            else if (Math.Abs(minY - maxY) < 0.05) // horizontal segment at minY
            {
                if (minY >= devTop && minY <= devBottom && maxX > devLeft && minX < devRight)
                {
                    return true;
                }
            }
            else
            {
                if (dev.LocationX >= minX - 0.2 && dev.LocationX <= maxX + 0.2 &&
                    dev.LocationY >= minY - 0.2 && dev.LocationY <= maxY + 0.2)
                {
                    return true;
                }
            }
            return false;
        }

        // An endpoint matches a pin on this device.
        // It is only allowed if it departs/approaches along the pin's outward normal,
        // and does NOT head inward into the component body!
        if (matchedPin1 >= 0)
        {
            var (normX, normY) = dev.GetPinNormal(matchedPin1);
            double dirX = x2 - x1;
            double dirY = y2 - y1;

            if (normX != 0 && Math.Sign(dirX) == -normX) return true; // heading inward horizontally
            if (normY != 0 && Math.Sign(dirY) == -normY) return true; // heading inward vertically
        }

        if (matchedPin2 >= 0)
        {
            var (normX, normY) = dev.GetPinNormal(matchedPin2);
            double dirX = x1 - x2;
            double dirY = y1 - y2;

            if (normX != 0 && Math.Sign(dirX) == -normX) return true; // approaching inward horizontally
            if (normY != 0 && Math.Sign(dirY) == -normY) return true; // approaching inward vertically
        }

        return false;
    }

    private static List<(double X, double Y)> FallbackOrthogonalRoute(
        double x1, double y1, int dx1, int dy1,
        double x2, double y2, int dx2, int dy2)
    {
        var pts = new List<(double X, double Y)>();
        pts.Add((x1, y1));

        // If dx1 != 0, initial step is horizontal; if dy1 != 0, vertical
        if (dx1 != 0)
        {
            if (Math.Abs(y1 - y2) < 0.05) // collinear horizontal pins facing each other
            {
                double detourY = y1 + 2.0;
                pts.Add((x1 + dx1, y1));
                pts.Add((x1 + dx1, detourY));
                pts.Add((x2 + dx2, detourY));
                pts.Add((x2 + dx2, y2));
            }
            else
            {
                double midX = (dx1 > 0) ? Math.Max(x1 + 1.0, (x1 + x2) / 2.0) : Math.Min(x1 - 1.0, (x1 + x2) / 2.0);
                pts.Add((midX, y1));
                pts.Add((midX, y2));
            }
        }
        else if (dy1 != 0)
        {
            if (Math.Abs(x1 - x2) < 0.05) // collinear vertical pins facing each other
            {
                double detourX = x1 + 2.0;
                pts.Add((x1, y1 + dy1));
                pts.Add((detourX, y1 + dy1));
                pts.Add((detourX, y2 + dy2));
                pts.Add((x2, y2 + dy2));
            }
            else
            {
                double midY = (dy1 > 0) ? Math.Max(y1 + 1.0, (y1 + y2) / 2.0) : Math.Min(y1 - 1.0, (y1 + y2) / 2.0);
                pts.Add((x1, midY));
                pts.Add((x2, midY));
            }
        }
        else
        {
            // Default 1-corner L route
            pts.Add((x2, y1));
        }

        pts.Add((x2, y2));
        return pts;
    }
}
