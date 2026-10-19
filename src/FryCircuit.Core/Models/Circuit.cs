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

        // If pin1 doesn't have a valid node yet, create one
        if (targetNodeId <= 0 && oldNodeId <= 0)
        {
            int nextNode = (Nodes.Count > 0 ? Nodes.Keys.Max() : 0) + 1;
            targetNodeId = nextNode;
            oldNodeId = nextNode;
            GetOrCreateNode(targetNodeId);
            pin1.NodeId = targetNodeId;
            pin2.NodeId = targetNodeId;
        }
        else if (targetNodeId <= 0)
        {
            targetNodeId = oldNodeId;
            pin1.NodeId = targetNodeId;
        }
        else if (oldNodeId <= 0)
        {
            pin2.NodeId = targetNodeId;
        }
        else if (targetNodeId != oldNodeId)
        {
            // If oldNodeId was Ground, preserve ground
            bool oldIsGround = Nodes.TryGetValue(oldNodeId, out var on) && on.IsGround;
            bool targetIsGround = Nodes.TryGetValue(targetNodeId, out var tn) && tn.IsGround;

            int unifiedNode = (oldIsGround && !targetIsGround) ? oldNodeId : targetNodeId;
            int replacedNode = (unifiedNode == oldNodeId) ? targetNodeId : oldNodeId;

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

            targetNodeId = unifiedNode;
        }

        int nextWireId = (Wires.Count > 0 ? Wires.Max(w => w.Id) : 0) + 1;
        var wire = new CircuitWire(nextWireId, targetNodeId, dev1.Id, pinIndex1, dev2.Id, pinIndex2);
        Wires.Add(wire);
        return wire;
    }

    /// <summary>
    /// Connects a device pin to an existing wire net, forming a T-junction.
    /// </summary>
    public CircuitWire? ConnectPinToWire(Device dev, int pinIndex, CircuitWire targetWire)
    {
        var targetDev = Devices.FirstOrDefault(d => d.Id == targetWire.FromDeviceId) ??
                        Devices.FirstOrDefault(d => d.Id == targetWire.ToDeviceId);
        if (targetDev == null) return null;

        int targetPinIdx = (targetDev.Id == targetWire.FromDeviceId) ? targetWire.FromPinIndex : targetWire.ToPinIndex;
        var wire = ConnectPins(dev, pinIndex, targetDev, targetPinIdx);
        RouteAllWires();
        return wire;
    }

    /// <summary>
    /// Disconnects a pin from its node, assigning it a new isolated node.
    /// </summary>
    public void DisconnectPin(Device dev, int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= dev.Pins.Count) return;

        var pin = dev.Pins[pinIndex];
        // Remove wires attached to this pin
        Wires.RemoveAll(w => w.ConnectsToPin(dev.Id, pinIndex));

        // Allocate a new unique node ID
        int nextNode = (Nodes.Count > 0 ? Nodes.Keys.Max() : 0) + 1;
        pin.NodeId = nextNode;
        GetOrCreateNode(nextNode);
    }

    /// <summary>
    /// Removes a wire connection.
    /// </summary>
    public bool RemoveWire(CircuitWire wire)
    {
        return Wires.Remove(wire);
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
    /// </summary>
    public void RouteAllWires()
    {
        foreach (var wire in Wires)
        {
            var dev1 = Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dev2 = Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (dev1 != null && dev2 != null)
            {
                wire.PathPoints = SchematicWireRouter.RouteWire(this, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
            }
        }
    }
}
