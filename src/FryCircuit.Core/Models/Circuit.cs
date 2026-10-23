using FryCircuit.Core.Devices;

namespace FryCircuit.Core.Models;

public class Circuit
{
    public string Name { get; set; } = "Untitled";
    public int Version { get; set; } = 10;
    public CircuitFrame Frame { get; set; } = new(0, 0, 20, 20);
    public CircuitOptions Options { get; set; } = new();

    public Dictionary<int, CircuitNode> Nodes { get; set; } = new();
    public List<Device> Devices { get; set; } = new();
    public List<CircuitWire> Wires { get; set; } = new();

    public Circuit() { }

    public Circuit(string name)
    {
        Name = name;
    }

    public CircuitNode GetOrCreateNode(int id, bool isGround = false)
    {
        if (!Nodes.TryGetValue(id, out var node))
        {
            node = new CircuitNode(id, isGround);
            Nodes[id] = node;
        }
        else if (isGround)
        {
            node.IsGround = true;
        }
        return node;
    }

    public void AddDevice(Device device)
    {
        if (device.Id <= 0 && Devices.Count > 0)
        {
            device.Id = Devices.Max(d => d.Id) + 1;
        }
        else if (device.Id <= 0)
        {
            device.Id = 1;
        }
        Devices.Add(device);
    }

    public bool RemoveDevice(Device device)
    {
        // Remove all wires connected to this device
        Wires.RemoveAll(w => w.FromDeviceId == device.Id || w.ToDeviceId == device.Id);
        return Devices.Remove(device);
    }

    public CircuitNode? FindGroundNode()
    {
        return Nodes.Values.FirstOrDefault(n => n.IsGround);
    }

    public void ResetSimulationState()
    {
        foreach (var node in Nodes.Values)
        {
            node.Voltage = 0.0;
            node.PrevVoltage = 0.0;
        }

        foreach (var dev in Devices)
        {
            dev.Current = 0.0;
            dev.VoltageDrop = 0.0;
            dev.ResetBlown();
        }
    }

    /// <summary>
    /// Searches for a device pin within a given tolerance of world coordinates (x, y).
    /// </summary>
    public (Device Device, Pin Pin, int PinIndex)? FindPinAt(double x, double y, double tolerance = 0.5)
    {
        foreach (var dev in Devices)
        {
            for (int i = 0; i < dev.Pins.Count; i++)
            {
                var (px, py) = dev.GetAbsolutePinPosition(i);
                double dx = px - x;
                double dy = py - y;
                if ((dx * dx + dy * dy) <= (tolerance * tolerance))
                {
                    return (dev, dev.Pins[i], i);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Connects two pins with a wire, unifying their electrical node.
    /// </summary>
    public CircuitWire ConnectPins(Device dev1, int pinIndex1, Device dev2, int pinIndex2)
    {
        if (pinIndex1 < 0 || pinIndex1 >= dev1.Pins.Count ||
            pinIndex2 < 0 || pinIndex2 >= dev2.Pins.Count)
        {
            throw new ArgumentOutOfRangeException("Invalid pin index");
        }

        var pin1 = dev1.Pins[pinIndex1];
        var pin2 = dev2.Pins[pinIndex2];

        int targetNodeId = pin1.NodeId;
        int oldNodeId = pin2.NodeId;

        bool pin1HasNode = pin1.NodeId >= 0 && Nodes.ContainsKey(pin1.NodeId);
        bool pin2HasNode = pin2.NodeId >= 0 && Nodes.ContainsKey(pin2.NodeId);

        if (!pin1HasNode && !pin2HasNode)
        {
            int nextNode = (Nodes.Count > 0 ? Nodes.Keys.Max() : 0) + 1;
            targetNodeId = nextNode;
            oldNodeId = nextNode;
            GetOrCreateNode(targetNodeId);
            pin1.NodeId = targetNodeId;
            pin2.NodeId = targetNodeId;
        }
        else if (!pin1HasNode)
        {
            targetNodeId = oldNodeId;
            pin1.NodeId = targetNodeId;
        }
        else if (!pin2HasNode)
        {
            pin2.NodeId = targetNodeId;
        }
        else if (targetNodeId != oldNodeId)
        {
            targetNodeId = MergeNodes(targetNodeId, oldNodeId);
        }

        var existingWire = Wires.FirstOrDefault(w =>
            (w.FromDeviceId == dev1.Id && w.FromPinIndex == pinIndex1 && w.ToDeviceId == dev2.Id && w.ToPinIndex == pinIndex2) ||
            (w.FromDeviceId == dev2.Id && w.FromPinIndex == pinIndex2 && w.ToDeviceId == dev1.Id && w.ToPinIndex == pinIndex1));
        if (existingWire != null)
        {
            existingWire.NodeId = targetNodeId;
            return existingWire;
        }

        int nextWireId = (Wires.Count > 0 ? Wires.Max(w => w.Id) : 0) + 1;
        var wire = new CircuitWire(nextWireId, targetNodeId, dev1.Id, pinIndex1, dev2.Id, pinIndex2);
        Wires.Add(wire);
        return wire;
    }

    /// <summary>
    /// Merges two electrical nodes, preserving ground reference if either was ground,
    /// and updating all pins and wires referencing oldNode to canonicalNode.
    /// </summary>
    public int MergeNodes(int node1, int node2)
    {
        if (node1 == node2) return node1;

        bool is1Ground = Nodes.TryGetValue(node1, out var n1) && n1.IsGround;
        bool is2Ground = Nodes.TryGetValue(node2, out var n2) && n2.IsGround;

        int unifiedNode;
        if (is1Ground && !is2Ground)
        {
            unifiedNode = node1;
        }
        else if (is2Ground && !is1Ground)
        {
            unifiedNode = node2;
        }
        else if (is1Ground && is2Ground)
        {
            unifiedNode = (node1 == 0) ? node1 : ((node2 == 0) ? node2 : Math.Min(node1, node2));
        }
        else
        {
            unifiedNode = Math.Min(node1, node2);
        }

        int replacedNode = (unifiedNode == node1) ? node2 : node1;

        foreach (var d in Devices)
        {
            foreach (var p in d.Pins)
            {
                if (p.NodeId == replacedNode)
                {
                    p.NodeId = unifiedNode;
                }
            }
        }

        foreach (var w in Wires)
        {
            if (w.NodeId == replacedNode)
            {
                w.NodeId = unifiedNode;
            }
        }

        if (replacedNode != unifiedNode)
        {
            Nodes.Remove(replacedNode);
        }

        GetOrCreateNode(unifiedNode, isGround: is1Ground || is2Ground);
        return unifiedNode;
    }

    /// <summary>
    /// Connects a device pin to an existing wire net, forming a T-junction.
    /// </summary>
    public CircuitWire? ConnectPinToWire(Device dev, int pinIndex, CircuitWire targetWire, (double X, double Y)? tapPoint = null)
    {
        var targetDev = Devices.FirstOrDefault(d => d.Id == targetWire.FromDeviceId) ??
                        Devices.FirstOrDefault(d => d.Id == targetWire.ToDeviceId);
        if (targetDev == null) return null;

        int targetPinIdx = (targetDev.Id == targetWire.FromDeviceId) ? targetWire.FromPinIndex : targetWire.ToPinIndex;
        var wire = ConnectPins(dev, pinIndex, targetDev, targetPinIdx);

        if (tapPoint.HasValue)
        {
            double tx = Math.Round(tapPoint.Value.X * 2.0) / 2.0;
            double ty = Math.Round(tapPoint.Value.Y * 2.0) / 2.0;

            // Insert tap vertex into target wire if not present
            InsertVertexIfMissing(targetWire, tx, ty);

            // Route new wire from dev pin directly to the tap point
            var (px, py) = dev.GetAbsolutePinPosition(pinIndex);
            var (dx, dy) = dev.GetPinNormal(pinIndex);
            var tapRoute = SchematicWireRouter.RouteWire(this, px, py, dx, dy, tx, ty, 0, 0, dev, null);

            wire.PathPoints = tapRoute;
            wire.IsCustomPath = true;
            targetWire.IsCustomPath = true;
        }
        else
        {
            RouteAllWires();
        }

        return wire;
    }

    private static void InsertVertexIfMissing(CircuitWire wire, double x, double y)
    {
        if (wire.PathPoints == null || wire.PathPoints.Count < 2) return;

        // Check if point already exists
        if (wire.PathPoints.Any(p => Math.Abs(p.X - x) < 1e-3 && Math.Abs(p.Y - y) < 1e-3))
            return;

        // Find which segment contains (x, y)
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            double x1 = wire.PathPoints[i].X;
            double y1 = wire.PathPoints[i].Y;
            double x2 = wire.PathPoints[i + 1].X;
            double y2 = wire.PathPoints[i + 1].Y;

            bool onH = Math.Abs(y1 - y2) < 1e-3 && Math.Abs(y - y1) < 1e-3 && x >= Math.Min(x1, x2) - 1e-3 && x <= Math.Max(x1, x2) + 1e-3;
            bool onV = Math.Abs(x1 - x2) < 1e-3 && Math.Abs(x - x1) < 1e-3 && y >= Math.Min(y1, y2) - 1e-3 && y <= Math.Max(y1, y2) + 1e-3;

            if (onH || onV)
            {
                wire.PathPoints.Insert(i + 1, (x, y));
                break;
            }
        }
    }

    /// <summary>
    /// Disconnects a pin from its node, assigning it a new isolated node.
    /// </summary>
    public void DisconnectPin(Device dev, int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= dev.Pins.Count) return;

        var pin = dev.Pins[pinIndex];
        int oldNodeId = pin.NodeId;

        // Remove wires attached to this pin
        Wires.RemoveAll(w => w.ConnectsToPin(dev.Id, pinIndex));

        // Allocate a new unique node ID
        int nextNode = (Nodes.Count > 0 ? Nodes.Keys.Max() : 0) + 1;
        pin.NodeId = nextNode;
        GetOrCreateNode(nextNode);

        if (oldNodeId >= 0)
        {
            PartitionNodeComponents(oldNodeId);
        }
    }

    /// <summary>
    /// Removes a wire connection and partitions any disconnected pins into independent nodes.
    /// </summary>
    public bool RemoveWire(CircuitWire wire)
    {
        int nodeId = wire.NodeId;
        bool removed = Wires.Remove(wire);
        if (removed && nodeId >= 0)
        {
            PartitionNodeComponents(nodeId);
        }
        return removed;
    }

    /// <summary>
    /// Checks the connected components of pins and wires sharing nodeId after a wire removal.
    /// If the node has split into multiple disconnected components, isolates each into a new node.
    /// </summary>
    public List<int> PartitionNodeComponents(int nodeId)
    {
        var newNodesCreated = new List<int>();
        var wiresOnNode = Wires.Where(w => w.NodeId == nodeId).ToList();
        var pinsOnNode = new List<(Device Dev, int PinIndex)>();

        foreach (var dev in Devices)
        {
            for (int i = 0; i < dev.Pins.Count; i++)
            {
                if (dev.Pins[i].NodeId == nodeId)
                {
                    pinsOnNode.Add((dev, i));
                }
            }
        }

        if (pinsOnNode.Count <= 1) return newNodesCreated;

        // Build adjacency graph for pins on this node using remaining wires
        var adj = new Dictionary<(int DevId, int PinIndex), List<(int DevId, int PinIndex)>>();
        foreach (var p in pinsOnNode)
        {
            adj[(p.Dev.Id, p.PinIndex)] = new List<(int, int)>();
        }

        foreach (var w in wiresOnNode)
        {
            var p1 = (w.FromDeviceId, w.FromPinIndex);
            var p2 = (w.ToDeviceId, w.ToPinIndex);
            if (adj.ContainsKey(p1) && adj.ContainsKey(p2))
            {
                adj[p1].Add(p2);
                adj[p2].Add(p1);
            }
        }

        // Find connected components using BFS
        var visited = new HashSet<(int DevId, int PinIndex)>();
        var components = new List<List<(Device Dev, int PinIndex)>>();

        foreach (var p in pinsOnNode)
        {
            var key = (p.Dev.Id, p.PinIndex);
            if (visited.Contains(key)) continue;

            var comp = new List<(Device, int)>();
            var queue = new Queue<(int DevId, int PinIndex)>();
            queue.Enqueue(key);
            visited.Add(key);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                var dev = Devices.FirstOrDefault(d => d.Id == curr.DevId);
                if (dev != null && curr.PinIndex < dev.Pins.Count)
                {
                    comp.Add((dev, curr.PinIndex));
                }

                if (adj.TryGetValue(curr, out var nbrs))
                {
                    foreach (var next in nbrs)
                    {
                        if (visited.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }
            }
            components.Add(comp);
        }

        // If only 1 component, the node is still fully connected
        if (components.Count <= 1) return newNodesCreated;

        // Choose which component keeps the original nodeId:
        // Prioritize the component containing GroundDevice, NetLabel, or containing Node 0
        int keepIndex = 0;
        for (int i = 0; i < components.Count; i++)
        {
            if (components[i].Any(p => p.Dev.Type == "ground" || p.Dev is GroundDevice || p.Dev is NetLabelDevice))
            {
                keepIndex = i;
                break;
            }
        }

        int maxNodeId = Nodes.Count > 0 ? Nodes.Keys.Max() : 0;

        for (int i = 0; i < components.Count; i++)
        {
            if (i == keepIndex) continue;

            int newNodeId = ++maxNodeId;
            GetOrCreateNode(newNodeId);
            newNodesCreated.Add(newNodeId);

            var compPins = components[i];
            var compDevPinKeys = compPins.Select(p => (p.Dev.Id, p.PinIndex)).ToHashSet();

            foreach (var (dev, pinIdx) in compPins)
            {
                dev.Pins[pinIdx].NodeId = newNodeId;
            }

            // Update remaining wires that connect pins within this component
            foreach (var w in wiresOnNode)
            {
                if (compDevPinKeys.Contains((w.FromDeviceId, w.FromPinIndex)) &&
                    compDevPinKeys.Contains((w.ToDeviceId, w.ToPinIndex)))
                {
                    w.NodeId = newNodeId;
                }
            }
        }

        return newNodesCreated;
    }

    /// <summary>
    /// Ensures presets or circuits with shared NodeIds have explicit CircuitWire entries.
    /// </summary>
    public void EnsureWiresSynthesized()
    {
        if (Wires.Count > 0) return;

        // Group pins by NodeId
        var nodePins = new Dictionary<int, List<(Device Dev, int PinIndex)>>();
        foreach (var dev in Devices)
        {
            for (int i = 0; i < dev.Pins.Count; i++)
            {
                int nId = dev.Pins[i].NodeId;
                if (!nodePins.TryGetValue(nId, out var list))
                {
                    list = new List<(Device, int)>();
                    nodePins[nId] = list;
                }
                list.Add((dev, i));
            }
        }

        int wireIdCounter = 1;
        foreach (var (nId, pinList) in nodePins)
        {
            if (pinList.Count < 2) continue;

            var edges = BuildMinimumSpanningTree(this, pinList);
            foreach (var (p1, p2) in edges)
            {
                Wires.Add(new CircuitWire(wireIdCounter++, nId, p1.Dev.Id, p1.PinIndex, p2.Dev.Id, p2.PinIndex));
            }
        }

        RouteAllWires();
    }

    /// <summary>
    /// Dynamically resynthesizes the Minimum Spanning Tree (MST) wiring topology for the specified electrical node.
    /// This dynamically reconnects wires to the nearest/collinear pins when components are dragged.
    /// </summary>
    public void ResynthesizeNode(int nodeId)
    {
        var pinList = new List<(Device Dev, int PinIndex)>();
        foreach (var dev in Devices)
        {
            for (int i = 0; i < dev.Pins.Count; i++)
            {
                if (dev.Pins[i].NodeId == nodeId)
                {
                    pinList.Add((dev, i));
                }
            }
        }

        // If any wires are marked as custom on this node, preserve them
        if (Wires.Any(w => w.NodeId == nodeId && w.IsCustomPath))
        {
            RouteAllWires();
            return;
        }

        // Remove old wires for this node
        Wires.RemoveAll(w => w.NodeId == nodeId);

        if (pinList.Count < 2) return;

        int nextWireId = (Wires.Count > 0 ? Wires.Max(w => w.Id) : 0) + 1;
        var edges = BuildMinimumSpanningTree(this, pinList);
        foreach (var (p1, p2) in edges)
        {
            Wires.Add(new CircuitWire(nextWireId++, nodeId, p1.Dev.Id, p1.PinIndex, p2.Dev.Id, p2.PinIndex));
        }

        RouteAllWires();
    }

    private static List<((Device Dev, int PinIndex) P1, (Device Dev, int PinIndex) P2)> BuildMinimumSpanningTree(
        Circuit? circuit,
        List<(Device Dev, int PinIndex)> pins)
    {
        var result = new List<((Device, int), (Device, int))>();
        if (pins.Count < 2) return result;

        // If there are multiple Ground pins in this node, cluster pins by their closest ground device
        // so we don't synthesize long, spurious cross-schematic wires connecting two ground symbols.
        var groundPins = pins.Where(p => p.Dev.Type == "ground").ToList();
        if (groundPins.Count > 1)
        {
            var clusters = new Dictionary<Device, List<(Device Dev, int PinIndex)>>();
            foreach (var gp in groundPins)
            {
                clusters[gp.Dev] = new List<(Device Dev, int PinIndex)> { gp };
            }

            var nonGroundPins = pins.Where(p => p.Dev.Type != "ground").ToList();
            foreach (var ngp in nonGroundPins)
            {
                var pos = ngp.Dev.GetAbsolutePinPosition(ngp.PinIndex);
                Device? closest = null;
                double minDist = double.MaxValue;
                foreach (var gp in groundPins)
                {
                    var gpos = gp.Dev.GetAbsolutePinPosition(gp.PinIndex);
                    double dist = Math.Abs(pos.X - gpos.X) + Math.Abs(pos.Y - gpos.Y);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = gp.Dev;
                    }
                }

                if (closest != null)
                {
                    clusters[closest].Add(ngp);
                }
            }

            foreach (var cluster in clusters.Values)
            {
                if (cluster.Count >= 2)
                {
                    result.AddRange(BuildMinimumSpanningTree(circuit, cluster));
                }
            }
            return result;
        }

        int[] parent = new int[pins.Count];
        for (int i = 0; i < pins.Count; i++) parent[i] = i;

        int Find(int i)
        {
            if (parent[i] == i) return i;
            return parent[i] = Find(parent[i]);
        }

        bool Union(int i, int j)
        {
            int rootI = Find(i);
            int rootJ = Find(j);
            if (rootI != rootJ)
            {
                parent[rootI] = rootJ;
                return true;
            }
            return false;
        }

        var candidateEdges = new List<(int U, int V, double Cost)>();
        for (int i = 0; i < pins.Count; i++)
        {
            var p1 = pins[i].Dev.GetAbsolutePinPosition(pins[i].PinIndex);
            for (int j = i + 1; j < pins.Count; j++)
            {
                var p2 = pins[j].Dev.GetAbsolutePinPosition(pins[j].PinIndex);
                double dx = Math.Abs(p1.X - p2.X);
                double dy = Math.Abs(p1.Y - p2.Y);

                // Collinear bonus: straight horizontal or vertical lines get lowest cost
                // If collinear segment passes through an obstacle, add penalty
                bool isCollinear = dx < 1e-4 || dy < 1e-4;
                bool blocked = isCollinear && SchematicWireRouter.HasObstacleBetween(circuit, p1.X, p1.Y, p2.X, p2.Y, pins[i].Dev, pins[j].Dev);
                double cost = (isCollinear && !blocked) ? (dx + dy) : (dx + dy + 2.0);
                candidateEdges.Add((i, j, cost));
            }
        }

        candidateEdges.Sort((a, b) => a.Cost.CompareTo(b.Cost));

        int edgesCount = 0;
        foreach (var edge in candidateEdges)
        {
            if (Union(edge.U, edge.V))
            {
                result.Add((pins[edge.U], pins[edge.V]));
                edgesCount++;
                if (edgesCount == pins.Count - 1) break;
            }
        }

        return result;
    }

    /// <summary>
    /// Recalculates intelligent orthogonal routes for all wires in the circuit.
    /// Custom routed wires adapt their terminal segments to remain locked to moving device pins,
    /// preserving Manhattan orthogonality and the user's custom intermediate waypoints.
    /// </summary>
    public void RouteAllWires()
    {
        foreach (var wire in Wires)
        {
            var dev1 = Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dev2 = Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (dev1 == null || dev2 == null) continue;

            if (wire.IsCustomPath && wire.PathPoints != null && wire.PathPoints.Count >= 2)
            {
                AdaptCustomWireEndpoints(wire, dev1, dev2);
                continue;
            }

            wire.PathPoints = SchematicWireRouter.RouteWire(this, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
        }
    }

    /// <summary>
    /// Adapts the terminal points of a custom routed wire when connected devices move,
    /// maintaining strict Manhattan orthogonality and preserving intermediate custom routing.
    /// </summary>
    public void AdaptCustomWireEndpoints(CircuitWire wire, Device dev1, Device dev2)
    {
        if (wire.PathPoints == null || wire.PathPoints.Count < 2) return;

        var pStart = dev1.GetAbsolutePinPosition(wire.FromPinIndex);
        var pEnd = dev2.GetAbsolutePinPosition(wire.ToPinIndex);
        var pts = wire.PathPoints;

        // Check if both devices moved by the exact same delta from their wire endpoints (e.g. multi-select drag)
        double deltaStartDx = pStart.X - pts[0].X;
        double deltaStartDy = pStart.Y - pts[0].Y;
        double deltaEndDx = pEnd.X - pts[^1].X;
        double deltaEndDy = pEnd.Y - pts[^1].Y;

        if (Math.Abs(deltaStartDx - deltaEndDx) < 1e-4 && Math.Abs(deltaStartDy - deltaEndDy) < 1e-4 &&
            (Math.Abs(deltaStartDx) > 1e-4 || Math.Abs(deltaStartDy) > 1e-4))
        {
            // Both devices moved together by (deltaStartDx, deltaStartDy) - translate entire custom polyline rigidly
            for (int i = 0; i < pts.Count; i++)
            {
                pts[i] = (pts[i].X + deltaStartDx, pts[i].Y + deltaStartDy);
            }
            return;
        }

        bool startMoved = Math.Abs(pts[0].X - pStart.X) > 1e-4 || Math.Abs(pts[0].Y - pStart.Y) > 1e-4;
        bool endMoved = Math.Abs(pts[^1].X - pEnd.X) > 1e-4 || Math.Abs(pts[^1].Y - pEnd.Y) > 1e-4;
        if (!startMoved && !endMoved)
        {
            // Neither connected device moved: preserve user custom path as-is
            return;
        }

        // 1. Adapt start endpoint if dev1 pin moved
        if (startMoved)
        {
            AdaptStartEndpoint(pts, pStart, dev1.GetPinNormal(wire.FromPinIndex));
        }

        // 2. Adapt end endpoint if dev2 pin moved
        if (endMoved)
        {
            AdaptEndEndpoint(pts, pEnd, dev2.GetPinNormal(wire.ToPinIndex));
        }

        // 3. Ensure strict Manhattan orthogonality throughout the entire path
        EnsureManhattanPath(pts, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);

        // 4. Simplify any redundant collinear segments
        wire.SimplifyCollinear();

        // 5. Look for devices! If any segment of the adapted wire collides with or penetrates any component body,
        // automatically reroute cleanly around all obstacles in the circuit.
        bool collidesWithDevice = false;
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            if (SchematicWireRouter.FindCollidingDeviceForSegment(this, wire.PathPoints[i].X, wire.PathPoints[i].Y, wire.PathPoints[i + 1].X, wire.PathPoints[i + 1].Y, dev1, dev2) != null)
            {
                collidesWithDevice = true;
                break;
            }
        }

        if (collidesWithDevice)
        {
            wire.PathPoints = SchematicWireRouter.RouteWire(this, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
            wire.IsCustomPath = false;
        }
    }

    private static void AdaptStartEndpoint(List<(double X, double Y)> pts, (double X, double Y) pStart, (int Dx, int Dy) normal)
    {
        if (pts.Count == 2)
        {
            pts[0] = pStart;
            if (Math.Abs(pStart.X - pts[1].X) > 1e-4 && Math.Abs(pStart.Y - pts[1].Y) > 1e-4)
            {
                var corner = (normal.Dx != 0) ? (pts[1].X, pStart.Y) : (pStart.X, pts[1].Y);
                pts.Insert(1, corner);
            }
            return;
        }

        if (pts.Count == 3)
        {
            // With 3 points: pts[0] is start pin, pts[1] is end pin's entry corner, pts[2] is end pin.
            // Do NOT overwrite pts[1] because that could violate end pin's entry direction!
            var corner = pts[1];
            var endPt = pts[2];
            pts[0] = pStart;

            bool lastIsH = Math.Abs(corner.Y - endPt.Y) < 1e-4;
            if (lastIsH)
            {
                if (Math.Abs(pStart.X - corner.X) > 1e-4 && Math.Abs(pStart.Y - corner.Y) > 1e-4)
                {
                    pts.Insert(1, (pStart.X, corner.Y));
                }
            }
            else
            {
                if (Math.Abs(pStart.X - corner.X) > 1e-4 && Math.Abs(pStart.Y - corner.Y) > 1e-4)
                {
                    pts.Insert(1, (corner.X, pStart.Y));
                }
            }
            return;
        }

        var p0 = pts[0];
        var p1 = pts[1];
        bool wasH = Math.Abs(p0.Y - p1.Y) < 1e-4;
        bool wasV = Math.Abs(p0.X - p1.X) < 1e-4;

        if (wasH)
        {
            if (Math.Abs(pStart.Y - p1.Y) < 1e-4)
            {
                pts[0] = pStart;
            }
            else
            {
                pts[0] = pStart;
                pts[1] = (p1.X, pStart.Y);
            }
        }
        else if (wasV)
        {
            if (Math.Abs(pStart.X - p1.X) < 1e-4)
            {
                pts[0] = pStart;
            }
            else
            {
                pts[0] = pStart;
                pts[1] = (pStart.X, p1.Y);
            }
        }
        else
        {
            pts[0] = pStart;
            var corner = (normal.Dx != 0) ? (pts[1].X, pStart.Y) : (pStart.X, pts[1].Y);
            pts.Insert(1, corner);
        }
    }

    private static void AdaptEndEndpoint(List<(double X, double Y)> pts, (double X, double Y) pEnd, (int Dx, int Dy) normal)
    {
        if (pts.Count == 2)
        {
            pts[^1] = pEnd;
            if (Math.Abs(pEnd.X - pts[0].X) > 1e-4 && Math.Abs(pEnd.Y - pts[0].Y) > 1e-4)
            {
                var corner = (normal.Dx != 0) ? (pts[0].X, pEnd.Y) : (pEnd.X, pts[0].Y);
                pts.Insert(1, corner);
            }
            return;
        }

        if (pts.Count == 3)
        {
            // With 3 points: pts[0] is start pin, pts[1] is start pin's escape corner, pts[2] is end pin.
            // Do NOT overwrite pts[1] because that violates start pin's escape direction!
            var p0 = pts[0];
            var p1 = pts[1];
            pts[^1] = pEnd;

            bool firstIsH = Math.Abs(p0.Y - p1.Y) < 1e-4;
            if (firstIsH)
            {
                if (Math.Abs(p1.Y - pEnd.Y) > 1e-4 && Math.Abs(p1.X - pEnd.X) > 1e-4)
                {
                    pts.Insert(2, (p1.X, pEnd.Y));
                }
            }
            else
            {
                if (Math.Abs(p1.X - pEnd.X) > 1e-4 && Math.Abs(p1.Y - pEnd.Y) > 1e-4)
                {
                    pts.Insert(2, (pEnd.X, p1.Y));
                }
            }
            return;
        }

        var pLast = pts[^1];
        var pPrev = pts[^2];
        bool wasH = Math.Abs(pPrev.Y - pLast.Y) < 1e-4;
        bool wasV = Math.Abs(pPrev.X - pLast.X) < 1e-4;

        if (wasH)
        {
            if (Math.Abs(pEnd.Y - pPrev.Y) < 1e-4)
            {
                pts[^1] = pEnd;
            }
            else
            {
                pts[^1] = pEnd;
                pts[^2] = (pPrev.X, pEnd.Y);
            }
        }
        else if (wasV)
        {
            if (Math.Abs(pEnd.X - pPrev.X) < 1e-4)
            {
                pts[^1] = pEnd;
            }
            else
            {
                pts[^1] = pEnd;
                pts[^2] = (pEnd.X, pPrev.Y);
            }
        }
        else
        {
            pts[^1] = pEnd;
            var corner = (normal.Dx != 0) ? (pEnd.X, pts[^2].Y) : (pts[^2].X, pEnd.Y);
            pts.Insert(pts.Count - 1, corner);
        }
    }

    private static void EnsureManhattanPath(List<(double X, double Y)> pts, Device dev1, int pin1, Device dev2, int pin2)
    {
        for (int i = 0; i < pts.Count - 1; i++)
        {
            double dx = Math.Abs(pts[i].X - pts[i + 1].X);
            double dy = Math.Abs(pts[i].Y - pts[i + 1].Y);
            if (dx > 1e-4 && dy > 1e-4)
            {
                // Diagonal segment detected! Repair into orthogonal dogleg
                (double X, double Y) corner;
                if (i == 0)
                {
                    var (normDx, _) = dev1.GetPinNormal(pin1);
                    corner = normDx != 0 ? (pts[i + 1].X, pts[i].Y) : (pts[i].X, pts[i + 1].Y);
                }
                else
                {
                    var (normDx, _) = dev2.GetPinNormal(pin2);
                    corner = normDx != 0 ? (pts[i].X, pts[i + 1].Y) : (pts[i + 1].X, pts[i].Y);
                }
                pts.Insert(i + 1, corner);
                i++;
            }
        }
    }
}
