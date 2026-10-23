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

    [Fact]
    public void RouteWire_EscObstacleBody_RoutesAroundBoundingBox()
    {
        var circuit = new Circuit();
        // ESC device centered at (10, 10), bounding box is 4.4 x 2.6
        var esc = new FryCircuit.Core.Devices.Aerospace.EscDevice(1, x: 10, y: 10);
        circuit.AddDevice(esc);

        // Place two resistors above and below the ESC:
        // r1 at (10, 4) pin 1 at (10, 5) normal (0, 1) [DOWN]
        var r1 = new ResistorDevice(2, x: 10, y: 4);
        // r2 at (10, 16) pin 0 at (10, 15) normal (0, -1) [UP]
        var r2 = new ResistorDevice(3, x: 10, y: 16);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        var path = SchematicWireRouter.RouteWire(circuit, r1, 1, r2, 0);

        Assert.NotNull(path);
        Assert.True(path.Count >= 4);

        // Wire must not pass directly through the interior of the ESC bounding box
        bool passesThroughCenter = path.Any(p => Math.Abs(p.X - 10.0) < 0.2 && Math.Abs(p.Y - 10.0) < 0.2);
        Assert.False(passesThroughCenter, "Wire must route around the ESC bounding box and not pass through its center");
    }

    [Fact]
    public void Wire_SimplifyCollinear_CollapsesInlineSegments()
    {
        var wire = new CircuitWire(1, 1, 1, 0, 2, 0)
        {
            PathPoints = new List<(double X, double Y)>
            {
                (0, 0),
                (2, 0),
                (5, 0),
                (5, 3),
                (5, 7)
            }
        };

        wire.SimplifyCollinear();

        Assert.Equal(3, wire.PathPoints.Count);
        Assert.Equal((0.0, 0.0), wire.PathPoints[0]);
        Assert.Equal((5.0, 0.0), wire.PathPoints[1]);
        Assert.Equal((5.0, 7.0), wire.PathPoints[2]);
    }

    [Fact]
    public void CustomPath_PreservedDuringAutoRoute()
    {
        var circuit = new Circuit();
        var r1 = new ResistorDevice(1, x: 2, y: 5);
        var r2 = new ResistorDevice(2, x: 8, y: 5);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        var wire = new CircuitWire(1, 1, r1.Id, 1, r2.Id, 0)
        {
            IsCustomPath = true,
            PathPoints = new List<(double X, double Y)>
            {
                (2.0, 6.0),
                (2.0, 12.0),
                (8.0, 12.0),
                (8.0, 4.0)
            }
        };
        circuit.Wires.Add(wire);

        // Auto-route sweep must NOT touch or overwrite custom wire path
        circuit.RouteAllWires();

        Assert.True(wire.IsCustomPath);
        Assert.Equal(4, wire.PathPoints.Count);
        Assert.Equal((2.0, 12.0), wire.PathPoints[1]);
        Assert.Equal((8.0, 12.0), wire.PathPoints[2]);
    }

    [Fact]
    public void CustomPath_WhenConnectedDeviceMoves_TerminalPinAdaptsOrthogonally()
    {
        var circuit = new Circuit();
        var r1 = new ResistorDevice(1, x: 2, y: 5);
        var r2 = new ResistorDevice(2, x: 8, y: 5);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        var wire = new CircuitWire(1, 1, r1.Id, 1, r2.Id, 1)
        {
            IsCustomPath = true,
            PathPoints = new List<(double X, double Y)>
            {
                (2.0, 6.0),
                (2.0, 12.0),
                (8.0, 12.0),
                (8.0, 6.0)
            }
        };
        circuit.Wires.Add(wire);

        // Move device r2 to a new location off-axis
        r2.LocationX = 12;
        r2.LocationY = 7;

        // Trigger route update
        circuit.RouteAllWires();

        // 1. Must stay custom
        Assert.True(wire.IsCustomPath);

        // 2. Start endpoint must stay locked to r1 pin 1
        Assert.Equal(r1.GetAbsolutePinPosition(1), wire.PathPoints[0]);

        // 3. End endpoint must stay locked to r2 pin 1 (never detached in thin air)
        Assert.Equal(r2.GetAbsolutePinPosition(1), wire.PathPoints[^1]);

        // 4. Intermediate custom route must be preserved
        Assert.Equal((2.0, 12.0), wire.PathPoints[1]);

        // 5. Every segment must be strictly Manhattan orthogonal (never diagonal)
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(wire.PathPoints[i].X - wire.PathPoints[i + 1].X) < 1e-4 ||
                                Math.Abs(wire.PathPoints[i].Y - wire.PathPoints[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal, $"Segment {i} ({wire.PathPoints[i]} -> {wire.PathPoints[i + 1]}) must be Manhattan orthogonal");
        }
    }

    [Fact]
    public void CustomPath_WhenBothDevicesMoveTogether_TranslatesEntirePathRigidly()
    {
        var circuit = new Circuit();
        var r1 = new ResistorDevice(1, x: 2, y: 5);
        var r2 = new ResistorDevice(2, x: 8, y: 5);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        var wire = new CircuitWire(1, 1, r1.Id, 1, r2.Id, 0)
        {
            IsCustomPath = true,
            PathPoints = new List<(double X, double Y)>
            {
                (2.0, 6.0),
                (2.0, 12.0),
                (8.0, 12.0),
                (8.0, 4.0)
            }
        };
        circuit.Wires.Add(wire);

        // Move both devices together by (+4, +3)
        r1.LocationX = 6;
        r1.LocationY = 8;
        r2.LocationX = 12;
        r2.LocationY = 8;

        circuit.RouteAllWires();

        Assert.True(wire.IsCustomPath);
        Assert.Equal(r1.GetAbsolutePinPosition(1), wire.PathPoints[0]);
        Assert.Equal((6.0, 15.0), wire.PathPoints[1]);
        Assert.Equal((12.0, 15.0), wire.PathPoints[2]);
        Assert.Equal(r2.GetAbsolutePinPosition(0), wire.PathPoints[^1]);

        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(wire.PathPoints[i].X - wire.PathPoints[i + 1].X) < 1e-4 ||
                                Math.Abs(wire.PathPoints[i].Y - wire.PathPoints[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal);
        }
    }

    [Fact]
    public void ConnectPinToWire_WithExactTapPoint_SplitsAndConnectsAtTap()
    {
        var circuit = new Circuit();
        var r1 = new ResistorDevice(1, x: 2, y: 5);
        var r2 = new ResistorDevice(2, x: 10, y: 5);
        circuit.AddDevice(r1);
        circuit.AddDevice(r2);

        var w1 = new CircuitWire(1, 1, r1.Id, 1, r2.Id, 0)
        {
            PathPoints = new List<(double X, double Y)>
            {
                (2.0, 6.0),
                (2.0, 8.0),
                (10.0, 8.0),
                (10.0, 4.0)
            }
        };
        circuit.Wires.Add(w1);

        // Third device taps into w1 at exact point (6.0, 8.0)
        var cap = new CapacitorDevice(3, x: 6, y: 12);
        circuit.AddDevice(cap);

        var w2 = circuit.ConnectPinToWire(cap, 0, w1, tapPoint: (6.0, 8.0));

        Assert.NotNull(w2);
        Assert.True(w1.IsCustomPath);
        Assert.True(w2.IsCustomPath);
        Assert.Equal(w1.NodeId, w2.NodeId);

        // Target wire must contain the tap vertex
        Assert.Contains(w1.PathPoints, p => Math.Abs(p.X - 6.0) < 1e-4 && Math.Abs(p.Y - 8.0) < 1e-4);

        // New wire end point must connect at the tap point
        Assert.Equal((6.0, 8.0), w2.PathPoints[^1]);
    }

    [Fact]
    public void ModifyWirePathAction_UndoRedo_RestoresState()
    {
        var wire = new CircuitWire(1, 1, 1, 0, 2, 0)
        {
            IsCustomPath = false,
            PathPoints = new List<(double X, double Y)> { (0, 0), (5, 0) }
        };

        var oldPath = wire.ClonePath();
        var newPath = new List<(double X, double Y)> { (0, 0), (0, 3), (5, 3), (5, 0) };

        var action = new FryCircuit.Core.History.ModifyWirePathAction(wire, oldPath, newPath, false, true);

        // Apply new path
        action.Redo();
        Assert.True(wire.IsCustomPath);
        Assert.Equal(4, wire.PathPoints.Count);

        // Undo
        action.Undo();
        Assert.False(wire.IsCustomPath);
        Assert.Equal(2, wire.PathPoints.Count);
        Assert.Equal((5.0, 0.0), wire.PathPoints[1]);
    }

    [Fact]
    public void Device_Intersects_DetectsOverlapAt1GridSpacing()
    {
        // Diode at (6, 2) and Resistor at (6, 3) (Scenario from User Screenshot 2)
        var diode = new DiodeDevice(1, x: 6, y: 2);
        var resistor = new ResistorDevice(2, x: 6, y: 3);

        // Center distance in Y is 1.0 (previously failed naive < 0.6 check)
        bool collides = diode.Intersects(resistor);
        Assert.True(collides, "Diode and Resistor separated by 1.0 grid unit must be detected as an overlap collision");
    }

    [Fact]
    public void Device_Intersects_ReportsFalseWhenSeparated()
    {
        var diode = new DiodeDevice(1, x: 6, y: 2);
        var resistor = new ResistorDevice(2, x: 6, y: 6);

        bool collides = diode.Intersects(resistor);
        Assert.False(collides, "Diode and Resistor separated by 4.0 grid units must NOT collide");
    }

    [Fact]
    public void Device_GetBoundingBox_RespectsRotation()
    {
        var esc = new FryCircuit.Core.Devices.Aerospace.EscDevice(1, x: 10, y: 10);
        var (unrotMinX, unrotMaxX, unrotMinY, unrotMaxY) = esc.GetBoundingBox(padding: 0);

        // Unrotated width = 4.4, height = 2.6
        Assert.Equal(4.4, unrotMaxX - unrotMinX, 2);
        Assert.Equal(2.6, unrotMaxY - unrotMinY, 2);

        // Rotate 90 degrees Right
        esc.Transform = new DeviceTransform(DeviceRotation.Right);
        var (rotMinX, rotMaxX, rotMinY, rotMaxY) = esc.GetBoundingBox(padding: 0);

        // Rotated width = 2.6, height = 4.4
        Assert.Equal(2.6, rotMaxX - rotMinX, 2);
        Assert.Equal(4.4, rotMaxY - rotMinY, 2);
    }

    [Fact]
    public void SchematicWireRouter_FindCollidingDeviceForSegment_DetectsSegmentPassingThroughBody()
    {
        var circuit = new Circuit();
        var diode = new DiodeDevice(1, x: 6, y: 6);
        circuit.AddDevice(diode);

        // Segment straight through the center of the diode: (4, 6) -> (8, 6)
        var hit = SchematicWireRouter.FindCollidingDeviceForSegment(circuit, 4, 6, 8, 6);
        Assert.NotNull(hit);
        Assert.Equal(diode.Id, hit.Id);

        // Segment clearly outside: (4, 10) -> (8, 10)
        var hitOutside = SchematicWireRouter.FindCollidingDeviceForSegment(circuit, 4, 10, 8, 10);
        Assert.Null(hitOutside);
    }

    [Fact]
    public void FindCollidingDeviceForSegment_SegmentPenetratingConnectedDeviceInward_ReturnsDevice()
    {
        var circuit = new Circuit();
        var vs = new VoltageSourceDevice(1, x: 2, y: 5);
        circuit.AddDevice(vs);

        // Top pin 0 is at (2, 4) with normal (0, -1) [UP]
        // An inward segment heading down through the body to (2, 8) must be flagged as a collision
        var hit = SchematicWireRouter.FindCollidingDeviceForSegment(circuit, 2, 4, 2, 8, ignoreDev1: vs);
        Assert.NotNull(hit);
        Assert.Equal(vs.Id, hit.Id);

        // A valid outward segment heading UP to (2, 2) must NOT be flagged as collision
        var validOutward = SchematicWireRouter.FindCollidingDeviceForSegment(circuit, 2, 4, 2, 2, ignoreDev1: vs);
        Assert.Null(validOutward);
    }

    [Fact]
    public void SimplifyCollinear_ReversalCorner_NeverCollapsed()
    {
        var wire = new CircuitWire(1, 1, 1, 0, 2, 0)
        {
            PathPoints = new List<(double X, double Y)>
            {
                (2.0, 4.0),
                (2.0, 2.0),
                (2.0, 8.0)
            }
        };

        // Reversal at (2.0, 2.0) must be preserved and never collapsed into a single through-line
        wire.SimplifyCollinear();

        Assert.Equal(3, wire.PathPoints.Count);
        Assert.Equal((2.0, 4.0), wire.PathPoints[0]);
        Assert.Equal((2.0, 2.0), wire.PathPoints[1]);
        Assert.Equal((2.0, 8.0), wire.PathPoints[2]);
    }

    [Fact]
    public void CustomPath_HalfWaveRectifier_WhenDiodeMovedBelowSource_NeverCutsThroughSourceBody()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateHalfWaveRectifier();
        var vs = circuit.Devices.First(d => d.Id == 1);
        var diode = circuit.Devices.First(d => d.Id == 2);

        // Synthesize and route initial wires: wire between vs pin 0 (2, 4) and diode pin 0 (5, 2)
        circuit.EnsureWiresSynthesized();
        var wire = circuit.Wires.First(w => (w.FromDeviceId == vs.Id && w.ToDeviceId == diode.Id) || (w.FromDeviceId == diode.Id && w.ToDeviceId == vs.Id));
        Assert.NotNull(wire);

        // Move Diode below the voltage source (diode y = 8, pin 0 at (5, 8))
        diode.LocationY = 8;
        circuit.RouteAllWires();

        // The wire MUST NOT cut through the voltage source body at (2, 5)
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            var p1 = wire.PathPoints[i];
            var p2 = wire.PathPoints[i + 1];

            // If segment is vertical along X = 2, it must not cross Y = 5
            if (Math.Abs(p1.X - 2.0) < 0.1 && Math.Abs(p2.X - 2.0) < 0.1)
            {
                double minY = Math.Min(p1.Y, p2.Y);
                double maxY = Math.Max(p1.Y, p2.Y);
                bool traversesSourceCenter = minY < 5.0 && maxY > 5.0;
                Assert.False(traversesSourceCenter, $"Segment {i} ({p1} -> {p2}) must not cross through the voltage source body at Y = 5");
            }
        }

        // Verify strictly Manhattan orthogonal
        for (int i = 0; i < wire.PathPoints.Count - 1; i++)
        {
            bool isOrthogonal = Math.Abs(wire.PathPoints[i].X - wire.PathPoints[i + 1].X) < 1e-4 ||
                                Math.Abs(wire.PathPoints[i].Y - wire.PathPoints[i + 1].Y) < 1e-4;
            Assert.True(isOrthogonal, $"Segment {i} must be orthogonal");
        }
    }
}
