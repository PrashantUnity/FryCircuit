using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class MnaSolverTests
{
    [Fact]
    public void ResistorDivider_CalculatesCorrectNodalVoltages()
    {
        // 10V DC source connected to two 1000 Ohm resistors in series to ground
        // Node 0: Ground (0V)
        // Node 1: +10V (from voltage source)
        // Node 2: Midpoint (+5V)
        var circuit = new Circuit("Divider");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        // Voltage Source between Node 1 (+) and Node 0 (-)
        var vs = new VoltageSourceDevice(1, 0, 0, voltage: 10.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Resistor 1 between Node 1 and Node 2 (1000 Ohm)
        var r1 = new ResistorDevice(2, 0, 0, resistance: 1000.0);
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        // Resistor 2 between Node 2 and Node 0 (1000 Ohm)
        var r2 = new ResistorDevice(3, 0, 0, resistance: 1000.0);
        r2.Pins[0].NodeId = 2;
        r2.Pins[1].NodeId = 0;
        circuit.AddDevice(r2);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001); // 1 ms step

        Assert.Equal(10.0, circuit.Nodes[1].Voltage, precision: 3);
        Assert.Equal(5.0, circuit.Nodes[2].Voltage, precision: 3);
        Assert.Equal(0.0, circuit.Nodes[0].Voltage, precision: 3);

        // Current = 10V / 2000 Ohm = 0.005 A (5 mA)
        Assert.Equal(0.005, r1.Current, precision: 4);
        Assert.Equal(0.005, r2.Current, precision: 4);
    }

    [Fact]
    public void RcStepResponse_ChargesCapacitorTowardInputVoltage()
    {
        // 10V step into 1kOhm + 1uF capacitor (time constant tau = R*C = 1 ms)
        var circuit = new Circuit("RC");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 10.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, resistance: 1000.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 2;
        circuit.AddDevice(res);

        var cap = new CapacitorDevice(3, capacitance: 1e-6);
        cap.Pins[0].NodeId = 2;
        cap.Pins[1].NodeId = 0;
        circuit.AddDevice(cap);

        var solver = new MnaSolver(circuit);

        // Initially V_cap = 0
        Assert.Equal(0.0, cap.StateVoltage);

        // Step by 1 ms (1 tau)
        double dt = 1e-4; // 0.1 ms steps
        for (int i = 0; i < 10; i++)
        {
            solver.Step(dt);
        }

        // At t = 1 tau, V_cap should be approx 10 * (1 - 1/e) = 6.32V
        double vCap = circuit.Nodes[2].Voltage;
        Assert.InRange(vCap, 6.0, 6.6);

        // Step further to 5 ms (5 tau -> fully charged > 9.8V)
        for (int i = 0; i < 40; i++)
        {
            solver.Step(dt);
        }

        Assert.InRange(circuit.Nodes[2].Voltage, 9.8, 10.0);
    }

    [Fact]
    public void PotentiometerPreset_MetersAndWiperDividedVoltage()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreatePotentiometerMeters();
        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        var am = circuit.Devices.OfType<AmmeterDevice>().First();
        var vm = circuit.Devices.OfType<VoltmeterDevice>().First();
        var pot = circuit.Devices.OfType<PotentiometerDevice>().First();

        // 10V across 10kOhm -> 1.0 mA total current
        Assert.Equal(0.001, am.Current, precision: 4);

        // 50% wiper -> 5.0 V
        Assert.Equal(5.0, vm.VoltageDrop, precision: 2);

        // Tune wiper to 75%
        pot.Wiper = 0.75;
        solver.Step(0.001);
        Assert.Equal(7.5, vm.VoltageDrop, precision: 2);
    }

    [Fact]
    public void Ohmmeter_MeasuresAccurateResistance()
    {
        var circuit = new Circuit("OhmTest");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        // Resistor under test: 4.7 kOhm between Node 1 and Node 0
        var res = new ResistorDevice(1, resistance: 4700.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        // Ohmmeter measuring across Node 1 and Node 0
        var ohm = new OhmmeterDevice(2);
        ohm.Pins[0].NodeId = 1;
        ohm.Pins[1].NodeId = 0;
        circuit.AddDevice(ohm);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        Assert.Equal(4700.0, ohm.MeasuredResistance, precision: 0);
    }

    [Fact]
    public void DroneInverterPreset_HasRealisticQuiescentCurrent()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateDroneInverter();
        var solver = new MnaSolver(circuit);

        for (int i = 0; i < 2000; i++)
        {
            solver.Step(1e-4);
        }

        var vbat = circuit.Devices.OfType<VoltageSourceDevice>().First();
        Assert.True(Math.Abs(vbat.Current) < 10.0, $"Battery current was {vbat.Current} A, expected realistic finite current");
        Assert.Equal(14.8, vbat.VoltageDrop, precision: 2);
    }

    [Fact]
    public void LedCircuit_CalculatesPhysicalForwardCurrent()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateLedCircuit();
        var solver = new MnaSolver(circuit);

        for (int i = 0; i < 50; i++)
        {
            solver.Step(1e-4);
        }

        var vs = circuit.Devices.OfType<VoltageSourceDevice>().First();
        var res = circuit.Devices.OfType<ResistorDevice>().First();
        var led = circuit.Devices.OfType<LedDevice>().First();

        // 5.0V source through closed switch, 330 Ohm resistor and Red LED (~1.98V drop)
        // I = (5.0 - 1.98) / 330 = ~9.15 mA
        Assert.InRange(led.Current, 0.007, 0.012);
        Assert.Equal(led.Current, res.Current, precision: 4);
        Assert.Equal(led.Current, vs.Current, precision: 4);
        Assert.InRange(led.VoltageDrop, 1.8, 2.2);
    }

    [Fact]
    public void Inductor_RlCircuit_ExponentialCurrentRise()
    {
        // 10V DC source -> 10 Ohm Resistor -> 10 mH Inductor -> Ground
        // tau = L / R = 10mH / 10 Ohm = 1.0 ms
        // I(t) = (V/R) * (1 - e^(-t/tau)) = 1.0 * (1 - e^(-t/tau))
        var circuit = new Circuit("RL_Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 10.0);
        vs.Pins[0].NodeId = 1; // + 10V
        vs.Pins[1].NodeId = 0; // GND
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, resistance: 10.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 2;
        circuit.AddDevice(res);

        var ind = new InductorDevice(3, inductance: 0.010) // 10 mH
        {
            SeriesResistance = 0.001 // negligible ESR
        };
        ind.Pins[0].NodeId = 2;
        ind.Pins[1].NodeId = 0;
        circuit.AddDevice(ind);

        var solver = new MnaSolver(circuit);

        // Step by 100 us x 10 = 1.0 ms (1 tau)
        double dt = 1e-4;
        for (int i = 0; i < 10; i++)
        {
            solver.Step(dt);
        }

        // At t = 1 tau, I = 1.0 * (1 - 1/e) = 0.632 A
        Assert.InRange(ind.Current, 0.58, 0.68);
        Assert.Equal(ind.Current, res.Current, precision: 3);

        // Step further to 5 ms (5 tau -> fully energized > 0.98 A)
        for (int i = 0; i < 40; i++)
        {
            solver.Step(dt);
        }

        Assert.InRange(ind.Current, 0.98, 1.01);
        Assert.InRange(ind.VoltageDrop, 0.0, 0.25);
    }

    [Fact]
    public void ZenerDiode_ReverseBreakdownRegulation()
    {
        // 12V DC source -> 1 kOhm Resistor -> 5.1V Zener Diode in reverse bias -> Ground
        // V_zener = 5.1V, I = (12 - 5.1) / 1000 = ~6.9 mA
        var circuit = new Circuit("Zener_Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 12.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, resistance: 1000.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 2;
        circuit.AddDevice(res);

        // Zener cathode at Node 2, anode at Ground (Node 0) -> Reverse biased
        var zener = new ZenerDiodeDevice(3, breakdown: 5.1, zenerR: 10.0);
        zener.Pins[0].NodeId = 0; // Anode to GND
        zener.Pins[1].NodeId = 2; // Cathode to Node 2
        circuit.AddDevice(zener);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        double vZener = circuit.Nodes[2].Voltage;
        Assert.InRange(vZener, 5.0, 5.3); // Regulates tightly at 5.1V
        Assert.InRange(res.Current, 0.0065, 0.0072);
    }

    [Fact]
    public void OpAmp_InvertingAmplifier_DeliversAccurateCurrent()
    {
        // OpAmp driving a 100 Ohm load from its output to ground
        // V+ = 0.1V, V- = 0V -> Output saturates towards Max (or clamped)
        var circuit = new Circuit("OpAmp_Load_Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 0.05); // 50 mV
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var op = new OpAmpDevice(2)
        {
            Gain = 100.0,
            MaxVoltage = 12.0,
            MinVoltage = -12.0
        };
        op.Pins[0].NodeId = 0; // V- at GND
        op.Pins[1].NodeId = 1; // V+ at 50 mV -> Target = 50mV * 100 = 5.0V
        op.Pins[2].NodeId = 2; // Output
        circuit.AddDevice(op);

        var load = new ResistorDevice(3, resistance: 99.0); // 99 + 1 (rOut) = 100 Ohm total
        load.Pins[0].NodeId = 2;
        load.Pins[1].NodeId = 0;
        circuit.AddDevice(load);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        // V_out = 5.0V * (99 / 100) = 4.95V
        // I_out = 4.95 / 99 = 0.050 A (50 mA)
        Assert.InRange(circuit.Nodes[2].Voltage, 4.90, 5.00);
        Assert.InRange(op.Current, 0.048, 0.052);
        Assert.Equal(op.Current, load.Current, precision: 3);
    }

    [Fact]
    public void CircuitWithoutGround_PrioritizesNegativeTerminalOfVoltageSourceAsGround()
    {
        // User scenario: 5V DC Source + two 10 Ohm resistors in series. No GroundDevice!
        // Middle wire was drawn first (NodeId 1).
        // Positive rail is Node 2.
        // Negative rail is Node 3.
        var circuit = new Circuit("NoGroundDivider");
        circuit.GetOrCreateNode(1); // Middle divider
        circuit.GetOrCreateNode(2); // Top (+)
        circuit.GetOrCreateNode(3); // Bottom (-)

        // 5V DC Source between Node 2 (+) and Node 3 (-)
        var vs = new VoltageSourceDevice(1, 0, 0, voltage: 5.0);
        vs.Pins[0].NodeId = 2; // Positive
        vs.Pins[1].NodeId = 3; // Negative
        circuit.AddDevice(vs);

        // Top Resistor (10 Ohm) between Node 2 and Node 1
        var r1 = new ResistorDevice(2, 0, 0, resistance: 10.0);
        r1.Pins[0].NodeId = 2;
        r1.Pins[1].NodeId = 1;
        circuit.AddDevice(r1);

        // Bottom/Right Resistor (10 Ohm) between Node 1 and Node 3
        var r2 = new ResistorDevice(3, 0, 0, resistance: 10.0);
        r2.Pins[0].NodeId = 1;
        r2.Pins[1].NodeId = 3;
        circuit.AddDevice(r2);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        // Negative rail must resolve to 0.0 V (Reference Ground)
        Assert.True(circuit.Nodes[3].IsGround);
        Assert.Equal(0.0, circuit.Nodes[3].Voltage, precision: 4);

        // Positive rail must be +5.0 V
        Assert.Equal(5.0, circuit.Nodes[2].Voltage, precision: 4);

        // Middle divider must be +2.50 V (NOT 0.0 V!)
        Assert.Equal(2.5, circuit.Nodes[1].Voltage, precision: 4);

        // Current through all components: 5V / (10 + 10) = 0.25 A (250 mA)
        Assert.Equal(0.25, vs.Current, precision: 4);
        Assert.Equal(0.25, r1.Current, precision: 4);
        Assert.Equal(0.25, r2.Current, precision: 4);
    }

    [Fact]
    public void CircuitWithoutGround_BatteryCell_PrioritizesNegativeTerminalAsGround()
    {
        var circuit = new Circuit("BatteryNoGround");
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var bat = new BatteryCellDevice(1, 0, 0)
        {
            StateOfCharge = 1.0,
            NominalSeriesResistance = 0.05
        };
        bat.Pins[0].NodeId = 1; // (+)
        bat.Pins[1].NodeId = 2; // (-)
        circuit.AddDevice(bat);

        var load = new ResistorDevice(2, 0, 0, resistance: 100.0);
        load.Pins[0].NodeId = 1;
        load.Pins[1].NodeId = 2;
        circuit.AddDevice(load);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        // Negative terminal of battery (Node 2) must be Ground (0 V)
        Assert.True(circuit.Nodes[2].IsGround);
        Assert.Equal(0.0, circuit.Nodes[2].Voltage, precision: 4);
        Assert.InRange(circuit.Nodes[1].Voltage, 4.10, 4.25);
    }
}

