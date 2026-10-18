using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class ParticleFlowEngineTests
{
    [Fact]
    public void GetPinExitCurrent_VoltageSource_ExitsPin0AndEntersPin1()
    {
        var vs = new VoltageSourceDevice(1, 0, 0, voltage: 10.0)
        {
            Current = 2.5 // Supplying 2.5 A
        };

        double exitPin0 = ParticleFlowEngine.GetPinExitCurrent(vs, 0);
        double exitPin1 = ParticleFlowEngine.GetPinExitCurrent(vs, 1);

        // Current must exit Pin 0 (+) into circuit, and enter Pin 1 (-)
        Assert.Equal(2.5, exitPin0, precision: 4);
        Assert.Equal(-2.5, exitPin1, precision: 4);
    }

    [Fact]
    public void GetPinExitCurrent_Battery_ExitsPin0AndEntersPin1()
    {
        var bat = new BatteryCellDevice(1, 0, 0)
        {
            Current = 1.8 // Supplying 1.8 A
        };

        double exitPin0 = ParticleFlowEngine.GetPinExitCurrent(bat, 0);
        double exitPin1 = ParticleFlowEngine.GetPinExitCurrent(bat, 1);

        Assert.Equal(1.8, exitPin0, precision: 4);
        Assert.Equal(-1.8, exitPin1, precision: 4);
    }

    [Fact]
    public void GetPinExitCurrent_Resistor_EntersPin0AndExitsPin1()
    {
        var r = new ResistorDevice(2, 0, 0, resistance: 100.0)
        {
            Current = 0.5 // 0.5 A flowing through from Pin 0 to Pin 1
        };

        double exitPin0 = ParticleFlowEngine.GetPinExitCurrent(r, 0);
        double exitPin1 = ParticleFlowEngine.GetPinExitCurrent(r, 1);

        // Pin 0 is entrance (-Current), Pin 1 is exit (+Current)
        Assert.Equal(-0.5, exitPin0, precision: 4);
        Assert.Equal(0.5, exitPin1, precision: 4);
    }

    [Fact]
    public void SyncWithCircuit_FullCircuitLoop_CreatesBranchesForBothWiresAndDevices()
    {
        var circuit = new Circuit("LoopTest");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        // 10V DC source at x=2, y=6 (Pin 0 at y=5, Pin 1 at y=7)
        var vs = new VoltageSourceDevice(1, 2, 6, voltage: 10.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Resistor 1 (100 Ohm) at x=6, y=2 (Pin 0 at x=5, Pin 1 at x=7)
        var r1 = new ResistorDevice(2, 6, 2, resistance: 100.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        // Resistor 2 (100 Ohm) at x=10, y=6 (Pin 0 at y=5, Pin 1 at y=7)
        var r2 = new ResistorDevice(3, 10, 6, resistance: 100.0);
        r2.Pins[0].NodeId = 2;
        r2.Pins[1].NodeId = 0;
        circuit.AddDevice(r2);

        // Add 3 connecting wires
        circuit.Wires.Add(new CircuitWire(1, 1, vs.Id, 0, r1.Id, 0));
        circuit.Wires.Add(new CircuitWire(2, 2, r1.Id, 1, r2.Id, 0));
        circuit.Wires.Add(new CircuitWire(3, 0, r2.Id, 1, vs.Id, 1));

        // Solve DC operating point
        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        // Total resistance = 200 Ohm, Current = 10V / 200 = 0.05 A (50 mA)
        Assert.Equal(0.05, vs.Current, precision: 4);
        Assert.Equal(0.05, r1.Current, precision: 4);
        Assert.Equal(0.05, r2.Current, precision: 4);

        // Sync particle flow engine
        var engine = new ParticleFlowEngine();
        engine.SyncWithCircuit(circuit);

        // Must have 6 branches: 3 wires + 3 components
        Assert.Equal(6, engine.Branches.Count);

        // Every branch in the circuit must have non-zero current equal to 0.05 A
        foreach (var branch in engine.Branches)
        {
            double cur = branch.CurrentProvider != null ? branch.CurrentProvider() : (branch.Device?.Current ?? 0.0);
            Assert.Equal(0.05, cur, precision: 4);
            Assert.NotEmpty(branch.Particles);
        }

        // Test particle updates
        var initialProgresses = engine.Branches.Select(b => b.Particles[0].Progress).ToList();
        engine.Update(0.016, 1.0); // 1 frame at 60fps

        for (int i = 0; i < engine.Branches.Count; i++)
        {
            // Progress must advance (not stay frozen at initial)
            Assert.NotEqual(initialProgresses[i], engine.Branches[i].Particles[0].Progress);
        }
    }
}
