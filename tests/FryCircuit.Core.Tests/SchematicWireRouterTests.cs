using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using Xunit;

namespace FryCircuit.Core.Tests;

public class SchematicWireRouterTests
{
    [Fact]
    public void RouteWire_CollinearFacingPins_ReturnsDirectLine()
    {
        var circuit = new Circuit();
        var d1 = new ResistorDevice(1, x: 2, y: 5); // Pin 1 is at (2, 6) pointing down (0, 1)
        var d2 = new ResistorDevice(2, x: 2, y: 9); // Pin 0 is at (2, 8) pointing up (0, -1)
        circuit.AddDevice(d1);
        circuit.AddDevice(d2);

        var path = SchematicWireRouter.RouteWire(circuit, d1, 1, d2, 0);

        Assert.NotNull(path);
        Assert.Equal(2, path.Count);
        Assert.Equal((2.0, 6.0), path[0]);
        Assert.Equal((2.0, 8.0), path[1]);
    }

    [Fact]
    public void RouteWire_PerpendicularPins_ReturnsCleanOneBendRoute()
    {
        var circuit = new Circuit();
        // d1 at (2, 5), top pin 0 at (2, 4) normal (0, -1) [UP]
        var d1 = new VoltageSourceDevice(1, x: 2, y: 5);
        // d2 at (6, 2) with rotation Left, pin 0 at (5, 2) normal (-1, 0) [LEFT]
        var d2 = new DiodeDevice(2, x: 6, y: 2)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        circuit.AddDevice(d1);
        circuit.AddDevice(d2);

        var path = SchematicWireRouter.RouteWire(circuit, d1, 0, d2, 0);

        Assert.NotNull(path);
        Assert.True(path.Count >= 2);
        // Start is at (2, 4)
        Assert.Equal((2.0, 4.0), path[0]);
        // End is at (5, 2)
        Assert.Equal((5.0, 2.0), path[^1]);

        // Path must be strictly orthogonal (every segment is horizontal or vertical)
        for (int i = 0; i < path.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(path[i].X - path[i + 1].X) < 1e-4 ||
                                Math.Abs(path[i].Y - path[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal, $"Segment {i} -> {i + 1} must be orthogonal");
        }
    }

    [Fact]
    public void RouteWire_ObstacleInDirectPath_RoutesAroundObstacle()
    {
        var circuit = new Circuit();
        // d1 at (2, 5), Pin 1 at (2, 6) normal (0, 1) [DOWN]
        var d1 = new ResistorDevice(1, x: 2, y: 5);
        // d2 at (2, 11), Pin 0 at (2, 10) normal (0, -1) [UP]
        var d2 = new ResistorDevice(2, x: 2, y: 11);
        // Obstacle right in the direct collinear path between d1 and d2 at (2, 8)
        var obstacle = new ResistorDevice(3, x: 2, y: 8);

        circuit.AddDevice(d1);
        circuit.AddDevice(d2);
        circuit.AddDevice(obstacle);

        var path = SchematicWireRouter.RouteWire(circuit, d1, 1, d2, 0);

        Assert.NotNull(path);
        Assert.True(path.Count >= 3);
        // Path must not pass directly through the obstacle center (2, 8)
        bool crossesObstacleCenter = path.Any(p => Math.Abs(p.X - 2.0) < 0.1 && Math.Abs(p.Y - 8.0) < 0.1);
        Assert.False(crossesObstacleCenter, "Wire must route around obstacle and not pass through its center");

        // Every segment must be orthogonal
        for (int i = 0; i < path.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(path[i].X - path[i + 1].X) < 1e-4 ||
                                Math.Abs(path[i].Y - path[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal);
        }
    }

    [Fact]
    public void DynamicRubberbanding_WhenDeviceMoves_WireRecalculatesOrthogonally()
    {
        var circuit = new Circuit();
        var d1 = new VoltageSourceDevice(1, x: 2, y: 5);
        var d2 = new DiodeDevice(2, x: 6, y: 2) { Transform = new DeviceTransform(DeviceRotation.Left) };
        circuit.AddDevice(d1);
        circuit.AddDevice(d2);

        var wire = new CircuitWire(1, 2, d1.Id, 0, d2.Id, 0);
        circuit.Wires.Add(wire);

        // Initial route
        circuit.RouteAllWires();
        Assert.NotEmpty(wire.PathPoints);
        Assert.Equal((2.0, 4.0), wire.PathPoints[0]);
        Assert.Equal((5.0, 2.0), wire.PathPoints[^1]);

        // Drag diode to a new location
        d2.LocationX = 8;
        d2.LocationY = 3;

        // Dynamic rubberbanding
        circuit.RouteAllWires();
        var (newAnodeX, newAnodeY) = d2.GetAbsolutePinPosition(0);
        Assert.Equal((newAnodeX, newAnodeY), wire.PathPoints[^1]);

        // Still strictly orthogonal
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(wire.PathPoints[i].X - wire.PathPoints[i + 1].X) < 1e-4 ||
                                Math.Abs(wire.PathPoints[i].Y - wire.PathPoints[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal);
        }
    }

    [Fact]
    public void ConnectPinToWire_CreatesValidWireAndUnifiedNode()
    {
        var circuit = new Circuit();
        var r1 = new ResistorDevice(1, x: 2, y: 5);
        var r2 = new ResistorDevice(2, x: 6, y: 5);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        // Initial wire between r1 pin 1 (2, 6) and r2 pin 0 (6, 4)
        var w1 = circuit.ConnectPins(r1, 1, r2, 0);
        Assert.NotNull(w1);
        Assert.Equal(r1.Pins[1].NodeId, r2.Pins[0].NodeId);

        // New capacitor at (4, 8) connects pin 0 (4, 7) to w1
        var cap = new CapacitorDevice(3, x: 4, y: 8);
        circuit.AddDevice(cap);

        var w2 = circuit.ConnectPinToWire(cap, 0, w1);
        Assert.NotNull(w2);
        // Node unified
        Assert.Equal(w1.NodeId, cap.Pins[0].NodeId);
        Assert.Equal(w1.NodeId, w2.NodeId);

        // Path routed orthogonally
        Assert.NotEmpty(w2.PathPoints);
        for (int i = 0; i < w2.PathPoints.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(w2.PathPoints[i].X - w2.PathPoints[i + 1].X) < 1e-4 ||
                                Math.Abs(w2.PathPoints[i].Y - w2.PathPoints[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal);
        }
    }
}
