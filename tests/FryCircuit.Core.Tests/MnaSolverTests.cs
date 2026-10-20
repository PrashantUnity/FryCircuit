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

    [Theory]
    [InlineData(1e-14)] // Standard 10 fA
    [InlineData(1e-10)] // 100 pA (reproduction of bug scenario)
    [InlineData(1e-7)]  // High saturation current
    public void Diode_ForwardConduction_ConvergesWithoutLimitCycleOscillation(double isat)
    {
        // 5V DC Source -> Diode -> 10.1 Ohm Resistor -> Ground
        var circuit = new Circuit("Diode_ForwardConduction");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 5.0);
        vs.Pins[0].NodeId = 1; // +
        vs.Pins[1].NodeId = 0; // GND
        circuit.AddDevice(vs);

        var diode = new DiodeDevice(2)
        {
            SaturationCurrent = isat,
            EmissionCoefficient = 1.0
        };
        diode.Pins[0].NodeId = 1; // Anode
        diode.Pins[1].NodeId = 2; // Cathode
        circuit.AddDevice(diode);

        var res = new ResistorDevice(3, resistance: 10.1);
        res.Pins[0].NodeId = 2;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        var solver = new MnaSolver(circuit);

        // Step over several time steps to ensure transient stability
        for (int i = 0; i < 20; i++)
        {
            solver.Step(1e-4);
        }

        // Positive rail must remain rock solid at 5.0 V
        Assert.Equal(5.0, circuit.Nodes[1].Voltage, precision: 3);

        // Cathode voltage must be physical (~3.5V - 4.8V depending on Is), NEVER kV range
        double vCathode = circuit.Nodes[2].Voltage;
        Assert.InRange(vCathode, 3.5, 4.8);

        // Diode forward drop should be physical (~0.2V - 1.5V)
        Assert.InRange(diode.VoltageDrop, 0.2, 1.5);

        // Currents through all components in series must balance and be in ~0.3 - 0.5 A range
        Assert.InRange(diode.Current, 0.3, 0.5);
        Assert.Equal(diode.Current, res.Current, precision: 3);
        Assert.Equal(diode.Current, vs.Current, precision: 3);
    }

    [Fact]
    public void Diode_ReverseBias_BlocksCurrent()
    {
        // 5V DC Source -> Reverse Diode (Cathode to +5V, Anode to Resistor) -> 10 Ohm Resistor -> Ground
        var circuit = new Circuit("Diode_ReverseBias");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 5.0);
        vs.Pins[0].NodeId = 1; // +
        vs.Pins[1].NodeId = 0; // GND
        circuit.AddDevice(vs);

        var diode = new DiodeDevice(2)
        {
            SaturationCurrent = 1e-14
        };
        diode.Pins[0].NodeId = 2; // Anode
        diode.Pins[1].NodeId = 1; // Cathode connected to +5V (reverse bias)
        circuit.AddDevice(diode);

        var res = new ResistorDevice(3, resistance: 10.0);
        res.Pins[0].NodeId = 2;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 10; i++)
        {
            solver.Step(1e-4);
        }

        // Leakage current should be minute (< 1 uA)
        Assert.InRange(Math.Abs(diode.Current), 0.0, 1e-6);
        Assert.InRange(Math.Abs(circuit.Nodes[2].Voltage), 0.0, 1e-4);
    }

    [Fact]
    public void Led_Overcurrent_BlowsAndIsolatesCircuit()
    {
        // 5V DC Source connected directly across Red LED without series resistor
        var circuit = new Circuit("Led_Overcurrent");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var vs = new VoltageSourceDevice(1, voltage: 5.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var led = new LedDevice(2, wavelength: 650.0) // Red LED, max 30mA
        {
            MaxForwardCurrent = 0.030
        };
        led.Pins[0].NodeId = 1; // Anode to +5V
        led.Pins[1].NodeId = 0; // Cathode to GND
        circuit.AddDevice(led);

        var solver = new MnaSolver(circuit);

        // First step: overcurrent is detected and LED burns out
        solver.Step(1e-4);
        Assert.True(led.IsBlown);
        Assert.NotNull(led.BlownReason);
        Assert.Contains("Overcurrent", led.BlownReason);

        // Next step: circuit is open, current remains 0
        solver.Step(1e-4);
        Assert.Equal(0.0, led.Current, precision: 6);

        // Reset/replace component restores operation
        led.ResetBlown();
        Assert.False(led.IsBlown);
        Assert.Null(led.BlownReason);
    }

    [Fact]
    public void Led_ReverseBreakdown_BlowsOnExcessiveReverseVoltage()
    {
        // 12V DC Source with LED reverse biased (Cathode to +12V, Anode to GND)
        var circuit = new Circuit("Led_ReverseOvervoltage");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var vs = new VoltageSourceDevice(1, voltage: 12.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var led = new LedDevice(2, wavelength: 650.0)
        {
            MaxReverseVoltage = 5.0
        };
        led.Pins[0].NodeId = 0; // Anode to GND
        led.Pins[1].NodeId = 1; // Cathode to +12V (V_drop = -12V)
        circuit.AddDevice(led);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        Assert.True(led.IsBlown);
        Assert.Contains("Reverse breakdown", led.BlownReason);
    }

    [Fact]
    public void Lamp_Overvoltage_BurnsOutFilament()
    {
        // 12V nominal Lamp driven by 24V source (exceeding MaxVoltage = 18V)
        var circuit = new Circuit("Lamp_Overvoltage");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var vs = new VoltageSourceDevice(1, voltage: 24.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var lamp = new LampDevice(2, nominalVoltage: 12.0, nominalPower: 60.0);
        lamp.Pins[0].NodeId = 1;
        lamp.Pins[1].NodeId = 0;
        circuit.AddDevice(lamp);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        Assert.True(lamp.IsBlown);
        Assert.NotNull(lamp.BlownReason);
        Assert.Contains("Filament Burnout", lamp.BlownReason);

        // Next step should have zero current
        solver.Step(1e-4);
        Assert.Equal(0.0, lamp.Current, precision: 6);

        // Resetting restores filament
        lamp.ResetBlown();
        Assert.False(lamp.IsBlown);
    }

    [Fact]
    public void Fuse_Overcurrent_BlowsAndProtectsCircuit()
    {
        // 12V source -> 1A Fuse -> 2 Ohm resistor (I would be 6A)
        var circuit = new Circuit("Fuse_Overcurrent");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, voltage: 12.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var fuse = new FuseDevice(2, currentRating: 1.0);
        fuse.Pins[0].NodeId = 1;
        fuse.Pins[1].NodeId = 2;
        circuit.AddDevice(fuse);

        var res = new ResistorDevice(3, resistance: 2.0);
        res.Pins[0].NodeId = 2;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        var solver = new MnaSolver(circuit);

        // First step detects 6A > 1A and melts fuse
        solver.Step(1e-4);
        Assert.True(fuse.IsBlown);
        Assert.NotNull(fuse.BlownReason);
        Assert.Contains("Blown", fuse.BlownReason);

        // Next step: current is zero, protecting resistor
        solver.Step(1e-4);
        Assert.Equal(0.0, fuse.Current, precision: 6);
        Assert.Equal(0.0, res.Current, precision: 6);
        Assert.Equal(0.0, circuit.Nodes[2].Voltage, precision: 4);

        // Reset fuse
        fuse.ResetBlown();
        Assert.False(fuse.IsBlown);
    }

    [Fact]
    public void Simulation_Reset_RestoresAllBlownComponentsAndVoltages()
    {
        var circuit = new Circuit("ResetTest");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var vs = new VoltageSourceDevice(1, voltage: 5.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var fuse = new FuseDevice(2, currentRating: 0.1);
        fuse.Pins[0].NodeId = 1;
        fuse.Pins[1].NodeId = 0;
        fuse.IsBlown = true;
        fuse.BlownReason = "Blown";
        circuit.AddDevice(fuse);

        var led = new LedDevice(3, wavelength: 650.0);
        led.Pins[0].NodeId = 1;
        led.Pins[1].NodeId = 0;
        led.IsBlown = true;
        led.BlownReason = "Burned out";
        circuit.AddDevice(led);

        circuit.Nodes[1].Voltage = 5.0;

        var solver = new MnaSolver(circuit);
        solver.Reset();

        Assert.Equal(0.0, solver.CurrentTime);
        Assert.Equal(0.0, circuit.Nodes[1].Voltage);
        Assert.False(fuse.IsBlown);
        Assert.Null(fuse.BlownReason);
        Assert.False(led.IsBlown);
        Assert.Null(led.BlownReason);
    }

    [Fact]
    public void OpAmp_InvertingAmplifier_ClosedLoopLinearOutput()
    {
        // Vin = 1.0V -> Rin = 10k -> Inverting Node (2)
        // Rf = 20k from Inverting Node (2) to Output Node (3)
        // Non-inverting input (Pin 1) tied to Ground (0)
        // Ideal expected Vout = - (Rf / Rin) * Vin = -2.0 V
        var circuit = new Circuit("InvertingOpAmpTest");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Vin
        circuit.GetOrCreateNode(2); // Vinv (-)
        circuit.GetOrCreateNode(3); // Vout

        var vs = new VoltageSourceDevice(1, voltage: 1.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var rIn = new ResistorDevice(2, resistance: 10000.0);
        rIn.Pins[0].NodeId = 1;
        rIn.Pins[1].NodeId = 2;
        circuit.AddDevice(rIn);

        var rF = new ResistorDevice(3, resistance: 20000.0);
        rF.Pins[0].NodeId = 2;
        rF.Pins[1].NodeId = 3;
        circuit.AddDevice(rF);

        var opAmp = new OpAmpDevice(4)
        {
            Gain = 100000.0,
            MinVoltage = -15.0,
            MaxVoltage = 15.0
        };
        opAmp.Pins[0].NodeId = 2; // Inverting (-)
        opAmp.Pins[1].NodeId = 0; // Non-inverting (+) -> GND
        opAmp.Pins[2].NodeId = 3; // Output
        circuit.AddDevice(opAmp);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        double vOut = circuit.Nodes[3].Voltage;
        double vInv = circuit.Nodes[2].Voltage;

        // Vout should be very close to -2.0V
        Assert.InRange(vOut, -2.001, -1.999);
        // Virtual ground at node 2 should be within microvolts of 0V
        Assert.InRange(vInv, -1e-4, 1e-4);
    }

    [Fact]
    public void Multiple_Ground_Symbols_Unify_To_Common_Reference()
    {
        var circuit = new Circuit("MultiGroundTest");
        // Place two separate ground devices on initially distinct nodes
        var gnd1 = new GroundDevice(1, x: 0, y: 0);
        gnd1.Pins[0].NodeId = 2;
        circuit.AddDevice(gnd1);

        var gnd2 = new GroundDevice(2, x: 10, y: 0);
        gnd2.Pins[0].NodeId = 5;
        circuit.AddDevice(gnd2);

        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(5);

        // Add 5V source between node 1 and gnd1 (node 2)
        var vs = new VoltageSourceDevice(3, voltage: 5.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 2;
        circuit.AddDevice(vs);

        // Add 100 ohm resistor between node 1 and gnd2 (node 5)
        var res = new ResistorDevice(4, resistance: 100.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 5;
        circuit.AddDevice(res);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        // Both ground pins should share the unified ground node and be at 0V
        Assert.Equal(gnd1.Pins[0].NodeId, gnd2.Pins[0].NodeId);
        int gndNodeId = gnd1.Pins[0].NodeId;
        Assert.True(circuit.Nodes[gndNodeId].IsGround);
        Assert.Equal(0.0, circuit.Nodes[gndNodeId].Voltage, precision: 6);

        // Voltage across resistor should be 5V, current = 50mA
        Assert.Equal(5.0, circuit.Nodes[1].Voltage, precision: 3);
        Assert.Equal(0.05, res.Current, precision: 4);
    }

    [Fact]
    public void NetLabel_DistantPins_Aliased_Without_Wires()
    {
        var circuit = new Circuit("NetLabelAliasingTest");
        circuit.GetOrCreateNode(0, isGround: true);

        // 5V source connected to Node 1 and GND
        var vs = new VoltageSourceDevice(1, voltage: 5.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // NetLabel "+5V" at Node 1
        var nl1 = new NetLabelDevice(2, netName: "+5V");
        nl1.Pins[0].NodeId = 1;
        circuit.AddDevice(nl1);

        // Distant Resistor on Node 10 and GND
        var res = new ResistorDevice(3, resistance: 250.0);
        res.Pins[0].NodeId = 10;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        // NetLabel "+5V" at distant Node 10
        var nl2 = new NetLabelDevice(4, netName: "+5V");
        nl2.Pins[0].NodeId = 10;
        circuit.AddDevice(nl2);

        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(10);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        // Pins of nl1 and nl2 and res.Pins[0] should now be unified to the same node
        Assert.Equal(nl1.Pins[0].NodeId, nl2.Pins[0].NodeId);
        Assert.Equal(5.0, res.VoltageDrop, precision: 3);
        Assert.Equal(0.02, res.Current, precision: 4);
    }

    [Fact]
    public void WireDeletion_GraphPartitioning_IsolatesDisconnectedNodes()
    {
        var circuit = new Circuit("WireCutTest");
        var devA = new ResistorDevice(1, resistance: 100);
        var devB = new ResistorDevice(2, resistance: 200);
        var devC = new ResistorDevice(3, resistance: 300);
        circuit.AddDevice(devA);
        circuit.AddDevice(devB);
        circuit.AddDevice(devC);

        // Wire A.Pin1 to B.Pin0
        var wire1 = circuit.ConnectPins(devA, 1, devB, 0);
        // Wire B.Pin0 to C.Pin0
        var wire2 = circuit.ConnectPins(devB, 0, devC, 0);

        int originalNode = devA.Pins[1].NodeId;
        Assert.Equal(originalNode, devB.Pins[0].NodeId);
        Assert.Equal(originalNode, devC.Pins[0].NodeId);

        // Removing wire2 disconnects C from A and B
        circuit.RemoveWire(wire2);

        // A.Pin1 and B.Pin0 remain connected together
        Assert.Equal(devA.Pins[1].NodeId, devB.Pins[0].NodeId);
        // C.Pin0 is partitioned into a new independent node
        Assert.NotEqual(devA.Pins[1].NodeId, devC.Pins[0].NodeId);
        Assert.True(circuit.Nodes.ContainsKey(devC.Pins[0].NodeId));
    }
}


