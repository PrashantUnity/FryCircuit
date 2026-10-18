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
                minX = Math.Min(minX, (int)Math.Floor(d.LocationX * 2.0) - 6);
                maxX = Math.Max(maxX, (int)Math.Ceiling(d.LocationX * 2.0) + 6);
                minY = Math.Min(minY, (int)Math.Floor(d.LocationY * 2.0) - 6);
                maxY = Math.Max(maxY, (int)Math.Ceiling(d.LocationY * 2.0) + 6);
            }
        }

        var startPt = new GridPoint(startX, startY);
        var goalPt = new GridPoint(goalX, goalY);

        if (startPt.Equals(goalPt))
        {
            return new List<(double X, double Y)> { (origX1, origY1), (origX2, origY2) };
        }

        // Precompute obstacles in half-grid units
        var obstacles = new HashSet<GridPoint>();
        if (circuit != null)
        {
            foreach (var d in circuit.Devices)
            {
                if (ReferenceEquals(d, ignoreDev1) || ReferenceEquals(d, ignoreDev2))
                    continue;

                int cx = (int)Math.Round(d.LocationX * 2.0);
                int cy = (int)Math.Round(d.LocationY * 2.0);
                var centerPt = new GridPoint(cx, cy);

                // Mark component body core as impassable obstacle unless it is start/goal point
                if (!centerPt.Equals(startPt) && !centerPt.Equals(goalPt))
                {
                    obstacles.Add(centerPt);
                }

                // Add surrounding body for components with wider footprints
                if (d.Type == "tline" || d.Type == "timer555" || d.Type == "opamp")
                {
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        for (int oy = -2; oy <= 2; oy++)
                        {
                            var pBody = new GridPoint(cx + ox, cy + oy);
                            if (!pBody.Equals(startPt) && !pBody.Equals(goalPt))
                            {
                                obstacles.Add(pBody);
                            }
                        }
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
            var list = new List<(int Dx, int Dy)> { (dx1, dy1) };
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
        int maxIterations = 3000;

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

                // When approaching goal pin, disallow entering through the device body
                if (nextPt.Equals(goalPt))
                {
                    if (dx2 != 0 || dy2 != 0)
                    {
                        if ((dx2 != 0 && nextDx == dx2) || (dy2 != 0 && nextDy == dy2))
                        {
                            // Disallow approaching pin backwards through device body
                            continue;
                        }
                    }
                }

                // Cost: 1.0 per unit distance + bend penalty
                bool isTurn = (nextDx != current.Direction.Dx || nextDy != current.Direction.Dy);
                double stepCost = 1.0 + (isTurn ? 2.5 : 0.0);

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

        double minX = Math.Min(x1, x2);
        double maxX = Math.Max(x1, x2);
        double minY = Math.Min(y1, y2);
        double maxY = Math.Max(y1, y2);

        foreach (var dev in circuit.Devices)
        {
            if (ReferenceEquals(dev, ignoreDev1) || ReferenceEquals(dev, ignoreDev2))
                continue;

            int cx = (int)Math.Round(dev.LocationX);
            int cy = (int)Math.Round(dev.LocationY);

            if (Math.Abs(minX - maxX) < 0.05) // vertical segment
            {
                if (cx == (int)Math.Round(minX) && cy > minY + 0.05 && cy < maxY - 0.05)
                {
                    return true;
                }
            }
            else if (Math.Abs(minY - maxY) < 0.05) // horizontal segment
            {
                if (cy == (int)Math.Round(minY) && cx > minX + 0.05 && cx < maxX - 0.05)
                {
                    return true;
                }
            }
            else
            {
                if (dev.LocationX >= minX - 0.4 && dev.LocationX <= maxX + 0.4 &&
                    dev.LocationY >= minY - 0.4 && dev.LocationY <= maxY + 0.4)
                {
                    return true;
                }
            }
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
