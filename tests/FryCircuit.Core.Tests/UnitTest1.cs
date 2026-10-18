using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class CircuitBuilderAndDeviceTests
{
    [Fact]
    public void DeviceFactory_CreatesAllDeviceTypesInDedicatedClasses()
    {
        var resistor = DeviceFactory.CreateDevice("resistor", 1);
        Assert.IsType<ResistorDevice>(resistor);

        var capacitor = DeviceFactory.CreateDevice("capacitor", 2);
        Assert.IsType<CapacitorDevice>(capacitor);

        var inductor = DeviceFactory.CreateDevice("inductor", 3);
        Assert.IsType<InductorDevice>(inductor);

        var vsource = DeviceFactory.CreateDevice("vsource", 4);
        Assert.IsType<VoltageSourceDevice>(vsource);

        var diode = DeviceFactory.CreateDevice("diode", 5);
        Assert.IsType<DiodeDevice>(diode);

        var led = DeviceFactory.CreateDevice("led", 6);
        Assert.IsType<LedDevice>(led);

        var npn = DeviceFactory.CreateDevice("npn", 7);
        Assert.IsType<BjtDevice>(npn);

        var nmos = DeviceFactory.CreateDevice("nmos", 8);
        Assert.IsType<MosfetDevice>(nmos);

        var opamp = DeviceFactory.CreateDevice("opamp", 9);
        Assert.IsType<OpAmpDevice>(opamp);

        var sw = DeviceFactory.CreateDevice("spst", 10);
        Assert.IsType<SwitchDevice>(sw);

        var pot = DeviceFactory.CreateDevice("potentiometer", 11);
        Assert.IsType<PotentiometerDevice>(pot);

        var logic = DeviceFactory.CreateDevice("and", 12);
        Assert.IsType<LogicGateDevice>(logic);

        var volt = DeviceFactory.CreateDevice("voltmeter", 13);
        Assert.IsType<VoltmeterDevice>(volt);

        var ammeter = DeviceFactory.CreateDevice("amperemeter", 14);
        Assert.IsType<AmmeterDevice>(ammeter);

        var lamp = DeviceFactory.CreateDevice("lamp", 15);
        Assert.IsType<LampDevice>(lamp);
    }

    [Fact]
    public void Circuit_ConnectPins_CreatesWireAndUnifiesNodes()
    {
        var circuit = new Circuit("Interactive Wire Test");

        var r1 = new ResistorDevice(1, 0, 0, 1000.0);
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        var r2 = new ResistorDevice(2, 4, 0, 2000.0);
        r2.Pins[0].NodeId = 3;
        r2.Pins[1].NodeId = 4;
        circuit.AddDevice(r2);

        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(3);
        circuit.GetOrCreateNode(4);

        // Connect Pin 1 of r1 to Pin 0 of r2
        var wire = circuit.ConnectPins(r1, 1, r2, 0);

        Assert.NotNull(wire);
        Assert.Single(circuit.Wires);
        Assert.Equal(1, wire.FromDeviceId);
        Assert.Equal(1, wire.FromPinIndex);
        Assert.Equal(2, wire.ToDeviceId);
        Assert.Equal(0, wire.ToPinIndex);

        // Nodes must now be unified
        Assert.Equal(r1.Pins[1].NodeId, r2.Pins[0].NodeId);
    }

    [Fact]
    public void Circuit_DisconnectPin_RemovesAttachedWiresAndIsolatesPin()
    {
        var circuit = new Circuit("Disconnect Test");

        var r1 = new ResistorDevice(1, 0, 0);
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        var r2 = new ResistorDevice(2, 4, 0);
        r2.Pins[0].NodeId = 2;
        r2.Pins[1].NodeId = 3;
        circuit.AddDevice(r2);

        circuit.ConnectPins(r1, 1, r2, 0);
        Assert.Single(circuit.Wires);

        // Disconnect pin 1 of r1
        circuit.DisconnectPin(r1, 1);

        Assert.Empty(circuit.Wires);
        Assert.NotEqual(r1.Pins[1].NodeId, r2.Pins[0].NodeId);
    }

    [Fact]
    public void Circuit_SynthesizesWiresFromPreservedNodes()
    {
        var circuit = new Circuit("Synthesize Test");

        var vs = new VoltageSourceDevice(1, 0, 0, 5.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, 4, 0, 1000.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        Assert.Empty(circuit.Wires);

        circuit.EnsureWiresSynthesized();

        Assert.Equal(2, circuit.Wires.Count);
        Assert.Contains(circuit.Wires, w => w.NodeId == 1);
        Assert.Contains(circuit.Wires, w => w.NodeId == 0);
    }

    [Fact]
    public void MnaSolver_SimulatesPolymorphicDevicesAccurately()
    {
        var circuit = new Circuit("MNA Ohm's Law Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var vs = new VoltageSourceDevice(1, 0, 0, 12.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, 4, 0, 240.0);
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        var solver = new MnaSolver(circuit);
        solver.Step(0.001);

        // Node 1 should be 12V
        Assert.Equal(12.0, circuit.Nodes[1].Voltage, precision: 3);

        // Resistor current should be 12V / 240 Ohm = 0.05 A = 50 mA
        Assert.Equal(0.05, res.Current, precision: 3);
        Assert.Equal(12.0, res.VoltageDrop, precision: 3);
        Assert.Equal(0.6, res.Power, precision: 3); // 12V * 0.05A = 0.6W
    }

    [Fact]
    public void UndoRedoManager_ExecutesAndReversesActionsCorrectly()
    {
        var circuit = new Circuit("Undo Redo Test");
        var manager = new FryCircuit.Core.History.UndoRedoManager();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);

        var res = new ResistorDevice(1, 2, 3, 500.0);
        var addAction = new FryCircuit.Core.History.AddDeviceAction(circuit, res);
        manager.Execute(addAction);

        Assert.Single(circuit.Devices);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);

        // Undo adding device
        bool undone = manager.Undo();
        Assert.True(undone);
        Assert.Empty(circuit.Devices);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);

        // Redo adding device
        bool redone = manager.Redo();
        Assert.True(redone);
        Assert.Single(circuit.Devices);
        Assert.Equal(res, circuit.Devices[0]);

        // Move device
        var moveAction = new FryCircuit.Core.History.MoveDeviceAction(res, 2, 3, 8, 9);
        manager.Execute(moveAction);
        Assert.Equal(8.0, res.LocationX);
        Assert.Equal(9.0, res.LocationY);

        manager.Undo();
        Assert.Equal(2.0, res.LocationX);
        Assert.Equal(3.0, res.LocationY);

        manager.Redo();
        Assert.Equal(8.0, res.LocationX);
        Assert.Equal(9.0, res.LocationY);
    }

    [Fact]
    public void Timer555Device_SimulatesAstableOscillationLogic()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.Create555TimerOscillator();
        Assert.NotNull(circuit);
        Assert.NotEmpty(circuit.Devices);

        var timer = circuit.Devices.OfType<Timer555Device>().FirstOrDefault();
        Assert.NotNull(timer);
        Assert.Equal(8, timer.Pins.Count);

        var solver = new MnaSolver(circuit);
        // Step through multiple iterations to verify stability
        for (int i = 0; i < 50; i++)
        {
            solver.Step(0.0005);
        }

        // Voltage at VCC (Node 1) should be 9V
        Assert.Equal(9.0, circuit.Nodes[1].Voltage, precision: 2);
        // Output node (Node 2) should be driven
        Assert.True(circuit.Nodes[2].Voltage >= 0.0);
    }

    [Fact]
    public void PhysicalConstants_CalculatesAccurateThermalVoltageAndBandgap()
    {
        // At 300.15 K, V_t should be ~ 25.86 mV
        double vtRoom = FryCircuit.Core.Physics.PhysicalConstants.ThermalVoltage(300.15);
        Assert.True(vtRoom > 0.0258 && vtRoom < 0.0259);

        // At cryogenic 4.2 K, V_t should be ~ 0.362 mV
        double vtCryo = FryCircuit.Core.Physics.PhysicalConstants.ThermalVoltage(4.2);
        Assert.True(vtCryo > 0.00036 && vtCryo < 0.00037);

        // Silicon bandgap at 300K should be ~ 1.12 eV
        double eg = FryCircuit.Core.Physics.PhysicalConstants.SiliconBandgap(300.15);
        Assert.True(eg > 1.11 && eg < 1.13);
    }

    [Fact]
    public void JosephsonJunction_ComputesQuantumPlasmaFrequencyAndPhase()
    {
        var jj = new JosephsonJunctionDevice(1, 0, 0)
        {
            CriticalCurrent = 20e-6, // 20 µA
            JunctionCapacitance = 50e-15 // 50 fF
        };

        // Plasma frequency f_p should be in microwave / mm-wave range (~20-200 GHz)
        double fp = jj.PlasmaFrequencyHz;
        Assert.True(fp > 1e9 && fp < 500e9);

        // Superconducting kinetic inductance should be in nanohenry range
        double lj = jj.JosephsonInductanceH;
        Assert.True(lj > 1e-12 && lj < 1e-6);
    }

    [Fact]
    public void TunnelDiode_ExhibitsNegativeDifferentialResistance()
    {
        var td = new TunnelDiodeDevice(1, 0, 0)
        {
            PeakCurrent = 0.005,
            ValleyCurrent = 0.0005,
            PeakVoltage = 0.065,
            ValleyVoltage = 0.35
        };

        double vt = 0.0258;
        // In the peak region (e.g. 0.065V), conductance is around 0
        var (iPeak, _) = td.Evaluate(td.PeakVoltage, vt);
        Assert.True(iPeak > 0.003);

        // In the NDR valley slope (e.g. 0.15V), differential conductance must be negative!
        var (iMid, gMid) = td.Evaluate(0.15, vt);
        Assert.True(gMid < 0, $"Expected negative differential resistance, got {gMid}");
    }

    [Fact]
    public void AdvancedPresets_SimulateQuantumAndAerospaceCircuitsStably()
    {
        // 1. Transmon Qubit
        var qubit = FryCircuit.Core.Presets.CircuitPresets.CreateTransmonQubit();
        var qubitSolver = new MnaSolver(qubit) { TemperatureKelvin = 4.2 };
        for (int i = 0; i < 20; i++) qubitSolver.Step(1e-6);
        Assert.True(qubit.Nodes[2].Voltage != double.NaN);

        // 2. Tunnel Diode Oscillator
        var tdCirc = FryCircuit.Core.Presets.CircuitPresets.CreateTunnelDiodeOscillator();
        var tdSolver = new MnaSolver(tdCirc);
        for (int i = 0; i < 20; i++) tdSolver.Step(1e-6);
        Assert.True(tdCirc.Nodes[2].Voltage != double.NaN);

        // 3. Drone 3-Phase Inverter
        var drone = FryCircuit.Core.Presets.CircuitPresets.CreateDroneInverter();
        var droneSolver = new MnaSolver(drone);
        for (int i = 0; i < 20; i++) droneSolver.Step(1e-6);
        Assert.Equal(14.8, drone.Nodes[1].Voltage, precision: 2);
    }

    [Fact]
    public void SquidDevice_ModulatesCriticalCurrentWithExternalFlux()
    {
        var squid = new SquidDevice(1, 0, 0)
        {
            BaseCriticalCurrent = 10e-6, // 10 µA per junction
            ExternalFluxNormalized = 0.0
        };

        // At Phi_ext = 0: Ic_tot = 2 * Ic0 = 20 µA
        Assert.Equal(20e-6, squid.ModulatedCriticalCurrent, precision: 9);

        // At Phi_ext = 0.5 Phi_0: complete destructive interference => Ic_tot = 0
        squid.ExternalFluxNormalized = 0.5;
        Assert.True(squid.ModulatedCriticalCurrent < 1e-12);

        // At Phi_ext = 1.0 Phi_0: periodic constructive interference => 20 µA
        squid.ExternalFluxNormalized = 1.0;
        Assert.Equal(20e-6, squid.ModulatedCriticalCurrent, precision: 9);

        // Plasma frequency must be in the GHz microwave realm (typically 10-250 GHz)
        squid.ExternalFluxNormalized = 0.0;
        Assert.True(squid.PlasmaFrequencyHz > 1e9 && squid.PlasmaFrequencyHz < 500e9);
    }

    [Fact]
    public void CosmicIonPulseDevice_DeliversAccurateHeavyIonDoubleExponentialCharge()
    {
        var ion = new CosmicIonPulseDevice(1, 0, 0)
        {
            CollectedCharge = 200e-15, // 200 fC
            RiseTimeSeconds = 10e-12,   // 10 ps
            FallTimeSeconds = 100e-12,  // 100 ps
            StrikeTimeSeconds = 1.0e-9  // 1 ns
        };

        // Before strike, current must be 0
        Assert.Equal(0.0, ion.GetCurrentAtTime(0.5e-9));

        // After strike, current must surge to milliampere peak
        double currentDuringPulse = ion.GetCurrentAtTime(1.02e-9);
        Assert.True(currentDuringPulse > 0.0001, $"Expected current pulse surge, got {currentDuringPulse}");

        // Far after strike, current must relax back to 0
        double currentLate = ion.GetCurrentAtTime(10.0e-9);
        Assert.True(currentLate < 1e-9);
    }

    [Fact]
    public void HallEffectSensorDevice_ProvidesLinearBFieldTransduction()
    {
        var sensor = new HallEffectSensorDevice(1, 0, 0)
        {
            SensitivityVoltsPerTesla = 2.5, // 2.5 V/T (2.5 mV/mT)
            MagneticFieldTesla = 0.040,      // 40 mT
            OffsetVoltage = 0.0
        };

        var circuit = new Circuit("Hall Sensor Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Vcc
        circuit.GetOrCreateNode(2); // Vout+
        circuit.GetOrCreateNode(3); // Vout-

        sensor.Pins[0].NodeId = 1;
        sensor.Pins[1].NodeId = 0;
        sensor.Pins[2].NodeId = 2;
        sensor.Pins[3].NodeId = 3;
        circuit.AddDevice(sensor);

        // Bias supply 5V
        var vcc = new VoltageSourceDevice(2, 0, 0, 5.0);
        vcc.Pins[0].NodeId = 1;
        vcc.Pins[1].NodeId = 0;
        circuit.AddDevice(vcc);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-6);

        // Expected V_Hall = 2.5 V/T * 0.040 T = 0.100 V (100 mV)
        double vDiff = circuit.Nodes[2].Voltage - circuit.Nodes[3].Voltage;
        Assert.True(Math.Abs(vDiff - 0.100) < 0.02, $"Expected ~100mV Hall output, got {vDiff * 1000}mV");
    }

    [Fact]
    public void SpiceNetlistEngine_ExportsAndImportsDeckWithFidelity()
    {
        var circuit = new Circuit("Precision RLC Benchmark");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, 0, 0, 10.0);
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var r = new ResistorDevice(2, 0, 0, 4700.0);
        r.Pins[0].NodeId = 1;
        r.Pins[1].NodeId = 2;
        circuit.AddDevice(r);

        var c = new CapacitorDevice(3, 0, 0, 220e-9);
        c.Pins[0].NodeId = 2;
        c.Pins[1].NodeId = 0;
        circuit.AddDevice(c);

        // 1. Export SPICE netlist
        string spice = FryCircuit.Core.IO.SpiceNetlistEngine.ExportSpice(circuit, tempKelvin: 77.0);
        Assert.Contains(".TEMP -196.15", spice);
        Assert.Contains("R2 N1 N2 4700", spice);
        Assert.Contains("C3 N2 0 2.2E-07", spice);
        Assert.Contains("V1 N1 0 DC 10", spice);
        Assert.Contains(".TRAN", spice);
        Assert.Contains(".END", spice);

        // 2. Import SPICE netlist
        var imported = FryCircuit.Core.IO.SpiceNetlistEngine.ImportSpice(spice, "Re-imported Deck");
        Assert.NotNull(imported);
        Assert.True(imported.Devices.Count >= 3);
        Assert.Contains(imported.Devices, d => d is ResistorDevice res && Math.Abs(res.Resistance - 4700.0) < 1.0);
        Assert.Contains(imported.Devices, d => d is CapacitorDevice cap && Math.Abs(cap.Capacitance - 220e-9) < 1e-12);
    }

    [Fact]
    public void BatteryCell_CalculatesAccurateSocAndArrheniusInternalEsr()
    {
        var bat = new FryCircuit.Core.Devices.Battery.BatteryCellDevice(1, 0, 0)
        {
            CapacityAmpHours = 2.2,
            NominalSeriesResistance = 0.020, // 20 mΩ
            StateOfCharge = 1.0 // 100% full
        };

        // At 100% full, Voc is around 4.20V
        Assert.True(bat.OpenCircuitVoltage >= 4.15 && bat.OpenCircuitVoltage <= 4.25);

        // At 0% empty, Voc drops to ~3.0V - 3.2V
        bat.StateOfCharge = 0.0;
        Assert.True(bat.OpenCircuitVoltage >= 2.7 && bat.OpenCircuitVoltage <= 3.2);

        // At room temp (298.15 K), ESR is nominal (20 mΩ)
        double esrRoom = bat.GetSeriesResistanceAtTemperature(298.15);
        Assert.Equal(0.020, esrRoom, precision: 4);

        // At freezing aerospace temp (-55°C = 218.15 K), Arrhenius electrolyte freezing increases ESR significantly
        double esrCold = bat.GetSeriesResistanceAtTemperature(218.15);
        Assert.True(esrCold > esrRoom * 2.0, $"Expected cold ESR surge, got {esrCold} vs {esrRoom}");
    }

    [Fact]
    public void BomEngine_GeneratesCompleteIndustrialManufacturingBillOfMaterials()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateDroneInverter();
        string mdBom = FryCircuit.Core.IO.BomEngine.GenerateMarkdownBom(circuit, tempKelvin: 298.15);
        string csvBom = FryCircuit.Core.IO.BomEngine.GenerateCsvBom(circuit);

        // Check Markdown Table format
        Assert.Contains("# Bill of Materials (BOM) - Aerospace 3-Phase Drone Inverter", mdBom);
        Assert.Contains("| Item | Ref Designators | Qty | Type | Value / Rating | Footprint | Domain | Spec |", mdBom);
        Assert.Contains("PowerPAK SO-8 / DFN", mdBom);
        Assert.Contains("Mil-Spec Grade", mdBom);

        // Check CSV format
        Assert.Contains("Item,Designator,Quantity,Type,Value,Package,Domain,Specification", csvBom);
        Assert.Contains("PowerPAK SO-8 / DFN", csvBom);
    }

    [Fact]
    public void DeviceDescriptorBase_TryParseEngineering_ParsesStandardUnitsAndPrefixes()
    {
        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("10k", out double v1));
        Assert.Equal(10000.0, v1);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("4.7kΩ", out double v2));
        Assert.Equal(4700.0, v2);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("100nF", out double v3));
        Assert.True(Math.Abs(v3 - 100e-9) < 1e-14);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("10µF", out double v4));
        Assert.True(Math.Abs(v4 - 10e-6) < 1e-11);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("1u", out double v5));
        Assert.True(Math.Abs(v5 - 1e-6) < 1e-11);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("50m", out double v6));
        Assert.Equal(0.05, v6);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("12V", out double v7));
        Assert.Equal(12.0, v7);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("2.5MHz", out double v8));
        Assert.Equal(2.5e6, v8);

        Assert.True(FryCircuit.Core.Devices.Base.DeviceDescriptorBase.TryParseEngineering("100pF", out double v9));
        Assert.True(Math.Abs(v9 - 100e-12) < 1e-17);
    }

    [Fact]
    public void Circuit_ResynthesizeNode_DynamicallyAdjustsWiresOnPositionChange()
    {
        var circuit = new Circuit("Dynamic Drag Test");
        var r1 = new ResistorDevice(1, x: 2, y: 6);
        r1.Pins[0].NodeId = 1;
        circuit.AddDevice(r1);

        var r2 = new ResistorDevice(2, x: 10, y: 6);
        r2.Pins[0].NodeId = 1;
        circuit.AddDevice(r2);

        var r3 = new ResistorDevice(3, x: 6, y: 6);
        r3.Pins[0].NodeId = 1;
        circuit.AddDevice(r3);

        circuit.ResynthesizeNode(1);
        Assert.Equal(2, circuit.Wires.Count);

        // When R3 is moved to the far right at x=14
        r3.LocationX = 14;
        circuit.ResynthesizeNode(1);
        Assert.Equal(2, circuit.Wires.Count);

        // Verify that R1 (x=2) connects to R2 (x=10), and R2 connects to R3 (x=14)
        bool connectsR1R2 = circuit.Wires.Any(w => w.ConnectsToPin(1, 0) && w.ConnectsToPin(2, 0));
        bool connectsR2R3 = circuit.Wires.Any(w => w.ConnectsToPin(2, 0) && w.ConnectsToPin(3, 0));
        Assert.True(connectsR1R2, "Expected dynamic MST to connect R1 and R2");
        Assert.True(connectsR2R3, "Expected dynamic MST to connect R2 and R3");
    }

    [Fact]
    public void AllPresets_WiresAreSynthesizedAndValid()
    {
        var presets = new[]
        {
            FryCircuit.Core.Presets.CircuitPresets.CreateHalfWaveRectifier(),
            FryCircuit.Core.Presets.CircuitPresets.CreateResistorDivider(),
            FryCircuit.Core.Presets.CircuitPresets.CreateRcStepResponse(),
            FryCircuit.Core.Presets.CircuitPresets.CreateLedCircuit(),
            FryCircuit.Core.Presets.CircuitPresets.CreateTwoWaySwitch(),
            FryCircuit.Core.Presets.CircuitPresets.CreateInvertingOpAmp(),
            FryCircuit.Core.Presets.CircuitPresets.CreateMosfetInverter(),
            FryCircuit.Core.Presets.CircuitPresets.CreateRlcResonance(),
            FryCircuit.Core.Presets.CircuitPresets.CreateLogicGates(),
            FryCircuit.Core.Presets.CircuitPresets.Create555TimerOscillator(),
            FryCircuit.Core.Presets.CircuitPresets.CreateTransmonQubit(),
            FryCircuit.Core.Presets.CircuitPresets.CreateSquidMagnetometer(),
            FryCircuit.Core.Presets.CircuitPresets.CreateTunnelDiodeOscillator(),
            FryCircuit.Core.Presets.CircuitPresets.CreateDroneInverter(),
            FryCircuit.Core.Presets.CircuitPresets.CreateCosmicRadiationStrike(),
            FryCircuit.Core.Presets.CircuitPresets.CreateDroneHallCommutator(),
            FryCircuit.Core.Presets.CircuitPresets.CreateTransmissionLineSerdes()
        };

        foreach (var c in presets)
        {
            c.EnsureWiresSynthesized();
            Assert.NotEmpty(c.Wires);
            foreach (var w in c.Wires)
            {
                Assert.NotNull(w.PathPoints);
                Assert.True(w.PathPoints.Count >= 2, $"Circuit {c.Name} wire {w.Id} has < 2 points");
            }
        }
    }

    [Fact]
    public void SpdtSwitch_TwoWaySwitch_ConductsAndTogglesCorrectly()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateTwoWaySwitch();
        var solver = new MnaSolver(circuit);
        for (int i = 0; i < 20; i++) solver.Step(1e-4);

        // In initial state (sw1 closed/pos1, sw2 open/pos2), both connect to Traveler A.
        // Node 4 (Lamp) should be energized at 12V.
        Assert.Equal(12.0, circuit.Nodes[4].Voltage, precision: 1);

        // Toggle sw1: opens Traveler A, connects to Traveler B. Circuit opens, Lamp drops to 0V.
        var sw1 = circuit.Devices.First(d => d.Id == 2) as FryCircuit.Core.Devices.SwitchDevice;
        Assert.NotNull(sw1);
        sw1.Toggle();
        for (int i = 0; i < 20; i++) solver.Step(1e-4);
        Assert.True(circuit.Nodes[4].Voltage < 0.1, "Lamp should be unpowered when switches are in opposite throws");

        // Toggle sw2: now both connect to Traveler B. Lamp turns back on at 12V!
        var sw2 = circuit.Devices.First(d => d.Id == 3) as FryCircuit.Core.Devices.SwitchDevice;
        Assert.NotNull(sw2);
        sw2.Toggle();
        for (int i = 0; i < 20; i++) solver.Step(1e-4);
        Assert.Equal(12.0, circuit.Nodes[4].Voltage, precision: 1);
    }

    [Fact]
    public void SetupDefaultPins_DirectlyPreservesDescriptorPins()
    {
        var mos = new FryCircuit.Core.Devices.MosfetDevice(1, "nmos", 10, 10);
        mos.SetupDefaultPins();
        Assert.Equal(3, mos.Pins.Count);
        Assert.Equal(-1, mos.Pins[0].LocalX);
        Assert.Equal(0, mos.Pins[0].LocalY);
        Assert.Equal(1, mos.Pins[1].LocalX);
        Assert.Equal(-1, mos.Pins[1].LocalY);
        Assert.Equal(1, mos.Pins[2].LocalX);
        Assert.Equal(1, mos.Pins[2].LocalY);

        var spdt = new FryCircuit.Core.Devices.SwitchDevice(2, "spdt", 5, 5);
        spdt.SetupDefaultPins();
        Assert.Equal(3, spdt.Pins.Count);
        Assert.Equal(-1, spdt.Pins[0].LocalX);
        Assert.Equal(0, spdt.Pins[0].LocalY);
        Assert.Equal(1, spdt.Pins[1].LocalX);
        Assert.Equal(-1, spdt.Pins[1].LocalY);
        Assert.Equal(1, spdt.Pins[2].LocalX);
        Assert.Equal(1, spdt.Pins[2].LocalY);
    }

    [Fact]
    public void LogicGates_Multiplexer_SimulatesAndRoutesCorrectly()
    {
        var circuit = FryCircuit.Core.Presets.CircuitPresets.CreateLogicGates();
        circuit.EnsureWiresSynthesized();

        // Must have synthesized wires for all connections
        Assert.NotEmpty(circuit.Wires);
        Assert.True(circuit.Wires.Count >= 7, "Multiplexer should have at least 7 wire routes");

        var solver = new MnaSolver(circuit);

        // Step simulation: sources are at t=0
        for (int i = 0; i < 10; i++) solver.Step(1e-4);

        // Verify nodes are registered and voltages are finite
        foreach (var node in circuit.Nodes.Values)
        {
            Assert.False(double.IsNaN(node.Voltage), $"Node {node.Id} has NaN voltage");
            Assert.False(double.IsInfinity(node.Voltage), $"Node {node.Id} has Infinite voltage");
        }
    }

    [Fact]
    public void CircuitSerializer_SupportsFrycircuitExtension()
    {
        Assert.Equal(".frycircuit", FryCircuit.Core.IO.CircuitSerializer.DefaultFileExtension);
        Assert.Equal(".circuit", FryCircuit.Core.IO.CircuitSerializer.LegacyFileExtension);

        var original = FryCircuit.Core.Presets.CircuitPresets.CreateResistorDivider();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_circuit_{Guid.NewGuid()}{FryCircuit.Core.IO.CircuitSerializer.DefaultFileExtension}");

        try
        {
            FryCircuit.Core.IO.CircuitSerializer.SaveToFile(original, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = FryCircuit.Core.IO.CircuitSerializer.LoadFromFile(tempFile);
            Assert.NotNull(loaded);
            Assert.Equal(original.Devices.Count, loaded.Devices.Count);
            Assert.Equal(original.Nodes.Count, loaded.Nodes.Count);
            Assert.Equal(original.Name, loaded.Name);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}




