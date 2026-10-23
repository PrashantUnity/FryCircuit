using System;
using System.Linq;
using System.Numerics;
using FryCircuit.Core.Control;
using Xunit;

namespace FryCircuit.Core.Tests;

public class TransferFunctionTests
{
    [Fact]
    public void FeedbackAlgebra_BlockDiagramReduction()
    {
        // Block diagram reduction:
        // Forward: G1(s) = 1 / (1 + s)
        // Feedback: G2(s) = 1 / (2 + s)
        // With positive feedback at sum:
        // T(s) = G1 / (1 - G1 * G2)
        // G1 * G2 = 1 / ((s + 1)(s + 2)) = 1 / (s^2 + 3s + 2)
        // 1 - G1*G2 = (s^2 + 3s + 1) / (s^2 + 3s + 2)
        // T(s) = (1 / (s + 1)) / ((s^2 + 3s + 1) / (s^2 + 3s + 2))
        //      = (s + 2) / (s^2 + 3s + 1)
        // Or with negative feedback:
        // T(s) = G1 / (1 + G1 * G2) = (s + 2) / (s^2 + 3s + 3)

        var g1 = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 1.0 });
        var g2 = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 2.0 });

        var clNegative = TransferFunction.Feedback(g1, g2, sign: -1);
        Assert.Equal(1, clNegative.Numerator.Degree);
        Assert.Equal(2, clNegative.Denominator.Degree);

        // Numerator should be s + 2
        Assert.Equal(1.0, clNegative.Numerator.Coefficients[0], 4);
        Assert.Equal(2.0, clNegative.Numerator.Coefficients[1], 4);

        // Denominator: s^2 + 3s + 3
        Assert.Equal(1.0, clNegative.Denominator.Coefficients[0], 4);
        Assert.Equal(3.0, clNegative.Denominator.Coefficients[1], 4);
        Assert.Equal(3.0, clNegative.Denominator.Coefficients[2], 4);
    }

    [Fact]
    public void StateSpaceRealization_And_StepSimulation()
    {
        // First order low-pass: G(s) = 1 / (s + 1)
        var tf = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 1.0 });
        var ss = tf.ToStateSpace();

        Assert.Equal(1, ss.StateCount);

        // Step response of 1/(s+1): y(t) = 1 - exp(-t)
        // Simulate for 1 second with dt = 1e-3
        double dt = 1e-3;
        double y = 0.0;
        for (int i = 0; i < 1000; i++)
        {
            y = ss.Step(1.0, dt);
        }

        double expected = 1.0 - Math.Exp(-1.0); // ~0.63212
        Assert.Equal(expected, y, 2);
    }

    [Fact]
    public void PolesAndZeros_Extraction()
    {
        // G(s) = (s + 5) / (s^2 + 4s + 13)
        // Zero at s = -5
        // Poles at s = -2 +/- 3j
        var tf = new TransferFunction(new[] { 1.0, 5.0 }, new[] { 1.0, 4.0, 13.0 });

        Assert.Single(tf.Zeros);
        Assert.Equal(-5.0, tf.Zeros[0].Real, 4);

        Assert.Equal(2, tf.Poles.Count);
        Assert.Contains(tf.Poles, p => Math.Abs(p.Real - (-2.0)) < 1e-3 && Math.Abs(Math.Abs(p.Imaginary) - 3.0) < 1e-3);
        Assert.True(tf.IsStable);
        Assert.True(tf.IsStrictlyProper);
    }
}
