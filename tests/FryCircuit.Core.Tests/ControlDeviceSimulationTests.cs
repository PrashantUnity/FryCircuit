using System;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class ControlDeviceSimulationTests
{
    [Fact]
    public void TransferFunctionDevice_SimulatesStepResponseInMna()
    {
        var circuit = new Circuit("TF Step Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Step in
        circuit.GetOrCreateNode(2); // Filtered out

        var step = new StepSourceDevice(1, x: 0, y: 0)
        {
            StepTime = 0.0,
            InitialValue = 0.0,
            FinalValue = 1.0
        };
        step.Pins[0].NodeId = 1;
        step.Pins[1].NodeId = 0;
        circuit.AddDevice(step);

        // G(s) = 1 / (s + 1)
        var tf = new TransferFunctionDevice(2, x: 5, y: 0)
        {
            NumeratorString = "1",
            DenominatorString = "1, 1"
        };
        tf.Pins[0].NodeId = 1;
        tf.Pins[1].NodeId = 2;
        tf.Pins[2].NodeId = 0;
        circuit.AddDevice(tf);

        var solver = new MnaSolver(circuit);

        // Simulate 1.0 second (1000 steps of 1ms)
        double dt = 1e-3;
        for (int i = 0; i < 1000; i++)
        {
            solver.Step(dt);
        }

        // Expected output at t = 1.0s: 1 - exp(-1) ~= 0.63212 V
        double vOut = circuit.Nodes[2].Voltage;
        double expected = 1.0 - Math.Exp(-1.0);
        Assert.Equal(expected, vOut, 2);
    }

    [Fact]
    public void SummingJunctionDevice_ComputesDifferenceInMna()
    {
        var circuit = new Circuit("Sum Junction Test");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // 5V
        circuit.GetOrCreateNode(2); // 2V
        circuit.GetOrCreateNode(3); // Result (5 - 2 = 3V)

        var v1 = new VoltageSourceDevice(1, 0, 0, 5.0);
        v1.Pins[0].NodeId = 1;
        v1.Pins[1].NodeId = 0;
        circuit.AddDevice(v1);

        var v2 = new VoltageSourceDevice(2, 0, 0, 2.0);
        v2.Pins[0].NodeId = 2;
        v2.Pins[1].NodeId = 0;
        circuit.AddDevice(v2);

        var sum = new SummingJunctionDevice(3, 0, 0)
        {
            Signs = "+-"
        };
        sum.Pins[0].NodeId = 1; // +5V
        sum.Pins[1].NodeId = 2; // -2V
        sum.Pins[3].NodeId = 3; // Out
        sum.Pins[4].NodeId = 0; // Ref
        circuit.AddDevice(sum);

        var solver = new MnaSolver(circuit);
        solver.Step(1e-3);

        double vOut = circuit.Nodes[3].Voltage;
        Assert.Equal(3.0, vOut, 2);
    }

    [Fact]
    public void BlockDiagramFeedbackReduction_LoopMatchesEquivalentTransferFunction()
    {
        // Block diagram feedback reduction benchmark:
        // Branch A (Loop):
        //   Step (1V) -> Sum (+, -) -> Forward TF G1 = 1/(s+1) -> Node 3 (Output A)
        //   Feedback: from Node 3 -> Feedback TF G2 = 1/(s+2) -> into Sum '-' input
        //
        // Branch B (Single Equivalent Transfer Function):
        //   With negative feedback: T(s) = G1 / (1 + G1*G2) = (s + 2) / (s^2 + 3s + 3)
        //   Step (1V) -> Equivalent TF -> Node 6 (Output B)

        var circuit = new Circuit("Block Diagram Equivalence");
        circuit.GetOrCreateNode(0, isGround: true);
        circuit.GetOrCreateNode(1); // Step input (1V)
        circuit.GetOrCreateNode(2); // Sum output
        circuit.GetOrCreateNode(3); // Loop output A
        circuit.GetOrCreateNode(4); // Feedback output
        circuit.GetOrCreateNode(5); // Equivalent TF output B

        var step = new StepSourceDevice(1, 0, 0)
        {
            StepTime = 0.0,
            InitialValue = 0.0,
            FinalValue = 1.0
        };
        step.Pins[0].NodeId = 1;
        step.Pins[1].NodeId = 0;
        circuit.AddDevice(step);

        // Branch A: Summing junction (Signs: "+-")
        var sum = new SummingJunctionDevice(2, 0, 0) { Signs = "+-" };
        sum.Pins[0].NodeId = 1; // Reference r
        sum.Pins[1].NodeId = 4; // Feedback y_fb
        sum.Pins[3].NodeId = 2; // Error / Sum out
        sum.Pins[4].NodeId = 0; // Ground
        circuit.AddDevice(sum);

        // Forward: 1 / (s + 1)
        var g1 = new TransferFunctionDevice(3, 0, 0)
        {
            NumeratorString = "1",
            DenominatorString = "1, 1"
        };
        g1.Pins[0].NodeId = 2;
        g1.Pins[1].NodeId = 3;
        g1.Pins[2].NodeId = 0;
        circuit.AddDevice(g1);

        // Feedback: 1 / (s + 2)
        var g2 = new TransferFunctionDevice(4, 0, 0)
        {
            NumeratorString = "1",
            DenominatorString = "1, 2"
        };
        g2.Pins[0].NodeId = 3;
        g2.Pins[1].NodeId = 4;
        g2.Pins[2].NodeId = 0;
        circuit.AddDevice(g2);

        // Branch B: Equivalent TF = (s + 2) / (s^2 + 3s + 3)
        var tfEq = new TransferFunctionDevice(5, 0, 0)
        {
            NumeratorString = "1, 2",
            DenominatorString = "1, 3, 3"
        };
        tfEq.Pins[0].NodeId = 1;
        tfEq.Pins[1].NodeId = 5;
        tfEq.Pins[2].NodeId = 0;
        circuit.AddDevice(tfEq);

        var solver = new MnaSolver(circuit);

        // Step simulation for 1000 steps (1.0 second)
        double dt = 1e-3;
        for (int i = 0; i < 1000; i++)
        {
            solver.Step(dt);

            if (i > 100)
            {
                double vLoop = circuit.Nodes[3].Voltage;
                double vEq = circuit.Nodes[5].Voltage;
                // Both branches should track with high fidelity
                Assert.True(Math.Abs(vLoop - vEq) < 0.05, $"Mismatch at step {i}: Loop={vLoop}, Eq={vEq}");
            }
        }
    }
}
