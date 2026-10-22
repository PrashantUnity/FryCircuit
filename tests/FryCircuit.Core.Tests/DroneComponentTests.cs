using System;
using System.Linq;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.IO;
using FryCircuit.Core.Models;
using FryCircuit.Core.Presets;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class DroneComponentTests
{
    [Fact]
    public void FanDevice_AcceleratesToRpmAndProducesAirflow()
    {
        var circuit = new Circuit("Cooling Fan Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 12V
        circuit.GetOrCreateNode(2); // Tach

        var vsrc = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 12.0);
        vsrc.Pins[0].NodeId = 1;
        vsrc.Pins[1].NodeId = 0;
        circuit.AddDevice(vsrc);

        var fan = new FanDevice(2, x: 5, y: 0)
        {
            RatedVoltage = 12.0,
            RatedCurrent = 0.25,
            MaxRpm = 5000.0,
            StartVoltage = 4.0,
            AirflowRatingCfm = 35.0
        };
        fan.Pins[0].NodeId = 1;
        fan.Pins[1].NodeId = 0;
        fan.Pins[2].NodeId = 2;
        circuit.AddDevice(fan);

        var solver = new MnaSolver(circuit);

        // Step simulation for 1000 steps of 1ms (1 second total)
        for (int i = 0; i < 1000; i++)
        {
            solver.Step(1e-3);
        }

        // Fan should accelerate and reach substantial operating speed
        Assert.True(fan.CurrentRpm > 1000.0, $"Expected RPM > 1000, got {fan.CurrentRpm}");
        Assert.True(fan.CurrentAirflowCfm > 5.0, $"Expected CFM > 5, got {fan.CurrentAirflowCfm}");
        Assert.True(fan.BackEmfVoltage > 0.0, "Back-EMF should be non-zero when spinning");
    }

    [Fact]
    public void FanDevice_StallsBelowStartVoltage()
    {
        var circuit = new Circuit("Fan Stall Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 2V (below 4V start threshold)

        var vsrc = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 2.0);
        vsrc.Pins[0].NodeId = 1;
        vsrc.Pins[1].NodeId = 0;
        circuit.AddDevice(vsrc);

        var fan = new FanDevice(2, x: 5, y: 0)
        {
            StartVoltage = 4.0
        };
        fan.Pins[0].NodeId = 1;
        fan.Pins[1].NodeId = 0;
        circuit.AddDevice(fan);

        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 200; i++)
        {
            solver.Step(1e-3);
        }

        Assert.Equal(0.0, fan.CurrentRpm, precision: 1);
        Assert.Equal(0.0, fan.CurrentAirflowCfm, precision: 1);
    }

    [Fact]
    public void BldcMotor_Computes3PhaseStatorImpedanceAndThrust()
    {
        var circuit = new Circuit("BLDC Motor Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Phase U
        circuit.GetOrCreateNode(2); // Phase V
        circuit.GetOrCreateNode(3); // Phase W

        var motor = new BldcMotorDevice(1, x: 5, y: 5)
        {
            Kv = 2300.0,
            PhaseResistance = 0.065,
            PropellerDiameterInches = 5.0,
            AngularVelocityRadS = 2000.0 // ~19,100 RPM
        };
        motor.Pins[0].NodeId = 1;
        motor.Pins[1].NodeId = 2;
        motor.Pins[2].NodeId = 3;
        circuit.AddDevice(motor);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        Assert.True(motor.CurrentRpm > 15000.0, $"Expected RPM > 15000, got {motor.CurrentRpm}");
        Assert.True(motor.ThrustGrams > 200.0, $"Expected thrust > 200g at 19k RPM, got {motor.ThrustGrams}");
    }

    [Fact]
    public void EscDevice_ConvertsPwmToInverterCommutation()
    {
        var circuit = new Circuit("ESC Drive Test");
        circuit.GetOrCreateNode(0, isGround: true); // GND
        circuit.GetOrCreateNode(1);                 // 14.8V VBAT
        circuit.GetOrCreateNode(2);                 // 2.5V PWM Throttle
        circuit.GetOrCreateNode(3);                 // U
        circuit.GetOrCreateNode(4);                 // V
        circuit.GetOrCreateNode(5);                 // W

        var vbat = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 14.8);
        vbat.Pins[0].NodeId = 1;
        vbat.Pins[1].NodeId = 0;
        circuit.AddDevice(vbat);

        var vpwm = new VoltageSourceDevice(2, x: 0, y: 5, voltage: 2.5);
        vpwm.Pins[0].NodeId = 2;
        vpwm.Pins[1].NodeId = 0;
        circuit.AddDevice(vpwm);

        var esc = new EscDevice(3, x: 5, y: 5)
        {
            ContinuousCurrentRating = 45.0
        };
        esc.Pins[0].NodeId = 1; // VBAT
        esc.Pins[1].NodeId = 0; // GND
        esc.Pins[2].NodeId = 2; // PWM
        esc.Pins[3].NodeId = 3; // U
        esc.Pins[4].NodeId = 4; // V
        esc.Pins[5].NodeId = 5; // W
        circuit.AddDevice(esc);

        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 10; i++)
        {
            solver.Step(1e-3);
        }

        Assert.True(esc.EffectiveThrottle > 0.5, $"Expected throttle > 50%, got {esc.EffectiveThrottle}");
        Assert.True(esc.Current > 0.0, "ESC should draw current when operating");
    }

    [Fact]
    public void BecDevice_Regulates5VWithHighEfficiency()
    {
        var circuit = new Circuit("BEC Regulator Test");
        circuit.GetOrCreateNode(0, isGround: true); // GND
        circuit.GetOrCreateNode(1);                 // 14.8V LiPo Bus
        circuit.GetOrCreateNode(2);                 // 5.0V Regulated Out

        var vbat = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 14.8);
        vbat.Pins[0].NodeId = 1;
        vbat.Pins[1].NodeId = 0;
        circuit.AddDevice(vbat);

        var bec = new BecDevice(2, x: 5, y: 0)
        {
            OutputVoltage = 5.0,
            MaxOutputCurrent = 3.0,
            Efficiency = 0.90
        };
        bec.Pins[0].NodeId = 1;
        bec.Pins[1].NodeId = 0;
        bec.Pins[2].NodeId = 2;
        circuit.AddDevice(bec);

        // 10 Ω load resistor on 5V rail (draws 0.5A)
        var rLoad = new ResistorDevice(3, x: 10, y: 0, resistance: 10.0);
        rLoad.Pins[0].NodeId = 2;
        rLoad.Pins[1].NodeId = 0;
        circuit.AddDevice(rLoad);

        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 10; i++)
        {
            solver.Step(1e-4);
        }

        // Regulated 5V output verification
        double vOut = circuit.Nodes[2].Voltage;
        Assert.InRange(vOut, 4.85, 5.05);

        // Load current verification
        Assert.InRange(bec.LoadCurrent, 0.45, 0.55);

        // Power balance: Pin = Pout / 0.90 -> Input current ~ 0.5A * 5V / (0.9 * 14.8V) ~ 0.187A
        Assert.True(bec.Current < bec.LoadCurrent, "Switching buck converter input current must be less than load current");
    }

    [Fact]
    public void PiezoBuzzer_ResonatesAndCalculatesDecibels()
    {
        var circuit = new Circuit("Piezo Buzzer Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 5V

        var vsrc = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 5.0);
        vsrc.Pins[0].NodeId = 1;
        vsrc.Pins[1].NodeId = 0;
        circuit.AddDevice(vsrc);

        var buzz = new PiezoBuzzerDevice(2, x: 5, y: 0)
        {
            NominalVoltage = 5.0,
            RatedDecibelsSpl = 85.0,
            MinOperateVoltage = 2.5
        };
        buzz.Pins[0].NodeId = 1;
        buzz.Pins[1].NodeId = 0;
        circuit.AddDevice(buzz);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-4);

        Assert.True(buzz.IsSounding, "Buzzer should be sounding at 5V");
        Assert.InRange(buzz.ActualDecibelsSpl, 84.0, 86.0);
    }

    [Fact]
    public void TvsDiode_ClampsHighVoltageFlybackSpikes()
    {
        var circuit = new Circuit("TVS Clamping Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 30V Spiking Rail

        // High inductive surge voltage source (30V)
        var vSpike = new VoltageSourceDevice(1, x: 0, y: 0, voltage: 30.0);
        vSpike.Pins[0].NodeId = 1;
        vSpike.Pins[1].NodeId = 0;
        circuit.AddDevice(vSpike);

        // TVS Diode with 18V breakdown
        var tvs = new TvsDiodeDevice(2, x: 5, y: 0, breakdown: 18.0)
        {
            ClampingResistance = 0.05
        };
        tvs.Pins[0].NodeId = 0; // Anode to GND
        tvs.Pins[1].NodeId = 1; // Cathode to Spike Rail
        circuit.AddDevice(tvs);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-5);

        // TVS should enter clamping mode and conduct reverse avalanche current
        Assert.True(tvs.IsClamping, "TVS should enter active clamping mode at 30V");
        Assert.True(tvs.Current < 0.0, "Reverse avalanche current flows from cathode to anode");
    }

    [Fact]
    public void DroneQuadcopterPreset_SolvesCleanlyAndGeneratesBom()
    {
        var circuit = CircuitPresets.CreateDronePropulsionAndAvionics();
        Assert.Equal("Drone Quadcopter Propulsion & Avionics", circuit.Name);
        Assert.True(circuit.Devices.Count >= 9, $"Expected >= 9 devices, got {circuit.Devices.Count}");

        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 50; i++)
        {
            solver.Step(1e-4);
        }

        // Verify key nodes are energized
        Assert.InRange(circuit.Nodes[1].Voltage, 14.0, 16.0); // LiPo raw bus
        Assert.InRange(circuit.Nodes[3].Voltage, 4.8, 5.2);   // BEC 5V regulated

        // Verify Bill of Materials (BOM) export
        string bomMarkdown = BomEngine.GenerateMarkdownBom(circuit);
        Assert.Contains("DC Brushless Cooling Fan", bomMarkdown);
        Assert.Contains("3-Phase BLDC Drone Outrunner", bomMarkdown);
        Assert.Contains("Electronic Speed Controller (ESC)", bomMarkdown);
        Assert.Contains("Step-Down DC-DC BEC Regulator", bomMarkdown);
        Assert.Contains("Piezoelectric Acoustic Buzzer", bomMarkdown);
        Assert.Contains("Transient Voltage Suppressor (TVS)", bomMarkdown);

        // Verify SPICE netlist export
        string spice = SpiceNetlistEngine.ExportSpice(circuit);
        Assert.Contains("R_FAN_COIL", spice);
        Assert.Contains("R_MTR_UV", spice);
        Assert.Contains("R_ESC_SUPPLY", spice);
        Assert.Contains("V_BEC_OUT", spice);
        Assert.Contains("R_BUZZ", spice);
        Assert.Contains("D_TVS", spice);
    }

    [Fact]
    public void DeviceRegistry_ResolvesAllDroneDescriptorsAndAliases()
    {
        DeviceRegistry.EnsureInitialized();

        Assert.NotNull(DeviceRegistry.TryGet("fan"));
        Assert.NotNull(DeviceRegistry.TryGet("dcfan"));
        Assert.NotNull(DeviceRegistry.TryGet("cooler"));

        Assert.NotNull(DeviceRegistry.TryGet("bldc"));
        Assert.NotNull(DeviceRegistry.TryGet("bldcmotor"));
        Assert.NotNull(DeviceRegistry.TryGet("motor"));

        Assert.NotNull(DeviceRegistry.TryGet("esc"));
        Assert.NotNull(DeviceRegistry.TryGet("bldcesc"));

        Assert.NotNull(DeviceRegistry.TryGet("bec"));
        Assert.NotNull(DeviceRegistry.TryGet("buck"));

        Assert.NotNull(DeviceRegistry.TryGet("buzzer"));
        Assert.NotNull(DeviceRegistry.TryGet("piezo"));

        Assert.NotNull(DeviceRegistry.TryGet("tvs"));
        Assert.NotNull(DeviceRegistry.TryGet("tvsdiode"));
    }
}
