using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Ground;
using FryCircuit.Core.Devices.VoltageSource;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.IO;

/// <summary>
/// Professional EDA Schematic Auto-Layout Engine.
/// Automatically places circuit components on a clean schematic grid based on signal flow,
/// power/ground topology, and component roles when explicit (x, y) coordinates are omitted by LLMs.
/// </summary>
public static class CircuitAutoLayoutEngine
{
    public static void Layout(Circuit circuit)
    {
        if (circuit.Devices.Count == 0) return;

        // Group devices by functional role
        var sources = new List<Device>();
        var grounds = new List<Device>();
        var activeIcs = new List<Device>();
        var passives = new List<Device>();
        var loadsAndMeters = new List<Device>();

        int groundNodeId = circuit.FindGroundNode()?.Id ?? 0;

        foreach (var dev in circuit.Devices)
        {
            if (dev is GroundDevice || dev.Type == "ground")
            {
                grounds.Add(dev);
            }
            else if (dev is VoltageSourceDevice || dev is CurrentSourceDevice || dev.Type is "vsource" or "csource" or "battery")
            {
                sources.Add(dev);
            }
            else if (dev.Type is "opamp" or "timer555" or "npn" or "pnp" or "nmos" or "pmos" or "and" or "or" or "not" or "nand" or "nor" or "xor" or "xnor")
            {
                activeIcs.Add(dev);
            }
            else if (dev.Type is "voltmeter" or "amperemeter" or "lamp" or "led" or "seg7" or "display")
            {
                loadsAndMeters.Add(dev);
            }
            else
            {
                passives.Add(dev);
            }
        }

        // Trace net adjacency to determine signal flow columns
        var nodeToDevices = new Dictionary<int, List<Device>>();
        foreach (var dev in circuit.Devices)
        {
            foreach (var pin in dev.Pins)
            {
                if (pin.NodeId <= 0 && pin.NodeId != groundNodeId) continue;
                if (!nodeToDevices.TryGetValue(pin.NodeId, out var devList))
                {
                    devList = new List<Device>();
                    nodeToDevices[pin.NodeId] = devList;
                }
                if (!devList.Contains(dev))
                {
                    devList.Add(dev);
                }
            }
        }

        // Determine topological depth/level for each device using BFS from sources
        var visited = new HashSet<Device>();
        var deviceDepth = new Dictionary<Device, int>();

        var queue = new Queue<(Device Dev, int Depth)>();
        foreach (var src in sources)
        {
            queue.Enqueue((src, 0));
            visited.Add(src);
            deviceDepth[src] = 0;
        }

        while (queue.Count > 0)
        {
            var (currentDev, depth) = queue.Dequeue();

            foreach (var pin in currentDev.Pins)
            {
                if (pin.NodeId == groundNodeId) continue; // Do not traverse through Ground rail
                if (!nodeToDevices.TryGetValue(pin.NodeId, out var neighbors)) continue;

                foreach (var neighbor in neighbors)
                {
                    if (neighbor is GroundDevice || neighbor.Type == "ground") continue;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        int nextDepth = depth + 1;
                        deviceDepth[neighbor] = nextDepth;
                        queue.Enqueue((neighbor, nextDepth));
                    }
                }
            }
        }

        // Any unvisited devices get placed in a default stage
        int maxDepth = deviceDepth.Values.Count > 0 ? deviceDepth.Values.Max() : 0;
        foreach (var dev in circuit.Devices)
        {
            if (dev is GroundDevice || dev.Type == "ground") continue;
            if (!visited.Contains(dev))
            {
                deviceDepth[dev] = maxDepth + 1;
            }
        }

        // Group non-ground devices into columns by depth
        var columns = new Dictionary<int, List<Device>>();
        foreach (var (dev, depth) in deviceDepth)
        {
            if (!columns.TryGetValue(depth, out var colList))
            {
                colList = new List<Device>();
                columns[depth] = colList;
            }
            colList.Add(dev);
        }

        // Position devices column by column
        double startX = 3.0;
        double colSpacing = 4.5;
        double rowSpacing = 3.5;
        double baselineY = 6.0;

        var sortedDepths = columns.Keys.OrderBy(d => d).ToList();
        double currentX = startX;

        foreach (var depth in sortedDepths)
        {
            var devList = columns[depth];
            int count = devList.Count;

            // Center the column vertically around baselineY
            double colStartY = baselineY - ((count - 1) * rowSpacing * 0.5);

            for (int i = 0; i < count; i++)
            {
                var dev = devList[i];
                double x = currentX;
                double y = colStartY + (i * rowSpacing);

                dev.LocationX = Math.Round(x, 1);
                dev.LocationY = Math.Round(y, 1);

                // Smart orientation
                if (dev is VoltageSourceDevice)
                {
                    dev.Transform.Rotation = DeviceRotation.Up;
                }
                else if (dev.Pins.Count == 2)
                {
                    // Check if one pin is Ground -> Shunt component, orient vertically
                    bool hasGnd = dev.Pins.Any(p => p.NodeId == groundNodeId);
                    if (hasGnd)
                    {
                        dev.Transform.Rotation = DeviceRotation.Up; // vertical to ground
                    }
                    else
                    {
                        dev.Transform.Rotation = DeviceRotation.Right; // horizontal in-line
                    }
                }
            }

            currentX += colSpacing;
        }

        // Position ground symbols directly below the devices that reference ground
        double maxY = circuit.Devices.Count > 0 ? circuit.Devices.Max(d => d.LocationY) : baselineY;
        double gndY = maxY + 2.5;

        if (grounds.Count > 0)
        {
            double gndSpacing = 4.0;
            double gndStartX = startX;
            for (int i = 0; i < grounds.Count; i++)
            {
                grounds[i].LocationX = Math.Round(gndStartX + (i * gndSpacing), 1);
                grounds[i].LocationY = Math.Round(gndY, 1);
                grounds[i].Transform.Rotation = DeviceRotation.Up;
            }
        }
        else
        {
            // If circuit has ground node connections but no visual ground symbol, synthesize one
            bool hasGroundPins = circuit.Devices.Any(d => d.Pins.Any(p => p.NodeId == groundNodeId));
            if (hasGroundPins)
            {
                int nextId = (circuit.Devices.Count > 0 ? circuit.Devices.Max(d => d.Id) : 0) + 1;
                var gndDev = new GroundDevice(nextId, startX, gndY);
                gndDev.Pins[0].NodeId = groundNodeId;
                circuit.AddDevice(gndDev);
            }
        }

        // Recompute frame
        double minX = circuit.Devices.Min(d => d.LocationX) - 3;
        double minY = circuit.Devices.Min(d => d.LocationY) - 3;
        double maxX = circuit.Devices.Max(d => d.LocationX) + 3;
        maxY = circuit.Devices.Max(d => d.LocationY) + 3;

        circuit.Frame = new CircuitFrame(
            Math.Min(0, minX),
            Math.Min(0, minY),
            Math.Max(20, maxX),
            Math.Max(16, maxY)
        );
    }
}
