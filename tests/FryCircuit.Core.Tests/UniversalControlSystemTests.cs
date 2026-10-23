using System;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.IO;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class UniversalControlSystemTests
{
    [Fact]
    public void RampSourceDevice_GeneratesCorrectRampInMna()
    {
        var circuit = new Circuit("Ramp Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);

        var ramp = new RampSourceDevice(1, 0, 0)
        {
            Slope = 2.5,
            StartTime = 0.1,
            InitialValue = 1.0
        };
        ramp.Pins[0].NodeId = 1;
        ramp.Pins[1].NodeId = 0;
        circuit.AddDevice(ramp);

        var solver = new MnaSolver(circuit);

        // At t = 0.05s (before start time): V = 1.0V
        double dt = 0.01;
        for (int i = 0; i < 5; i++) solver.Step(dt);
        Assert.Equal(1.0, circuit.Nodes[1].Voltage, 2);

        // At t = 0.5s: V = 1.0 + 2.5 * (0.5 - 0.1) = 1.0 + 1.0 = 2.0V
        for (int i = 5; i <= 50; i++) solver.Step(dt);
        Assert.Equal(2.0, circuit.Nodes[1].Voltage, 2);
    }

    [Fact]
    public void ConstantAndPulseSourceDevices_SimulateInMna()
    {
        var circuit = new Circuit("Sources Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1);
        circuit.GetOrCreateNode(2);

        var cSource = new ConstantSourceDevice(1, 0, 0) { Value = 3.3 };
        cSource.Pins[0].NodeId = 1;
        cSource.Pins[1].NodeId = 0;
        circuit.AddDevice(cSource);

        var pulse = new PulseSourceDevice(2, 0, 0)
        {
            LowValue = 0.0,
            HighValue = 5.0,
            Period = 0.1,
            DutyCycle = 0.5,
            Delay = 0.0
        };
        pulse.Pins[0].NodeId = 2;
        pulse.Pins[1].NodeId = 0;
        circuit.AddDevice(pulse);

        var solver = new MnaSolver(circuit);
        solver.Step(0.01);

        // Constant source should be 3.3V
        Assert.Equal(3.3, circuit.Nodes[1].Voltage, 2);
        // Pulse should be High (5.0V) during first 50ms of 100ms period
        Assert.Equal(5.0, circuit.Nodes[2].Voltage, 2);

        // Step to t = 70ms: pulse should be Low (0.0V)
        for (int i = 0; i < 6; i++) solver.Step(0.01);
        Assert.Equal(0.0, circuit.Nodes[2].Voltage, 2);
    }

    [Fact]
    public void MultiplierAndDividerDevices_ComputeAlgebraicOperationsInMna()
    {
        var circuit = new Circuit("Mult Div Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 4V
        circuit.GetOrCreateNode(2); // 2V
        circuit.GetOrCreateNode(3); // Mult Out (8V)
        circuit.GetOrCreateNode(4); // Div Out (2V)

        var v1 = new VoltageSourceDevice(1, 0, 0, 4.0);
        v1.Pins[0].NodeId = 1;
        v1.Pins[1].NodeId = 0;
        circuit.AddDevice(v1);

        var v2 = new VoltageSourceDevice(2, 0, 0, 2.0);
        v2.Pins[0].NodeId = 2;
        v2.Pins[1].NodeId = 0;
        circuit.AddDevice(v2);

        var mult = new MultiplierDevice(3, 0, 0);
        mult.Pins[0].NodeId = 1; // In A (4V)
        mult.Pins[1].NodeId = 2; // In B (2V)
        mult.Pins[2].NodeId = 3; // Out
        mult.Pins[3].NodeId = 0; // Ref
        circuit.AddDevice(mult);

        var div = new DividerDevice(4, 0, 0);
        div.Pins[0].NodeId = 1; // Num (4V)
        div.Pins[1].NodeId = 2; // Den (2V)
        div.Pins[2].NodeId = 4; // Out
        div.Pins[3].NodeId = 0; // Ref
        circuit.AddDevice(div);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-3);

        Assert.Equal(8.0, circuit.Nodes[3].Voltage, 2);
        Assert.Equal(2.0, circuit.Nodes[4].Voltage, 2);
    }

    [Fact]
    public void MathExpressionDevice_EvaluatesNonlinearFunctionsInMna()
    {
        var circuit = new Circuit("Math Expr Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 0.5V
        circuit.GetOrCreateNode(2); // sin(u) out
        circuit.GetOrCreateNode(3); // u^2 + 2*u out

        var vIn = new VoltageSourceDevice(1, 0, 0, 0.5);
        vIn.Pins[0].NodeId = 1;
        vIn.Pins[1].NodeId = 0;
        circuit.AddDevice(vIn);

        var mathSin = new MathExpressionDevice(2, 0, 0) { Expression = "sin(u)" };
        mathSin.Pins[0].NodeId = 1;
        mathSin.Pins[1].NodeId = 2;
        mathSin.Pins[2].NodeId = 0;
        circuit.AddDevice(mathSin);

        var mathPoly = new MathExpressionDevice(3, 0, 0) { Expression = "u^2 + 2*u" };
        mathPoly.Pins[0].NodeId = 1;
        mathPoly.Pins[1].NodeId = 3;
        mathPoly.Pins[2].NodeId = 0;
        circuit.AddDevice(mathPoly);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-3);

        double expectedSin = Math.Sin(0.5);
        double expectedPoly = 0.5 * 0.5 + 2 * 0.5; // 0.25 + 1.0 = 1.25

        Assert.Equal(expectedSin, circuit.Nodes[2].Voltage, 2);
        Assert.Equal(expectedPoly, circuit.Nodes[3].Voltage, 2);
    }

    [Fact]
    public void DeadZoneAndRelayDevices_ExhibitNonlinearThresholdingInMna()
    {
        var circuit = new Circuit("Nonlinear Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Input
        circuit.GetOrCreateNode(2); // DeadZone Out
        circuit.GetOrCreateNode(3); // Relay Out

        var vIn = new VoltageSourceDevice(1, 0, 0, 0.5);
        vIn.Pins[0].NodeId = 1;
        vIn.Pins[1].NodeId = 0;
        circuit.AddDevice(vIn);

        // Dead zone [-1.0, 1.0] -> 0.5V is inside dead-band -> out = 0.0V
        var dz = new DeadZoneDevice(2, 0, 0) { Threshold = 1.0 };
        dz.Pins[0].NodeId = 1;
        dz.Pins[1].NodeId = 2;
        dz.Pins[2].NodeId = 0;
        circuit.AddDevice(dz);

        // Relay on at 0.4V, off at -0.4V -> 0.5V turns it on (+1.0V)
        var relay = new RelayDevice(3, 0, 0) { OnThreshold = 0.4, OffThreshold = -0.4, HighOutput = 1.0, LowOutput = -1.0 };
        relay.Pins[0].NodeId = 1;
        relay.Pins[1].NodeId = 3;
        relay.Pins[2].NodeId = 0;
        circuit.AddDevice(relay);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-3);

        Assert.Equal(0.0, circuit.Nodes[2].Voltage, 2);
        Assert.Equal(1.0, circuit.Nodes[3].Voltage, 2);
    }

    [Fact]
    public void StateSpaceBlockDevice_SimulatesHarmonicOscillatorRK4InMna()
    {
        // 2nd Order Harmonic Oscillator: d^2 y / dt^2 + omega^2 y = 0
        // Let omega = 2 rad/s. A = [0, 1; -4, 0], B = [0; 0], C = [1, 0], D = [0]
        var circuit = new Circuit("StateSpace Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // In (0V)
        circuit.GetOrCreateNode(2); // Out (y)

        var vIn = new VoltageSourceDevice(1, 0, 0, 0.0);
        vIn.Pins[0].NodeId = 1;
        vIn.Pins[1].NodeId = 0;
        circuit.AddDevice(vIn);

        var ss = new StateSpaceBlockDevice(2, 0, 0)
        {
            AString = "0, 1; -4, 0",
            BString = "0; 0",
            CString = "1, 0",
            DString = "0"
        };
        ss.Pins[0].NodeId = 1;
        ss.Pins[1].NodeId = 2;
        ss.Pins[2].NodeId = 0;
        circuit.AddDevice(ss);

        // Initialize state x = [1.0, 0.0]^T -> y(t) = cos(2t)
        ss.SetStates(new[] { 1.0, 0.0 });

        var solver = new MnaSolver(circuit);

        // Simulate to t = 0.5s with dt = 1ms
        double dt = 1e-3;
        for (int i = 0; i < 500; i++)
        {
            solver.Step(dt);
        }

        // Analytical solution: cos(2 * 0.5) = cos(1 rad) ~= 0.540302
        double expected = Math.Cos(1.0);
        double actual = circuit.Nodes[2].Voltage;
        Assert.Equal(expected, actual, 2);
    }

    [Fact]
    public void DcMotorPlantDevice_SimulatesSteadyStateSpeedInMna()
    {
        // Physical DC Motor parameters:
        // Ra = 1.0 Ohm, La = 0.005 H
        // Kt = 0.05 N*m/A, Ke = 0.05 V/(rad/s)
        // J = 0.001 kg*m^2, B = 0.002 N*m*s
        //
        // Steady-state:
        // Va = Ra * Ia + Ke * omega
        // Kt * Ia = B * omega + T_load (with T_load = 0)
        // -> Ia = (B / Kt) * omega
        // -> Va = (Ra * B / Kt + Ke) * omega
        // -> omega_ss = Va / (Ke + Ra * B / Kt)
        // For Va = 12V:
        // omega_ss = 12 / (0.05 + 1.0 * 0.002 / 0.05) = 12 / (0.05 + 0.04) = 12 / 0.09 = 133.33 rad/s
        var circuit = new Circuit("DC Motor Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 12V Battery
        circuit.GetOrCreateNode(2); // Speed w
        circuit.GetOrCreateNode(3); // Angle theta
        circuit.GetOrCreateNode(4); // Load Torque T_L (0V)

        var battery = new VoltageSourceDevice(1, 0, 0, 12.0);
        battery.Pins[0].NodeId = 1;
        battery.Pins[1].NodeId = 0;
        circuit.AddDevice(battery);

        var motor = new DcMotorPlantDevice(2, 0, 0)
        {
            ResistanceRa = 1.0,
            InductanceLa = 0.005,
            TorqueConstantKt = 0.05,
            BackEmfConstantKe = 0.05,
            RotorInertiaJ = 0.001,
            ViscousDampingB = 0.002
        };
        motor.Pins[0].NodeId = 1; // Armature +
        motor.Pins[1].NodeId = 0; // Armature -
        motor.Pins[2].NodeId = 0; // Load Torque T_L (0 N*m)
        motor.Pins[3].NodeId = 2; // Output Speed w
        motor.Pins[4].NodeId = 3; // Output Angle theta
        motor.Pins[5].NodeId = 0; // Ref ground
        circuit.AddDevice(motor);

        var solver = new MnaSolver(circuit);

        // Mechanical time constant tau_m = J / (B + Kt*Ke/Ra) = 0.001 / (0.002 + 0.0025) = 0.001 / 0.0045 ~= 0.22s
        // Simulate for 1.5 seconds (1500 steps of 1ms) to reach steady state
        double dt = 1e-3;
        for (int i = 0; i < 1500; i++)
        {
            solver.Step(dt);
        }

        double omegaActual = circuit.Nodes[2].Voltage;
        double omegaExpected = 12.0 / (0.05 + (1.0 * 0.002) / 0.05); // 133.33 rad/s

        // Verify speed output within 1% of analytical electromechanical value
        Assert.InRange(omegaActual, omegaExpected * 0.98, omegaExpected * 1.02);
    }

    [Fact]
    public void DiscreteControlDevices_SimulateZDomainBehaviorInMna()
    {
        var circuit = new Circuit("Discrete Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 5V DC
        circuit.GetOrCreateNode(2); // Delay Out
        circuit.GetOrCreateNode(3); // Discrete TF Out

        var vIn = new VoltageSourceDevice(1, 0, 0, 5.0);
        vIn.Pins[0].NodeId = 1;
        vIn.Pins[1].NodeId = 0;
        circuit.AddDevice(vIn);

        // Unit delay with Ts = 0.01s
        var delay = new UnitDelayDevice(2, 0, 0) { SampleTime = 0.01 };
        delay.Pins[0].NodeId = 1;
        delay.Pins[1].NodeId = 2;
        delay.Pins[2].NodeId = 0;
        circuit.AddDevice(delay);

        // Discrete TF accumulator: H(z) = 0.1 / (1 - 0.9 z^-1)
        // Steady-state for 5V step: 5 * 0.1 / (1 - 0.9) = 5.0V
        var dtf = new DiscreteTransferFunctionDevice(3, 0, 0)
        {
            SampleTime = 0.01,
            NumeratorString = "0.1",
            DenominatorString = "1, -0.9"
        };
        dtf.Pins[0].NodeId = 1;
        dtf.Pins[1].NodeId = 3;
        dtf.Pins[2].NodeId = 0;
        circuit.AddDevice(dtf);

        var solver = new MnaSolver(circuit);

        // Step 1: At t = 1ms (before Ts = 10ms), delay output is initial 0V
        solver.Step(1e-3);
        Assert.Equal(0.0, circuit.Nodes[2].Voltage, 2);

        // Simulate 0.5s (50 samples)
        for (int i = 0; i < 500; i++) solver.Step(1e-3);

        // Delay should now follow input 5.0V
        Assert.Equal(5.0, circuit.Nodes[2].Voltage, 2);
        // Discrete TF should converge near 5.0V
        Assert.InRange(circuit.Nodes[3].Voltage, 4.8, 5.1);
    }

    [Fact]
    public void LlmCircuitSerializer_RoundtripsAllUniversalControlBlocks()
    {
        var original = new Circuit("Universal Control System");
        original.GetOrCreateNode(0, isGround: true);
        original.GetOrCreateNode(1);
        original.GetOrCreateNode(2);
        original.GetOrCreateNode(3);

        var ss = new StateSpaceBlockDevice(1, 10, 20)
        {
            AString = "0, 1; -5, -2",
            BString = "0; 5",
            CString = "1, 0",
            DString = "0"
        };
        ss.Pins[0].NodeId = 1;
        ss.Pins[1].NodeId = 2;
        ss.Pins[2].NodeId = 0;
        original.AddDevice(ss);

        var math = new MathExpressionDevice(2, 30, 40)
        {
            Expression = "tanh(u * 2.5)"
        };
        math.Pins[0].NodeId = 2;
        math.Pins[1].NodeId = 3;
        math.Pins[2].NodeId = 0;
        original.AddDevice(math);

        var motor = new DcMotorPlantDevice(3, 50, 60)
        {
            ResistanceRa = 1.5,
            TorqueConstantKt = 0.08
        };
        motor.Pins[0].NodeId = 3;
        motor.Pins[1].NodeId = 0;
        motor.Pins[2].NodeId = 0;
        motor.Pins[3].NodeId = 1;
        motor.Pins[4].NodeId = 0;
        motor.Pins[5].NodeId = 0;
        original.AddDevice(motor);

        string json = LlmCircuitSerializer.Serialize(original);
        var restored = LlmCircuitSerializer.Deserialize(json);

        Assert.Equal(3, restored.Devices.Count);

        var restoredSs = restored.Devices.Find(d => d is StateSpaceBlockDevice) as StateSpaceBlockDevice;
        Assert.NotNull(restoredSs);
        Assert.Equal("0, 1; -5, -2", restoredSs.AString);
        Assert.Equal("0; 5", restoredSs.BString);

        var restoredMath = restored.Devices.Find(d => d is MathExpressionDevice) as MathExpressionDevice;
        Assert.NotNull(restoredMath);
        Assert.Equal("tanh(u * 2.5)", restoredMath.Expression);

        var restoredMotor = restored.Devices.Find(d => d is DcMotorPlantDevice) as DcMotorPlantDevice;
        Assert.NotNull(restoredMotor);
        Assert.Equal(1.5, restoredMotor.ResistanceRa, 3);
        Assert.Equal(0.08, restoredMotor.TorqueConstantKt, 3);
    }
}
