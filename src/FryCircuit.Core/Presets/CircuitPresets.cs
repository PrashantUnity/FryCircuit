using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Display;
using FryCircuit.Core.Devices.Logic;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Presets;

/// <summary>
/// Pure C# factory providing built-in circuit designs without external file dependencies.
/// </summary>
public static class CircuitPresets
{
    public static readonly IReadOnlyList<string> PresetNames = new List<string>
    {
        "Half-Wave Rectifier",
        "Resistor Voltage Divider",
        "RC Step Response",
        "LED with Current Limiter",
        "Two-Way Light Switch",
        "Inverting Op-Amp",
        "MOSFET Inverter",
        "RLC Resonant Tank",
        "Digital Logic Gates",
        "555 Timer Oscillator",
        "Transmon Qubit (Quantum JJ)",
        "Superconducting DC SQUID Magnetometer",
        "Tunnel Diode Microwave Oscillator",
        "Aerospace 3-Phase Drone Inverter",
        "Cosmic Heavy-Ion SEU Radiation Strike",
        "Drone BLDC Hall Commutation Controller",
        "GHz Microwave Bergeron Transmission Line",
        "Potentiometer Tuning & Multimeters",
        "LED Cascade & Current Limiter Ladder",
        "Pyramid Resistor Current Divider Network",
        "Digital 555 & Dual 7-Segment Counter"
    };

    public static Circuit Create(string name)
    {
        return name switch
        {
            "Half-Wave Rectifier" => CreateHalfWaveRectifier(),
            "Resistor Voltage Divider" => CreateResistorDivider(),
            "RC Step Response" => CreateRcStepResponse(),
            "LED with Current Limiter" => CreateLedCircuit(),
            "Two-Way Light Switch" => CreateTwoWaySwitch(),
            "Inverting Op-Amp" => CreateInvertingOpAmp(),
            "MOSFET Inverter" => CreateMosfetInverter(),
            "RLC Resonant Tank" => CreateRlcResonance(),
            "Digital Logic Gates" => CreateLogicGates(),
            "555 Timer Oscillator" => Create555TimerOscillator(),
            "Transmon Qubit (Quantum JJ)" => CreateTransmonQubit(),
            "Superconducting DC SQUID Magnetometer" => CreateSquidMagnetometer(),
            "Tunnel Diode Microwave Oscillator" => CreateTunnelDiodeOscillator(),
            "Aerospace 3-Phase Drone Inverter" => CreateDroneInverter(),
            "Cosmic Heavy-Ion SEU Radiation Strike" => CreateCosmicRadiationStrike(),
            "Drone BLDC Hall Commutation Controller" => CreateDroneHallCommutator(),
            "GHz Microwave Bergeron Transmission Line" => CreateTransmissionLineSerdes(),
            "Potentiometer Tuning & Multimeters" => CreatePotentiometerMeters(),
            "LED Cascade & Current Limiter Ladder" => CreateLedCascadeLadder(),
            "Pyramid Resistor Current Divider Network" => CreatePyramidResistorDivider(),
            "Digital 555 & Dual 7-Segment Counter" => Create555Dual7SegmentCounter(),
            _ => CreateHalfWaveRectifier()
        };
    }

    public static Circuit CreateHalfWaveRectifier()
    {
        var circuit = new Circuit("Half-Wave Rectifier");
        circuit.Frame = new CircuitFrame(0, 0, 14, 12);

        // Nodes
        circuit.GetOrCreateNode(0, isGround: true); // Ground
        circuit.GetOrCreateNode(1);                 // Rectified Output
        circuit.GetOrCreateNode(2);                 // AC Input

        // AC Sine Voltage Source (3V peak, 2kHz)
        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 3.0)
        {
            WaveformType = VoltageWaveformType.Sine,
            Amplitude = 3.0,
            Frequency = 2000.0,
            Transform = new DeviceTransform(DeviceRotation.Up)
        };
        vs.Pins[0].NodeId = 2; // Positive
        vs.Pins[1].NodeId = 0; // Ground
        circuit.AddDevice(vs);

        // Diode
        var diode = new DiodeDevice(2, x: 6, y: 2)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        diode.Pins[0].NodeId = 2; // Anode
        diode.Pins[1].NodeId = 1; // Cathode
        circuit.AddDevice(diode);

        // Load Resistor (1 kOhm)
        var res = new ResistorDevice(3, x: 10, y: 5, resistance: 1000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Up)
        };
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 0;
        circuit.AddDevice(res);

        // Ground Reference
        var gnd = new GroundDevice(4, x: 6, y: 6);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateResistorDivider()
    {
        var circuit = new Circuit("Resistor Voltage Divider");
        circuit.Frame = new CircuitFrame(0, 0, 14, 14);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, x: 2, y: 6, voltage: 10.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 10.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var r1 = new ResistorDevice(2, x: 6, y: 3, resistance: 1000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        var r2 = new ResistorDevice(3, x: 10, y: 6, resistance: 1000.0);
        r2.Pins[0].NodeId = 2;
        r2.Pins[1].NodeId = 0;
        circuit.AddDevice(r2);

        var gnd = new GroundDevice(4, x: 6, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateRcStepResponse()
    {
        var circuit = new Circuit("RC Step Response");
        circuit.Frame = new CircuitFrame(0, 0, 14, 12);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 5.0,
            Frequency = 200.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, x: 6, y: 2, resistance: 1000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        res.Pins[0].NodeId = 1;
        res.Pins[1].NodeId = 2;
        circuit.AddDevice(res);

        var cap = new CapacitorDevice(3, x: 10, y: 5, capacitance: 1e-6);
        cap.Pins[0].NodeId = 2;
        cap.Pins[1].NodeId = 0;
        circuit.AddDevice(cap);

        var gnd = new GroundDevice(4, x: 6, y: 6);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateLedCircuit()
    {
        var circuit = new Circuit("LED with Current Limiter");
        circuit.Frame = new CircuitFrame(0, 0, 14, 12);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(3);

        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 5.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var sw = new SwitchDevice(2, "spst", x: 5, y: 2)
        {
            IsClosed = true,
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        sw.Pins[0].NodeId = 1;
        sw.Pins[1].NodeId = 2;
        circuit.AddDevice(sw);

        var res = new ResistorDevice(3, x: 9, y: 2, resistance: 330.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        res.Pins[0].NodeId = 2;
        res.Pins[1].NodeId = 3;
        circuit.AddDevice(res);

        var led = new LedDevice(4, x: 12, y: 5, wavelength: 650.0); // Red LED
        led.Pins[0].NodeId = 3;
        led.Pins[1].NodeId = 0;
        circuit.AddDevice(led);

        var gnd = new GroundDevice(5, x: 7, y: 6);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateTwoWaySwitch()
    {
        var circuit = new Circuit("Two-Way Light Switch");
        circuit.Frame = new CircuitFrame(0, 0, 16, 10);

        circuit.GetOrCreateNode(0, isGround: true); // Ground rail
        circuit.GetOrCreateNode(1);                 // 12V DC Supply rail
        circuit.GetOrCreateNode(2);                 // Traveler Wire A (top)
        circuit.GetOrCreateNode(3);                 // Traveler Wire B (bottom)
        circuit.GetOrCreateNode(4);                 // Switched Lamp hot terminal

        // 12V DC Power Source at (2, 5)
        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 12.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 12.0
        };
        vs.Pins[0].NodeId = 1; // Top pin at (2, 4)
        vs.Pins[1].NodeId = 0; // Bottom pin at (2, 6)
        circuit.AddDevice(vs);

        // SPDT Switch 1 (Entrance) at (5, 4): Common on left, Throws on right
        var sw1 = new SwitchDevice(2, "spdt", x: 5, y: 4)
        {
            IsClosed = true // Connects Common to Throw 1 (Traveler A)
        };
        sw1.Pins[0].NodeId = 1; // Common pin at (4, 4) connects to 12V rail
        sw1.Pins[1].NodeId = 2; // Throw 1 at (6, 3) connects to Traveler A
        sw1.Pins[2].NodeId = 3; // Throw 2 at (6, 5) connects to Traveler B
        circuit.AddDevice(sw1);

        // SPDT Switch 2 (Exit / Staircase) at (11, 4): Rotated 180° so Common is on right, Throws on left
        var sw2 = new SwitchDevice(3, "spdt", x: 11, y: 4)
        {
            IsClosed = false, // Connects Common to Throw 2 (Traveler A)
            Transform = new DeviceTransform(DeviceRotation.Down)
        };
        // Rotated 180°:
        // Pin 0 (Common) is at (12, 4)
        // Pin 1 (Throw 1) is at (10, 5) -> Traveler B
        // Pin 2 (Throw 2) is at (10, 3) -> Traveler A
        sw2.Pins[0].NodeId = 4; // Common pin at (12, 4) connects to Lamp
        sw2.Pins[1].NodeId = 3; // Pin 1 at (10, 5) connects to Traveler B
        sw2.Pins[2].NodeId = 2; // Pin 2 at (10, 3) connects to Traveler A
        circuit.AddDevice(sw2);

        // Incandescent Light Bulb (12V, 25W) at (14, 5)
        var lamp = new LampDevice(4, x: 14, y: 5, nominalVoltage: 12.0, nominalPower: 25.0);
        lamp.Pins[0].NodeId = 4; // Top pin at (14, 4)
        lamp.Pins[1].NodeId = 0; // Bottom pin at (14, 6)
        circuit.AddDevice(lamp);

        // Ground Reference at center
        var gnd = new GroundDevice(5, x: 8, y: 6);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateInvertingOpAmp()
    {
        var circuit = new Circuit("Inverting Op-Amp");
        circuit.Frame = new CircuitFrame(0, 0, 18, 14);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Input
        circuit.GetOrCreateNode(2); // Inverting input (-)
        circuit.GetOrCreateNode(3); // Output

        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 1.0)
        {
            WaveformType = VoltageWaveformType.Sine,
            Amplitude = 1.0,
            Frequency = 1000.0
        };
        vs.Pins[0].NodeId = 1; // Top pin at (2, 4)
        vs.Pins[1].NodeId = 0; // Bottom pin at (2, 6)
        circuit.AddDevice(vs);

        // Input resistor R_in = 1k at x=6, y=4 (left pin at (5, 4), right pin at (7, 4))
        var rIn = new ResistorDevice(2, x: 6, y: 4, resistance: 1000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        rIn.Pins[1].NodeId = 1; // Connects straight to source (2, 4)
        rIn.Pins[0].NodeId = 2; // Connects straight to OpAmp (-) at (8, 4)
        circuit.AddDevice(rIn);

        // Feedback resistor R_f = 2k (Gain = -2, horizontal at y=2, centered over OpAmp at x=9)
        var rF = new ResistorDevice(3, x: 9, y: 2, resistance: 2000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        rF.Pins[1].NodeId = 2; // Left pin at (8, 2) connects down to (8, 4)
        rF.Pins[0].NodeId = 3; // Right pin at (10, 2) connects down to output (10, 5)
        circuit.AddDevice(rF);

        // OpAmp at (9, 5): (-) input at (8, 4), (+) input at (8, 6), output at (10, 5)
        var opamp = new OpAmpDevice(4, x: 9, y: 5);
        opamp.Pins[0].NodeId = 2; // Inverting (-)
        opamp.Pins[1].NodeId = 0; // Non-inverting (+) to ground
        opamp.Pins[2].NodeId = 3; // Output
        circuit.AddDevice(opamp);

        // Load resistor 1k (vertical at x=13, y=6; top pin at (13, 5), bottom pin at (13, 7))
        var rLoad = new ResistorDevice(5, x: 13, y: 6, resistance: 1000.0);
        rLoad.Pins[0].NodeId = 3; // Top pin connects straight to output (10, 5)
        rLoad.Pins[1].NodeId = 0; // Bottom pin connects to ground
        circuit.AddDevice(rLoad);

        // Ground reference at (8, 7) aligned under OpAmp (+) terminal (8, 6)
        var gnd = new GroundDevice(6, x: 8, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateMosfetInverter()
    {
        var circuit = new Circuit("MOSFET Inverter");
        circuit.Frame = new CircuitFrame(0, 0, 16, 14);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // VDD (5V)
        circuit.GetOrCreateNode(2); // Gate Input
        circuit.GetOrCreateNode(3); // Drain Output

        var vdd = new VoltageSourceDevice(1, x: 2, y: 4, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 5.0
        };
        vdd.Pins[0].NodeId = 1; // Top pin at (2, 3) connects straight to pull-up (11, 3)
        vdd.Pins[1].NodeId = 0; // Bottom pin at (2, 5)
        circuit.AddDevice(vdd);

        // Gate Input Pulse Generator (top pin at (5, 8) connects straight to Gate (9, 8))
        var vGate = new VoltageSourceDevice(2, x: 5, y: 9, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 5.0,
            Frequency = 500.0
        };
        vGate.Pins[0].NodeId = 2; // Top pin at (5, 8)
        vGate.Pins[1].NodeId = 0; // Bottom pin at (5, 10)
        circuit.AddDevice(vGate);

        // Pull-up resistor (1k) vertical along x=11 (top pin at (11, 3), bottom pin at (11, 5))
        var rPullup = new ResistorDevice(3, x: 11, y: 4, resistance: 1000.0);
        rPullup.Pins[0].NodeId = 1;
        rPullup.Pins[1].NodeId = 3;
        circuit.AddDevice(rPullup);

        // N-Channel MOSFET (Gate at (9, 8), Drain at (11, 7), Source at (11, 9))
        var mos = new MosfetDevice(4, "nmos", x: 10, y: 8);
        mos.Pins[0].NodeId = 2; // Gate at (9, 8)
        mos.Pins[1].NodeId = 3; // Drain at (11, 7)
        mos.Pins[2].NodeId = 0; // Source at (11, 9)
        circuit.AddDevice(mos);

        var gnd = new GroundDevice(5, x: 8, y: 10);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateRlcResonance()
    {
        var circuit = new Circuit("RLC Resonant Tank");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(3);

        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Sine,
            Amplitude = 5.0,
            Frequency = 1590.0 // Near resonance for 10mH and 1uF
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        var res = new ResistorDevice(2, x: 6, y: 2, resistance: 100.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        res.Pins[1].NodeId = 1; // Left terminal (x=5) connects to source
        res.Pins[0].NodeId = 2; // Right terminal (x=7) connects to inductor
        circuit.AddDevice(res);

        var ind = new InductorDevice(3, x: 10, y: 2, inductance: 0.01)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        ind.Pins[1].NodeId = 2; // Left terminal (x=9) connects to resistor
        ind.Pins[0].NodeId = 3; // Right terminal (x=11) connects to capacitor
        circuit.AddDevice(ind);

        var cap = new CapacitorDevice(4, x: 13, y: 5, capacitance: 1e-6);
        cap.Pins[0].NodeId = 3;
        cap.Pins[1].NodeId = 0;
        circuit.AddDevice(cap);

        var gnd = new GroundDevice(5, x: 7, y: 6);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateLogicGates()
    {
        var circuit = new Circuit("Digital Logic Gates");
        circuit.Frame = new CircuitFrame(0, 0, 19, 14);

        circuit.GetOrCreateNode(0, isGround: true); // Ground
        circuit.GetOrCreateNode(1);                 // Data A (500 Hz Pulse)
        circuit.GetOrCreateNode(2);                 // Data B (200 Hz Pulse)
        circuit.GetOrCreateNode(3);                 // Select S (50 Hz Select Clock)
        circuit.GetOrCreateNode(4);                 // S_BAR (Inverted Select)
        circuit.GetOrCreateNode(5);                 // MUX Channel 0 (Data A AND S_BAR)
        circuit.GetOrCreateNode(6);                 // MUX Channel 1 (Data B AND S)
        circuit.GetOrCreateNode(7);                 // MUX Output Y (Channel 0 OR Channel 1)

        // 1. Data A Pulse Source at (2, 3): 500 Hz
        var vsDataA = new VoltageSourceDevice(1, x: 2, y: 3, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 5.0,
            Frequency = 500.0
        };
        vsDataA.Pins[0].NodeId = 1; // Top at (2, 2)
        vsDataA.Pins[1].NodeId = 0; // Bottom at (2, 4)
        circuit.AddDevice(vsDataA);

        // 2. Data B Pulse Source at (2, 7): 200 Hz
        var vsDataB = new VoltageSourceDevice(2, x: 2, y: 7, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 5.0,
            Frequency = 200.0
        };
        vsDataB.Pins[0].NodeId = 2; // Top at (2, 6)
        vsDataB.Pins[1].NodeId = 0; // Bottom at (2, 8)
        circuit.AddDevice(vsDataB);

        // 3. Select S Pulse Source at (2, 11): 50 Hz
        var vsSelect = new VoltageSourceDevice(3, x: 2, y: 11, voltage: 5.0)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 5.0,
            Frequency = 50.0
        };
        vsSelect.Pins[0].NodeId = 3; // Top at (2, 10)
        vsSelect.Pins[1].NodeId = 0; // Bottom at (2, 12)
        circuit.AddDevice(vsSelect);

        // Left Ground reference for sources at (2, 13)
        var gndLeft = new GroundDevice(4, x: 2, y: 13);
        gndLeft.Pins[0].NodeId = 0; // (2, 12)
        circuit.AddDevice(gndLeft);

        // 4. Inverter (NOT gate) for Select signal at (6, 4)
        var gateNot = new LogicGateDevice(5, "not", x: 6, y: 4);
        gateNot.Pins[0].NodeId = 3; // Input at (5, 4)
        gateNot.Pins[1].NodeId = 4; // Output at (7, 4) -> S_BAR
        circuit.AddDevice(gateNot);

        // 5. AND Gate 0 (Data A AND S_BAR) at (10, 3)
        var gateAnd0 = new LogicGateDevice(6, "and", x: 10, y: 3);
        gateAnd0.Pins[0].NodeId = 1; // Input A at (9, 2) connects to Data A
        gateAnd0.Pins[1].NodeId = 4; // Input B at (9, 4) connects to S_BAR
        gateAnd0.Pins[2].NodeId = 5; // Output at (11, 3) -> Channel 0
        circuit.AddDevice(gateAnd0);

        // 6. AND Gate 1 (Data B AND S) at (10, 9)
        var gateAnd1 = new LogicGateDevice(7, "and", x: 10, y: 9);
        gateAnd1.Pins[0].NodeId = 2; // Input A at (9, 8) connects to Data B
        gateAnd1.Pins[1].NodeId = 3; // Input B at (9, 10) connects to Select S
        gateAnd1.Pins[2].NodeId = 6; // Output at (11, 9) -> Channel 1
        circuit.AddDevice(gateAnd1);

        // 7. OR Gate (Channel 0 OR Channel 1) at (14, 6)
        var gateOr = new LogicGateDevice(8, "or", x: 14, y: 6);
        gateOr.Pins[0].NodeId = 5; // Input A at (13, 5) connects to Channel 0
        gateOr.Pins[1].NodeId = 6; // Input B at (13, 7) connects to Channel 1
        gateOr.Pins[2].NodeId = 7; // Output at (15, 6) -> MUX Output Y
        circuit.AddDevice(gateOr);

        // 8. Load Resistor 1k at (17, 7)
        var load = new ResistorDevice(9, x: 17, y: 7, resistance: 1000.0);
        load.Pins[0].NodeId = 7; // Top pin at (17, 6)
        load.Pins[1].NodeId = 0; // Bottom pin at (17, 8)
        circuit.AddDevice(load);

        // Right Ground reference at (17, 9)
        var gndRight = new GroundDevice(10, x: 17, y: 9);
        gndRight.Pins[0].NodeId = 0; // (17, 8)
        circuit.AddDevice(gndRight);

        return circuit;
    }

    public static Circuit Create555TimerOscillator()
    {
        var circuit = new Circuit("555 Timer Oscillator");
        circuit.Frame = new CircuitFrame(0, 0, 18, 14);

        circuit.GetOrCreateNode(0, isGround: true); // Ground
        circuit.GetOrCreateNode(1);                 // VCC (9V)
        circuit.GetOrCreateNode(2);                 // Output
        circuit.GetOrCreateNode(3);                 // Discharge node
        circuit.GetOrCreateNode(4);                 // Threshold & Trigger timing node
        circuit.GetOrCreateNode(5);                 // Control voltage decoupled

        // DC 9V Power Supply (pins at (2, 3.5) and (2, 5.5))
        var vcc = new VoltageSourceDevice(1, x: 2, y: 4.5, voltage: 9.0);
        vcc.Pins[0].NodeId = 1;
        vcc.Pins[1].NodeId = 0;
        circuit.AddDevice(vcc);

        // 555 Timer IC (center at (10, 6))
        var timer = new Timer555Device(2, x: 10, y: 6);
        timer.Pins[0].NodeId = 0; // GND (bottom at (10, 7.5))
        timer.Pins[1].NodeId = 4; // TRIG (left-bottom at (8.5, 6.8))
        timer.Pins[2].NodeId = 2; // OUT (right at (11.5, 6.0))
        timer.Pins[3].NodeId = 1; // RESET to VCC (left-top at (8.5, 5.0))
        timer.Pins[4].NodeId = 5; // CTRL (right-bottom at (11.5, 7.0))
        timer.Pins[5].NodeId = 4; // THRESH (left-middle at (8.5, 6.2))
        timer.Pins[6].NodeId = 3; // DISCH (left-upper at (8.5, 5.6))
        timer.Pins[7].NodeId = 1; // VCC (top at (10, 4.5))
        circuit.AddDevice(timer);

        // Timing resistor R1 (10 kΩ vertical between VCC y=3.5 and DISCH y=5.5)
        var r1 = new ResistorDevice(3, x: 6, y: 4.5, resistance: 10000.0);
        r1.Pins[0].NodeId = 1; // Top pin at (6, 3.5) connects to VCC
        r1.Pins[1].NodeId = 3; // Bottom pin at (6, 5.5) connects to DISCH
        circuit.AddDevice(r1);

        // Timing resistor R2 (47 kΩ vertical between DISCH y=5.5 and THRESH/TRIG y=7.5)
        var r2 = new ResistorDevice(4, x: 6, y: 6.5, resistance: 47000.0);
        r2.Pins[0].NodeId = 3; // Top pin at (6, 5.5) connects to DISCH
        r2.Pins[1].NodeId = 4; // Bottom pin at (6, 7.5) connects to THRESH/TRIG
        circuit.AddDevice(r2);

        // Timing capacitor C1 (10 µF vertical between THRESH/TRIG y=7.5 and GND y=9.5)
        var c1 = new CapacitorDevice(5, x: 6, y: 8.5, capacitance: 10e-6);
        c1.Pins[0].NodeId = 4; // Top pin at (6, 7.5)
        c1.Pins[1].NodeId = 0; // Bottom pin at (6, 9.5)
        circuit.AddDevice(c1);

        // Output Load: 1 kΩ Resistor (vertical at x=15.5, y=7.0; top pin at (15.5, 6.0) aligns straight with OUT)
        var rload = new ResistorDevice(6, x: 15.5, y: 7.0, resistance: 1000.0);
        rload.Pins[0].NodeId = 2; // Top pin at (15.5, 6.0) connects straight to OUT
        rload.Pins[1].NodeId = 0; // Bottom pin at (15.5, 8.0) connects to GND (15.5, 9.5)
        circuit.AddDevice(rload);

        // Control Voltage filter cap (10 nF vertical at x=12.5, y=8.0; top pin at (12.5, 7.0) aligns straight with CTRL)
        var cCtrl = new CapacitorDevice(7, x: 12.5, y: 8.0, capacitance: 10e-9);
        cCtrl.Pins[0].NodeId = 5; // Top pin at (12.5, 7.0) connects straight to CTRL
        cCtrl.Pins[1].NodeId = 0; // Bottom pin at (12.5, 9.0) connects to GND (12.5, 9.5)
        circuit.AddDevice(cCtrl);

        // Ground reference (aligned on ground rail at (10, 9.5))
        var gnd = new GroundDevice(8, x: 10, y: 9.5);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateTransmonQubit()
    {
        var circuit = new Circuit("Transmon Qubit (Quantum JJ)");
        circuit.Frame = new CircuitFrame(0, 0, 18, 12);

        circuit.GetOrCreateNode(0, isGround: true); // Superconducting Ground
        circuit.GetOrCreateNode(1);                 // Microwave Drive
        circuit.GetOrCreateNode(2);                 // Qubit Island Node

        // Microwave Drive Source (Scaled for Audio/Substep Visualization)
        var vs = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 0.005)
        {
            WaveformType = VoltageWaveformType.Sine,
            Amplitude = 0.005, // 5.0 mV RF tone
            Frequency = 600.0  // 600 Hz in center of audio/FFT bandwidth
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Readout Coupling Capacitor Cc = 1.0 µF at x=6, y=4
        var cc = new CapacitorDevice(2, x: 6, y: 4, capacitance: 1e-6)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        cc.Pins[1].NodeId = 1; // Left terminal (x=5) connects to source
        cc.Pins[0].NodeId = 2; // Right terminal (x=7) connects to qubit
        circuit.AddDevice(cc);

        // Josephson Junction (Superconducting Qubit Barrier - vertical shunt at x=9)
        var jj = new JosephsonJunctionDevice(3, x: 9, y: 5)
        {
            CriticalCurrent = 50e-6, // 50 µA
            NormalResistance = 500.0,
            JunctionCapacitance = 0.5e-6,
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        jj.Pins[0].NodeId = 2; // Top terminal (y=4) connects to qubit rail
        jj.Pins[1].NodeId = 0; // Bottom terminal (y=6) connects to ground bus
        circuit.AddDevice(jj);

        // Transmon Shunt Capacitor C_B = 2.0 µF at x=12
        var cb = new CapacitorDevice(4, x: 12, y: 5, capacitance: 2e-6);
        cb.Pins[0].NodeId = 2; // Top terminal (y=4)
        cb.Pins[1].NodeId = 0; // Bottom terminal (y=6)
        circuit.AddDevice(cb);

        // Readout termination 500 Ohm at x=15
        var rTerm = new ResistorDevice(5, x: 15, y: 5, resistance: 500.0);
        rTerm.Pins[0].NodeId = 2; // Top terminal (y=4)
        rTerm.Pins[1].NodeId = 0; // Bottom terminal (y=6)
        circuit.AddDevice(rTerm);

        var gnd = new GroundDevice(6, x: 9, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateTunnelDiodeOscillator()
    {
        var circuit = new Circuit("Tunnel Diode Microwave Oscillator");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true); // Ground
        circuit.GetOrCreateNode(1);                 // DC Bias
        circuit.GetOrCreateNode(2);                 // Microwave Tank Node

        // DC Bias (0.16V: placed right in the Negative Differential Resistance slope)
        var vdc = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 0.16);
        vdc.Pins[0].NodeId = 1;
        vdc.Pins[1].NodeId = 0;
        circuit.AddDevice(vdc);

        // Bias Decoupling Resistor (20 Ω)
        var rBias = new ResistorDevice(2, x: 5, y: 4, resistance: 20.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        rBias.Pins[1].NodeId = 1; // Left terminal (x=4) connects to bias supply
        rBias.Pins[0].NodeId = 2; // Right terminal (x=6) connects to tunnel diode
        circuit.AddDevice(rBias);

        // Quantum Tunnel Diode (Esaki Diode with NDR - vertical shunt)
        var td = new TunnelDiodeDevice(3, x: 8, y: 5)
        {
            PeakCurrent = 0.008, // 8 mA
            ValleyCurrent = 0.001,
            PeakVoltage = 0.065,
            ValleyVoltage = 0.35,
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        td.Pins[0].NodeId = 2; // Top terminal (y=4)
        td.Pins[1].NodeId = 0; // Bottom terminal (y=6)
        circuit.AddDevice(td);

        // Microwave Resonant Tank Inductor L = 10 µH
        var ind = new InductorDevice(4, x: 11, y: 5, inductance: 10e-6);
        ind.Pins[0].NodeId = 2; // Top terminal (y=4)
        ind.Pins[1].NodeId = 0; // Bottom terminal (y=6)
        circuit.AddDevice(ind);

        // Microwave Resonant Tank Capacitor C = 100 nF
        var cap = new CapacitorDevice(5, x: 14, y: 5, capacitance: 100e-9);
        cap.Pins[0].NodeId = 2; // Top terminal (y=4)
        cap.Pins[1].NodeId = 0; // Bottom terminal (y=6)
        circuit.AddDevice(cap);

        var gnd = new GroundDevice(6, x: 8, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateDroneInverter()
    {
        var circuit = new Circuit("Aerospace 3-Phase Drone Inverter");
        circuit.Frame = new CircuitFrame(0, 0, 25, 14);

        circuit.GetOrCreateNode(0, isGround: true); // DC Ground
        circuit.GetOrCreateNode(1);                 // 14.8V Drone Battery Bus
        circuit.GetOrCreateNode(2);                 // Phase U Output
        circuit.GetOrCreateNode(3);                 // Phase V Output
        circuit.GetOrCreateNode(4);                 // Phase U Gate Drive
        circuit.GetOrCreateNode(5);                 // Motor Neutral Star Center
        circuit.GetOrCreateNode(6);                 // Phase V Gate Drive

        // 14.8V 4S LiPo Drone Battery (aligned with high-side drain rail at y=2)
        var vbat = new VoltageSourceDevice(1, x: 2, y: 3, voltage: 14.8);
        vbat.Pins[0].NodeId = 1; // Top pin (2, 2) connects to 14.8V rail
        vbat.Pins[1].NodeId = 0; // Bottom pin (2, 4) connects to ground bus
        circuit.AddDevice(vbat);

        // Gate pull-down resistor for Phase U (100 kΩ vertical at x=5, y=4)
        var rgu = new ResistorDevice(8, x: 5, y: 4, resistance: 100000.0);
        rgu.Pins[0].NodeId = 4; // Top pin at (5, 3) connects to Gate U_H
        rgu.Pins[1].NodeId = 0; // Bottom pin at (5, 5) connects to Ground
        circuit.AddDevice(rgu);

        // High-Side GaN MOSFET Phase U (Drain at (9, 2), Source at (9, 4))
        var q1 = new MosfetDevice(2, "nmos", x: 8, y: 3);
        q1.Pins[0].NodeId = 4; // Gate U_H at (7, 3)
        q1.Pins[1].NodeId = 1; // Drain to 14.8V bus at (9, 2)
        q1.Pins[2].NodeId = 2; // Source to Phase U at (9, 4)
        circuit.AddDevice(q1);

        // Low-Side GaN MOSFET Phase U (Drain at (9, 6), Source at (9, 8))
        var q2 = new MosfetDevice(3, "nmos", x: 8, y: 7);
        q2.Pins[0].NodeId = 0; // Gate low at (7, 7)
        q2.Pins[1].NodeId = 2; // Drain to Phase U at (9, 6)
        q2.Pins[2].NodeId = 0; // Source to Ground bus at (9, 8)
        circuit.AddDevice(q2);

        // Gate pull-down resistor for Phase V (100 kΩ vertical at x=11, y=7; bottom pin on ground bus at y=8)
        var rgv = new ResistorDevice(9, x: 11, y: 7, resistance: 100000.0);
        rgv.Pins[0].NodeId = 6; // Top pin at (11, 6) connects to Gate V_L
        rgv.Pins[1].NodeId = 0; // Bottom pin at (11, 8) connects directly to Ground bus
        circuit.AddDevice(rgv);

        // High-Side GaN MOSFET Phase V (Drain at (16, 2), Source at (16, 4))
        var q3 = new MosfetDevice(4, "nmos", x: 15, y: 3);
        q3.Pins[0].NodeId = 0; // Gate low at (14, 3)
        q3.Pins[1].NodeId = 1; // Drain to 14.8V bus at (16, 2)
        q3.Pins[2].NodeId = 3; // Source to Phase V at (16, 4)
        circuit.AddDevice(q3);

        // Low-Side GaN MOSFET Phase V (Drain at (16, 6), Source at (16, 8))
        var q4 = new MosfetDevice(5, "nmos", x: 15, y: 7);
        q4.Pins[0].NodeId = 6; // Gate V_L at (14, 7)
        q4.Pins[1].NodeId = 3; // Drain to Phase V at (16, 6)
        q4.Pins[2].NodeId = 0; // Source to Ground bus at (16, 8)
        circuit.AddDevice(q4);

        // Motor Stator Coils (Wye / Star connected with realistic stator winding resistance)
        // Phase U coil to Neutral (horizontal, left pin at (18, 4))
        var lu = new InductorDevice(6, x: 19, y: 4, inductance: 50e-6)
        {
            Transform = new DeviceTransform(DeviceRotation.Right),
            SeriesResistance = 0.45 // 450 mΩ BLDC phase winding resistance
        };
        lu.Pins[1].NodeId = 2; // Connects straight to Phase U (9, 4)
        lu.Pins[0].NodeId = 5; // Neutral at (20, 4)
        circuit.AddDevice(lu);

        // Phase V coil to Neutral (horizontal, left pin at (18, 6))
        var lv = new InductorDevice(7, x: 19, y: 6, inductance: 50e-6)
        {
            Transform = new DeviceTransform(DeviceRotation.Right),
            SeriesResistance = 0.45 // 450 mΩ BLDC phase winding resistance
        };
        lv.Pins[1].NodeId = 3; // Connects straight to Phase V (16, 6)
        lv.Pins[0].NodeId = 5; // Neutral at (20, 6)
        circuit.AddDevice(lv);

        // Neutral point stabilization bleeder
        var rneut = new ResistorDevice(10, x: 22, y: 6, resistance: 100000.0);
        rneut.Pins[0].NodeId = 5; // Neutral at (22, 5)
        rneut.Pins[1].NodeId = 0; // Ground at (22, 7)
        circuit.AddDevice(rneut);

        var gnd = new GroundDevice(11, x: 10, y: 8);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateSquidMagnetometer()
    {
        var circuit = new Circuit("Superconducting DC SQUID Magnetometer");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true); // Superconducting Ground
        circuit.GetOrCreateNode(1);                 // SQUID Upper Terminal
        circuit.GetOrCreateNode(2);                 // Bias Current Node

        // Cryogenic Bias Current Source (25 µA)
        var ibias = new CurrentSourceDevice(1, x: 3, y: 5, current: 25.0e-6);
        ibias.Pins[0].NodeId = 1;
        ibias.Pins[1].NodeId = 0;
        circuit.AddDevice(ibias);

        // DC SQUID with External Flux Modulation
        var squid = new SquidDevice(2, x: 8, y: 5)
        {
            BaseCriticalCurrent = 12.0e-6, // 12 µA per junction
            NormalResistance = 180.0,      // 180 Ω
            TotalCapacitance = 60.0e-15,   // 60 fF
            ExternalFluxNormalized = 0.25  // 0.25 Phi_0
        };
        squid.Pins[0].NodeId = 1;
        squid.Pins[1].NodeId = 0;
        circuit.AddDevice(squid);

        // Cryogenic Shunt Damping Resistor (500 Ω)
        var rshunt = new ResistorDevice(3, x: 13, y: 5, resistance: 500.0);
        rshunt.Pins[0].NodeId = 1;
        rshunt.Pins[1].NodeId = 0;
        circuit.AddDevice(rshunt);

        var gnd = new GroundDevice(4, x: 8, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateCosmicRadiationStrike()
    {
        var circuit = new Circuit("Cosmic Heavy-Ion SEU Radiation Strike");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true); // Spacecraft Chassis Ground
        circuit.GetOrCreateNode(1);                 // 3.3V Spacecraft Power Rail
        circuit.GetOrCreateNode(2);                 // Memory Latch Storage Node Q

        // 3.3V Space Radiation-Tolerant Power Rail
        var vdd = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 3.3);
        vdd.Pins[0].NodeId = 1;
        vdd.Pins[1].NodeId = 0;
        circuit.AddDevice(vdd);

        // Pull-Up Load Resistor (10 kΩ horizontal along 3.3V rail at y=4)
        var rload = new ResistorDevice(2, x: 6, y: 4, resistance: 10000.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        rload.Pins[1].NodeId = 1; // Left pin at (5, 4) connects to 3.3V supply
        rload.Pins[0].NodeId = 2; // Right pin at (7, 4) connects to storage node Q
        circuit.AddDevice(rload);

        // Cosmic Heavy-Ion Radiation Strike Transient Source (LET ~ 45 MeV*cm2/mg, 150 fC charge)
        var ion = new CosmicIonPulseDevice(3, x: 10, y: 5)
        {
            CollectedCharge = 150.0e-15, // 150 fC heavy ion track
            RiseTimeSeconds = 8.0e-12,    // 8 ps
            FallTimeSeconds = 120.0e-12,  // 120 ps
            StrikeTimeSeconds = 50.0e-9   // Strike at 50 ns
        };
        ion.Pins[0].NodeId = 2; // Hits Latch Node Q at (10, 4)
        ion.Pins[1].NodeId = 0; // Chassis ground at (10, 6)
        circuit.AddDevice(ion);

        // Node Parasitic Storage Capacitance (20 fF vertical between y=4 and y=6)
        var cnode = new CapacitorDevice(4, x: 14, y: 5, capacitance: 20.0e-15);
        cnode.Pins[0].NodeId = 2; // Storage node Q at (14, 4)
        cnode.Pins[1].NodeId = 0; // Chassis ground at (14, 6)
        circuit.AddDevice(cnode);

        var gnd = new GroundDevice(5, x: 10, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateDroneHallCommutator()
    {
        var circuit = new Circuit("Drone BLDC Hall Commutation Controller");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true); // Drone ESC Ground
        circuit.GetOrCreateNode(1);                 // 5.0V Regulated Logic Rail
        circuit.GetOrCreateNode(2);                 // Hall Output (+)
        circuit.GetOrCreateNode(3);                 // Hall Output (-)
        circuit.GetOrCreateNode(4);                 // Commutation Gating Signal

        // 5.0V Logic Supply
        var vcc = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 5.0);
        vcc.Pins[0].NodeId = 1;
        vcc.Pins[1].NodeId = 0;
        circuit.AddDevice(vcc);

        // 4-Terminal Hall Effect Rotor Position Transducer
        var hall = new HallEffectSensorDevice(2, x: 7, y: 5)
        {
            SensitivityVoltsPerTesla = 3.0, // 3.0 V/T
            MagneticFieldTesla = 0.080,      // 80 mT Neodymium rotor magnet
            InputResistance = 800.0,
            OutputResistance = 75.0
        };
        hall.Pins[0].NodeId = 1; // Vcc at (6, 4)
        hall.Pins[1].NodeId = 0; // GND at (6, 6)
        hall.Pins[2].NodeId = 2; // Out (+) at (8, 4)
        hall.Pins[3].NodeId = 3; // Out (-) at (8, 6)
        circuit.AddDevice(hall);

        // Differential Termination Resistor (vertical between y=4 and y=6)
        var rdiff = new ResistorDevice(3, x: 11, y: 5, resistance: 10000.0);
        rdiff.Pins[0].NodeId = 2; // Top pin (11, 4) connects straight to Out (+)
        rdiff.Pins[1].NodeId = 3; // Bottom pin (11, 6) connects straight to Out (-)
        circuit.AddDevice(rdiff);

        var gnd = new GroundDevice(4, x: 6, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateTransmissionLineSerdes()
    {
        var circuit = new Circuit("GHz Microwave Bergeron Transmission Line");
        circuit.Frame = new CircuitFrame(0, 0, 18, 12);

        circuit.GetOrCreateNode(0, isGround: true); // RF Reference Ground
        circuit.GetOrCreateNode(1);                 // High-Speed Driver
        circuit.GetOrCreateNode(2);                 // Transmission Line Input (Near-End)
        circuit.GetOrCreateNode(3);                 // Transmission Line Output (Far-End)

        // 2 GHz High-Speed Pulse Source (pins at (2, 4) and (2, 6))
        var vsource = new VoltageSourceDevice(1, x: 2, y: 5, voltage: 1.2)
        {
            WaveformType = VoltageWaveformType.Pulse,
            Amplitude = 1.2,
            Frequency = 2.0e9 // 2 GHz
        };
        vsource.Pins[0].NodeId = 1;
        vsource.Pins[1].NodeId = 0;
        circuit.AddDevice(vsource);

        // Source Matching Resistor (50 Ω horizontal along y=4)
        var rsource = new ResistorDevice(2, x: 5, y: 4, resistance: 50.0)
        {
            Transform = new DeviceTransform(DeviceRotation.Right)
        };
        rsource.Pins[1].NodeId = 1; // Left pin at (4, 4) connects to source (2, 4)
        rsource.Pins[0].NodeId = 2; // Right pin at (6, 4) connects to tline (7, 4)
        circuit.AddDevice(rsource);

        // Bergeron Distributed Transmission Line (50 Ω, 250 ps delay)
        var tline = new TransmissionLineDevice(3, x: 9, y: 5)
        {
            CharacteristicImpedance = 50.0,
            DelaySeconds = 250.0e-12 // 250 ps
        };
        tline.Pins[0].NodeId = 2; // Port 1 (+) at (7, 4)
        tline.Pins[1].NodeId = 0; // Port 1 (-) at (7, 6)
        tline.Pins[2].NodeId = 3; // Port 2 (+) at (11, 4)
        tline.Pins[3].NodeId = 0; // Port 2 (-) at (11, 6)
        circuit.AddDevice(tline);

        // Far-End Termination Resistor (50 Ω vertical between y=4 and y=6)
        var rterm = new ResistorDevice(4, x: 13, y: 5, resistance: 50.0);
        rterm.Pins[0].NodeId = 3; // Top pin at (13, 4) connects to tline Out (+)
        rterm.Pins[1].NodeId = 0; // Bottom pin at (13, 6) connects to Ground bus
        circuit.AddDevice(rterm);

        var gnd = new GroundDevice(5, x: 7, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreatePotentiometerMeters()
    {
        var circuit = new Circuit("Potentiometer Tuning & Multimeters");
        circuit.Frame = new CircuitFrame(0, 0, 16, 12);

        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(3);

        // 10V DC Voltage source at (2, 6)
        var vs = new VoltageSourceDevice(1, x: 2, y: 6, voltage: 10.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 10.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Ammeter at (6, 3) in series along the top rail
        var am = new AmmeterDevice(2, x: 6, y: 3)
        {
            Transform = new DeviceTransform(DeviceRotation.Left)
        };
        am.Pins[0].NodeId = 1;
        am.Pins[1].NodeId = 2;
        circuit.AddDevice(am);

        // Potentiometer at (10, 6) (10 kΩ with 50% wiper position)
        var pot = new PotentiometerDevice(3, x: 10, y: 6)
        {
            Resistance = 10000.0,
            Wiper = 0.5
        };
        pot.Pins[0].NodeId = 2; // Terminal A (top)
        pot.Pins[1].NodeId = 0; // Terminal B (bottom, ground)
        pot.Pins[2].NodeId = 3; // Wiper (right, output)
        circuit.AddDevice(pot);

        // Voltmeter at (14, 6) measuring wiper potential relative to ground
        var vm = new VoltmeterDevice(4, x: 14, y: 6);
        vm.Pins[0].NodeId = 3; // + to Wiper
        vm.Pins[1].NodeId = 0; // - to Ground
        circuit.AddDevice(vm);

        // Ground reference at (6, 7)
        var gnd = new GroundDevice(5, x: 6, y: 7);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        return circuit;
    }

    public static Circuit CreateLedCascadeLadder()
    {
        var circuit = new Circuit("LED Cascade & Current Limiter Ladder");
        circuit.Frame = new CircuitFrame(0, 0, 22, 16);

        // Nodes: 0 (GND), 1 (9V), and intermediate ladder potentials
        circuit.GetOrCreateNode(0, isGround: true);
        for (int i = 1; i <= 11; i++) circuit.GetOrCreateNode(i);

        // 9V DC Main Rail Supply
        var vs = new VoltageSourceDevice(1, x: 2, y: 7, voltage: 9.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 9.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Column 1: 260 Ω -> 1 Green LED (conducts ~26.8 mA)
        var r1 = new ResistorDevice(2, x: 6, y: 5, resistance: 260.0);
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        var led1_1 = new LedDevice(3, x: 6, y: 11, wavelength: 550.0); // Green
        led1_1.Pins[0].NodeId = 2;
        led1_1.Pins[1].NodeId = 0;
        circuit.AddDevice(led1_1);

        // Column 2: 220 Ω -> Yellow LED -> Green LED (conducts ~22.6 mA)
        var r2 = new ResistorDevice(4, x: 10, y: 5, resistance: 220.0);
        r2.Pins[0].NodeId = 1;
        r2.Pins[1].NodeId = 3;
        circuit.AddDevice(r2);

        var led2_1 = new LedDevice(5, x: 10, y: 8, wavelength: 580.0); // Yellow
        led2_1.Pins[0].NodeId = 3;
        led2_1.Pins[1].NodeId = 4;
        circuit.AddDevice(led2_1);

        var led2_2 = new LedDevice(6, x: 10, y: 11, wavelength: 550.0); // Green
        led2_2.Pins[0].NodeId = 4;
        led2_2.Pins[1].NodeId = 0;
        circuit.AddDevice(led2_2);

        // Column 3: 180 Ω -> Orange LED -> Yellow LED -> Green LED (conducts ~17.0 mA)
        var r3 = new ResistorDevice(7, x: 14, y: 3, resistance: 180.0);
        r3.Pins[0].NodeId = 1;
        r3.Pins[1].NodeId = 5;
        circuit.AddDevice(r3);

        var led3_1 = new LedDevice(8, x: 14, y: 6, wavelength: 610.0); // Orange
        led3_1.Pins[0].NodeId = 5;
        led3_1.Pins[1].NodeId = 6;
        circuit.AddDevice(led3_1);

        var led3_2 = new LedDevice(9, x: 14, y: 8.5, wavelength: 580.0); // Yellow
        led3_2.Pins[0].NodeId = 6;
        led3_2.Pins[1].NodeId = 7;
        circuit.AddDevice(led3_2);

        var led3_3 = new LedDevice(10, x: 14, y: 11, wavelength: 550.0); // Green
        led3_3.Pins[0].NodeId = 7;
        led3_3.Pins[1].NodeId = 0;
        circuit.AddDevice(led3_3);

        // Column 4: 140 Ω -> Red LED -> Orange LED -> Yellow LED -> Green LED (conducts ~9.4 mA)
        var r4 = new ResistorDevice(11, x: 18, y: 1, resistance: 140.0);
        r4.Pins[0].NodeId = 1;
        r4.Pins[1].NodeId = 8;
        circuit.AddDevice(r4);

        var led4_1 = new LedDevice(12, x: 18, y: 3.5, wavelength: 650.0); // Red
        led4_1.Pins[0].NodeId = 8;
        led4_1.Pins[1].NodeId = 9;
        circuit.AddDevice(led4_1);

        var led4_2 = new LedDevice(13, x: 18, y: 6, wavelength: 610.0); // Orange
        led4_2.Pins[0].NodeId = 9;
        led4_2.Pins[1].NodeId = 10;
        circuit.AddDevice(led4_2);

        var led4_3 = new LedDevice(14, x: 18, y: 8.5, wavelength: 580.0); // Yellow
        led4_3.Pins[0].NodeId = 10;
        led4_3.Pins[1].NodeId = 11;
        circuit.AddDevice(led4_3);

        var led4_4 = new LedDevice(15, x: 18, y: 11, wavelength: 550.0); // Green
        led4_4.Pins[0].NodeId = 11;
        led4_4.Pins[1].NodeId = 0;
        circuit.AddDevice(led4_4);

        // Ground Return Reference
        var gnd = new GroundDevice(16, x: 10, y: 14);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        circuit.EnsureWiresSynthesized();
        return circuit;
    }

    public static Circuit CreatePyramidResistorDivider()
    {
        var circuit = new Circuit("Pyramid Resistor Current Divider Network");
        circuit.Frame = new CircuitFrame(0, 0, 20, 18);

        // Nodes: 0 (GND), 1 (4V), 2 (3V), 3 (2V), 4 (1V)
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);
        circuit.GetOrCreateNode(3);
        circuit.GetOrCreateNode(4);

        // 4.0V DC Main Rail Supply
        var vs = new VoltageSourceDevice(1, x: 2, y: 8, voltage: 4.0)
        {
            WaveformType = VoltageWaveformType.Dc,
            Voltage = 4.0
        };
        vs.Pins[0].NodeId = 1;
        vs.Pins[1].NodeId = 0;
        circuit.AddDevice(vs);

        // Tier 1: 1 Ω (carries 1.0 A, drops 4V -> 3V)
        var r1 = new ResistorDevice(2, x: 10, y: 3, resistance: 1.0);
        r1.Pins[0].NodeId = 1;
        r1.Pins[1].NodeId = 2;
        circuit.AddDevice(r1);

        // Tier 2: Two 2 Ω resistors (carries 500 mA each, drops 3V -> 2V)
        var r2_1 = new ResistorDevice(3, x: 8, y: 6.5, resistance: 2.0);
        r2_1.Pins[0].NodeId = 2;
        r2_1.Pins[1].NodeId = 3;
        circuit.AddDevice(r2_1);

        var r2_2 = new ResistorDevice(4, x: 12, y: 6.5, resistance: 2.0);
        r2_2.Pins[0].NodeId = 2;
        r2_2.Pins[1].NodeId = 3;
        circuit.AddDevice(r2_2);

        // Tier 3: Three 3 Ω resistors (carries 333 mA each, drops 2V -> 1V)
        var r3_1 = new ResistorDevice(5, x: 6, y: 10, resistance: 3.0);
        r3_1.Pins[0].NodeId = 3;
        r3_1.Pins[1].NodeId = 4;
        circuit.AddDevice(r3_1);

        var r3_2 = new ResistorDevice(6, x: 10, y: 10, resistance: 3.0);
        r3_2.Pins[0].NodeId = 3;
        r3_2.Pins[1].NodeId = 4;
        circuit.AddDevice(r3_2);

        var r3_3 = new ResistorDevice(7, x: 14, y: 10, resistance: 3.0);
        r3_3.Pins[0].NodeId = 3;
        r3_3.Pins[1].NodeId = 4;
        circuit.AddDevice(r3_3);

        // Tier 4: Four 4 Ω resistors (carries 250 mA each, drops 1V -> 0V)
        var r4_1 = new ResistorDevice(8, x: 4, y: 13.5, resistance: 4.0);
        r4_1.Pins[0].NodeId = 4;
        r4_1.Pins[1].NodeId = 0;
        circuit.AddDevice(r4_1);

        var r4_2 = new ResistorDevice(9, x: 8, y: 13.5, resistance: 4.0);
        r4_2.Pins[0].NodeId = 4;
        r4_2.Pins[1].NodeId = 0;
        circuit.AddDevice(r4_2);

        var r4_3 = new ResistorDevice(10, x: 12, y: 13.5, resistance: 4.0);
        r4_3.Pins[0].NodeId = 4;
        r4_3.Pins[1].NodeId = 0;
        circuit.AddDevice(r4_3);

        var r4_4 = new ResistorDevice(11, x: 16, y: 13.5, resistance: 4.0);
        r4_4.Pins[0].NodeId = 4;
        r4_4.Pins[1].NodeId = 0;
        circuit.AddDevice(r4_4);

        // Ground Reference
        var gnd = new GroundDevice(12, x: 10, y: 16);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        circuit.EnsureWiresSynthesized();
        return circuit;
    }

    /// <summary>
    /// Interactive Digital 555 Timer & Dual 7-Segment Display:
    /// Precision 555 Timer Astable Multivibrator driving Dual 74193 4-Bit Counters,
    /// Dual 7448 BCD-to-7-Segment Decoders, and Illuminated Green 7-Segment Displays showing 'A' and '4',
    /// accompanied by Green/Amber/Red diagnostic LEDs, capacitor filters, and live telemetry annotations.
    /// </summary>
    public static Circuit Create555Dual7SegmentCounter()
    {
        var circuit = new Circuit("Digital 555 & Dual 7-Segment Counter");

        // Nodes:
        // 0: GND (0 V)
        // 1: VCC (6 V)
        // 2: 555 Trigger / Threshold (Oscillating 2V - 4V)
        // 3: 555 Output Clock (0V - 6V pulses)
        // 4: 555 Control Voltage (~4V)
        // 5: 555 Discharge node
        // 7: LED rail 1
        // 8: LED rail 2
        // 9: LED rail 3

        circuit.Nodes[0] = new CircuitNode(0) { IsGround = true, Voltage = 0.0 };
        circuit.Nodes[1] = new CircuitNode(1) { Voltage = 6.0 };
        circuit.Nodes[2] = new CircuitNode(2) { Voltage = 3.99 };
        circuit.Nodes[3] = new CircuitNode(3) { Voltage = 4.0 };
        circuit.Nodes[4] = new CircuitNode(4) { Voltage = 4.0 };
        circuit.Nodes[5] = new CircuitNode(5) { Voltage = 2.5 };
        circuit.Nodes[7] = new CircuitNode(7) { Voltage = 5.0 };
        circuit.Nodes[8] = new CircuitNode(8) { Voltage = 3.93 };
        circuit.Nodes[9] = new CircuitNode(9) { Voltage = 3.99 };

        // 1. 6V Main Power Rail
        var vsrc = new VoltageSourceDevice(1, x: 2, y: 11, voltage: 6.0);
        vsrc.Pins[0].NodeId = 1; // + 6V
        vsrc.Pins[1].NodeId = 0; // - GND
        circuit.AddDevice(vsrc);

        // 2. NE555 Precision Timer IC
        var timer = new Timer555Device(2, x: 8, y: 13);
        timer.Pins[0].NodeId = 0; // GND
        timer.Pins[1].NodeId = 2; // TRIG
        timer.Pins[2].NodeId = 3; // OUT
        timer.Pins[3].NodeId = 1; // RST
        timer.Pins[4].NodeId = 4; // CTRL
        timer.Pins[5].NodeId = 2; // THR
        timer.Pins[6].NodeId = 5; // DIS
        timer.Pins[7].NodeId = 1; // VCC
        circuit.AddDevice(timer);

        // 3. Timing Ladder (1 kΩ + two 500 Ω resistors in series/parallel)
        var rTiming1 = new ResistorDevice(3, x: 5.5, y: 11, resistance: 1000.0);
        rTiming1.Pins[0].NodeId = 1;
        rTiming1.Pins[1].NodeId = 5;
        circuit.AddDevice(rTiming1);

        var rTiming2 = new ResistorDevice(4, x: 5, y: 13.5, resistance: 500.0);
        rTiming2.Pins[0].NodeId = 5;
        rTiming2.Pins[1].NodeId = 2;
        circuit.AddDevice(rTiming2);

        var rTiming3 = new ResistorDevice(5, x: 6.5, y: 13.5, resistance: 500.0);
        rTiming3.Pins[0].NodeId = 5;
        rTiming3.Pins[1].NodeId = 2;
        circuit.AddDevice(rTiming3);

        // 4. Timing & Filter Capacitors
        var cTiming = new CapacitorDevice(6, x: 2, y: 14, capacitance: 13.9e-9);
        cTiming.Pins[0].NodeId = 2;
        cTiming.Pins[1].NodeId = 0;
        circuit.AddDevice(cTiming);

        var cBypass = new CapacitorDevice(7, x: 10.5, y: 14, capacitance: 873e-12);
        cBypass.Pins[0].NodeId = 4;
        cBypass.Pins[1].NodeId = 0;
        circuit.AddDevice(cBypass);

        // Output damping resistor
        var rOut = new ResistorDevice(8, x: 11.5, y: 12, resistance: 100.0);
        rOut.Pins[0].NodeId = 3;
        rOut.Pins[1].NodeId = 3;
        circuit.AddDevice(rOut);

        // 5. Diagnostic LEDs along top rail
        // Green LED + 99.1 Ω (carries 21.5 mA)
        var rLedG = new ResistorDevice(9, x: 13, y: 4, resistance: 99.1);
        rLedG.Pins[0].NodeId = 1;
        rLedG.Pins[1].NodeId = 7;
        circuit.AddDevice(rLedG);

        var ledG = new LedDevice(10, x: 14.5, y: 4, wavelength: 530.0);
        ledG.Pins[0].NodeId = 7;
        ledG.Pins[1].NodeId = 0;
        circuit.AddDevice(ledG);

        // Amber LED + 100 Ω (carries 1.37 µA)
        var rLedA = new ResistorDevice(11, x: 16.5, y: 4, resistance: 100.0);
        rLedA.Pins[0].NodeId = 1;
        rLedA.Pins[1].NodeId = 8;
        circuit.AddDevice(rLedA);

        var ledA = new LedDevice(12, x: 18, y: 4, wavelength: 585.0);
        ledA.Pins[0].NodeId = 8;
        ledA.Pins[1].NodeId = 0;
        circuit.AddDevice(ledA);

        // Red LED + 100 Ω (carries 39.3 mA)
        var rLedR = new ResistorDevice(13, x: 20, y: 4, resistance: 100.0);
        rLedR.Pins[0].NodeId = 1;
        rLedR.Pins[1].NodeId = 9;
        circuit.AddDevice(rLedR);

        var ledR = new LedDevice(14, x: 21.5, y: 4, wavelength: 650.0);
        ledR.Pins[0].NodeId = 9;
        ledR.Pins[1].NodeId = 0;
        circuit.AddDevice(ledR);

        // 6. Dual 74193 4-Bit Synchronous Binary Counters
        var counter1 = new BinaryCounterDevice(15, x: 8, y: 6) { CounterValue = 10 };
        counter1.Pins[0].NodeId = 3; // CLK / UP driven by 555
        counter1.Pins[2].NodeId = 0; // RESET = GND
        counter1.Pins[3].NodeId = 30; // Q0
        counter1.Pins[4].NodeId = 31; // Q1
        counter1.Pins[5].NodeId = 32; // Q2
        counter1.Pins[6].NodeId = 33; // Q3
        circuit.AddDevice(counter1);

        var counter2 = new BinaryCounterDevice(16, x: 16, y: 13) { CounterValue = 4 };
        counter2.Pins[0].NodeId = 3; // CLK
        counter2.Pins[2].NodeId = 0; // RESET = GND
        counter2.Pins[3].NodeId = 50; // Q0
        counter2.Pins[4].NodeId = 51; // Q1
        counter2.Pins[5].NodeId = 52; // Q2
        counter2.Pins[6].NodeId = 53; // Q3
        circuit.AddDevice(counter2);

        // 7. Dual 7448 BCD-to-7-Segment Decoders
        var decoder1 = new BcdTo7SegmentDevice(17, x: 13, y: 6);
        decoder1.Pins[0].NodeId = 30; // A <- Q0
        decoder1.Pins[1].NodeId = 31; // B <- Q1
        decoder1.Pins[2].NodeId = 32; // C <- Q2
        decoder1.Pins[3].NodeId = 33; // D <- Q3
        decoder1.Pins[4].NodeId = 40; // a
        decoder1.Pins[5].NodeId = 41; // b
        decoder1.Pins[6].NodeId = 42; // c
        decoder1.Pins[7].NodeId = 43; // d
        decoder1.Pins[8].NodeId = 44; // e
        decoder1.Pins[9].NodeId = 45; // f
        decoder1.Pins[10].NodeId = 46; // g
        circuit.AddDevice(decoder1);

        var decoder2 = new BcdTo7SegmentDevice(18, x: 21, y: 13);
        decoder2.Pins[0].NodeId = 50; // A <- Q0
        decoder2.Pins[1].NodeId = 51; // B <- Q1
        decoder2.Pins[2].NodeId = 52; // C <- Q2
        decoder2.Pins[3].NodeId = 53; // D <- Q3
        decoder2.Pins[4].NodeId = 60; // a
        decoder2.Pins[5].NodeId = 61; // b
        decoder2.Pins[6].NodeId = 62; // c
        decoder2.Pins[7].NodeId = 63; // d
        decoder2.Pins[8].NodeId = 64; // e
        decoder2.Pins[9].NodeId = 65; // f
        decoder2.Pins[10].NodeId = 66; // g
        circuit.AddDevice(decoder2);

        // 8. Dual High-Fidelity Illuminated 7-Segment Displays
        // Display 1: Character 'A' in radiant glowing emerald green
        var displayA = new SevenSegmentDisplayDevice(19, x: 18, y: 6)
        {
            ManualHexValue = 10, // 'A'
            LedColor = "green"
        };
        displayA.Pins[0].NodeId = 40; // a
        displayA.Pins[1].NodeId = 41; // b
        displayA.Pins[2].NodeId = 42; // c
        displayA.Pins[3].NodeId = 43; // d
        displayA.Pins[4].NodeId = 44; // e
        displayA.Pins[5].NodeId = 45; // f
        displayA.Pins[6].NodeId = 46; // g
        displayA.Pins[7].NodeId = 0; // COM cathode to GND
        circuit.AddDevice(displayA);

        // Display 2: Numeral '4' in radiant glowing emerald green
        var display4 = new SevenSegmentDisplayDevice(20, x: 26, y: 6)
        {
            ManualHexValue = 4, // '4'
            LedColor = "green"
        };
        display4.Pins[0].NodeId = 60; // a
        display4.Pins[1].NodeId = 61; // b
        display4.Pins[2].NodeId = 62; // c
        display4.Pins[3].NodeId = 63; // d
        display4.Pins[4].NodeId = 64; // e
        display4.Pins[5].NodeId = 65; // f
        display4.Pins[6].NodeId = 66; // g
        display4.Pins[7].NodeId = 0; // COM cathode to GND
        circuit.AddDevice(display4);

        // Ground Reference
        var gnd = new GroundDevice(21, x: 8, y: 16);
        gnd.Pins[0].NodeId = 0;
        circuit.AddDevice(gnd);

        circuit.EnsureWiresSynthesized();

        return circuit;
    }
}



