using System;
using System.Linq;
using System.Numerics;
using FryCircuit.Core.Control;
using Xunit;

namespace FryCircuit.Core.Tests;

public class ControlLoopAnalyzerTests
{
    [Fact]
    public void BodeAnalyzer_ThirdOrderBenchmark_MatchesGainAndPhaseMargin()
    {
        // L(s) = 1 / (s * (s + 1) * (s + 2)) = 1 / (s^3 + 3s^2 + 2s)
        // Analytical results:
        // omega_pc = sqrt(2) ~= 1.414 rad/s
        // |L(j*sqrt(2))| = 1/6
        // Gain Margin = 20 * log10(6) ~= 15.56 dB

        var loopTf = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 3.0, 2.0, 0.0 });
        var bode = BodeAnalyzer.Analyze(loopTf, minOmega: 0.1, maxOmega: 10.0, numPoints: 800);

        Assert.NotNull(bode.PhaseCrossoverFrequency);
        Assert.Equal(Math.Sqrt(2.0), bode.PhaseCrossoverFrequency.Value, 0.05);

        Assert.NotNull(bode.GainMarginDb);
        double expectedGm = 20.0 * Math.Log10(6.0);
        Assert.Equal(expectedGm, bode.GainMarginDb.Value, 0.5);

        Assert.True(bode.IsStable);
    }

    [Fact]
    public void GangOfFour_SatisfiesSensitivityIdentity()
    {
        // Plant: P(s) = 1 / (s + 1)^2 = 1 / (s^2 + 2s + 1)
        // Controller: C(s) = 2 + 1/s = (2s + 1) / s
        var plant = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 2.0, 1.0 });
        var controller = new TransferFunction(new[] { 2.0, 1.0 }, new[] { 1.0, 0.0 });

        var gof = GangOfFourAnalyzer.Analyze(plant, controller, minOmega: 0.1, maxOmega: 100.0, numPoints: 100);

        // Verify fundamental Astrom & Murray identity: S(j*w) + T(j*w) == 1
        foreach (var w in new[] { 0.1, 0.5, 1.0, 2.0, 10.0, 50.0 })
        {
            Complex sVal = gof.Sensitivity.Evaluate(w);
            Complex tVal = gof.CompSensitivity.Evaluate(w);
            Complex sum = sVal + tVal;

            Assert.Equal(1.0, sum.Real, 4);
            Assert.Equal(0.0, sum.Imaginary, 4);
        }

        // Peak sensitivity Ms should be positive and reasonable (~1.0 to 2.5)
        Assert.True(gof.PeakSensitivityMs > 1.0);
        Assert.True(gof.PeakSensitivityMs < 3.0);
    }

    [Fact]
    public void Problem4_AnalyticalSpecVerification()
    {
        // Problem 4:
        // Y(s)/R(s) = K / (s^2 + 6s + K) with K = 100
        // 2*zeta*wn = 6 => wn = 10, zeta = 0.3
        // %OS = exp(-0.3*pi / sqrt(1 - 0.09)) * 100% = 37.23%
        // Ts (2%) = 4 / (zeta * wn) = 4 / 3 = 1.333s

        var p4a = ControlSpecsSolver.SolveProblem4PartA(100.0);
        Assert.Equal(0.3, p4a.DampingRatioZeta, 4);
        Assert.Equal(10.0, p4a.NaturalFrequencyWn, 4);
        Assert.Equal(37.23, p4a.PercentOvershoot, 0.1);
        Assert.Equal(1.333, p4a.SettlingTime2Percent, 0.01);

        // Part (b): %OS < 15%
        // Target zeta = 0.5169 => Max K = 33.68
        var (reqZeta, maxK) = ControlSpecsSolver.SolveProblem4PartB(15.0);
        Assert.Equal(0.517, reqZeta, 0.005);
        Assert.Equal(33.68, maxK, 0.5);

        // Verify with maxK that %OS is indeed ~15%
        var checkSpec = ControlSpecsSolver.SolveProblem4PartA(maxK);
        Assert.Equal(15.0, checkSpec.PercentOvershoot, 0.2);
    }

    [Fact]
    public void Problem5_SteadyStateErrorVerification()
    {
        // Problem 5:
        // Plant G(s) = 1 / (s * (s + 2)), Controller Gc(s) = K
        // e_ss(step) = 0
        // e_ss(ramp) = 2 / K
        // e_ss(dist) = 1 / K
        double K = 10.0;
        var p5 = ControlSpecsSolver.SolveProblem5SteadyStateErrors(K);

        Assert.Equal(0.0, p5.StepSteadyStateError, 6);
        Assert.Equal(2.0 / K, p5.RampSteadyStateError, 6); // 0.2
        Assert.Equal(1.0 / K, p5.DisturbanceSteadyStateError, 6); // 0.1
    }

    [Fact]
    public void Problem4Specs_SecondOrderDesign_ComputesExactGainsAndPoles()
    {
        // Spec: %OS = 16.3%, Ts = 8.0s
        // ln(0.163) = -1.814
        // zeta = 1.814 / sqrt(pi^2 + 1.814^2) = 1.814 / 3.628 ~= 0.50
        // wn = 4 / (zeta * Ts) = 4 / (0.5 * 8) = 1.0 rad/s
        // K = wn^2 = 1.0
        // a = 2 * zeta * wn = 1.0
        var sol = ControlSpecsSolver.SolveProblem4Specs(16.3, 8.0);

        Assert.Equal(0.50, sol.DampingRatioZeta, 0.01);
        Assert.Equal(1.00, sol.NaturalFrequencyWn, 0.02);
        Assert.Equal(1.00, sol.RequiredGainK, 0.04);
        Assert.Equal(1.00, sol.ParameterA, 0.04);
        Assert.Equal(Math.Sqrt(0.75), sol.DampedFrequencyWd, 0.02); // 0.866
        Assert.Equal(-0.50, sol.Pole1.Real, 0.02);
    }

    [Fact]
    public void Problem5SpeedPlant_ParameterSensitivity_ComputesCorrectExpressions()
    {
        // P(s) = 1 / (s + a), a = 2.0, K = 5.0
        // Closed loop pole s = -(a + K) = -7.0
        // Low freq sensitivity = a / (a + K) = 2/7 ~= 0.2857
        // Steady-state error = a / (a + K) = 2/7
        // Disturbance response = 1 / (a + K) = 1/7 ~= 0.1429
        var sol = ControlSpecsSolver.SolveProblem5SpeedPlant(2.0, 5.0);

        Assert.Equal(7.0, sol.ClosedLoopPoleReal, 0.001);
        Assert.Equal(2.0 / 7.0, sol.LowFrequencySensitivity, 0.001);
        Assert.Equal(2.0 / 7.0, sol.SteadyStateErrorStep, 0.001);
        Assert.Equal(1.0 / 7.0, sol.SteadyStateErrorDisturbance, 0.001);
    }

    [Fact]
    public void FeedbackEquivalence_ReducesCanonicalInnerLoop()
    {
        // G1(s) = 1 / (s + 1), G2(s) = 1 / (s + 2)
        // Closed loop: T(s) = G1 / (1 + G1*G2) = (s + 2) / (s^2 + 3s + 3)
        var g1 = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 1.0 });
        var g2 = new TransferFunction(new[] { 1.0 }, new[] { 1.0, 2.0 });
        var closed = TransferFunction.Feedback(g1, g2, -1);

        // Numerator should be proportional to s + 2
        Assert.Equal(1, closed.Numerator.Degree);
        // Denominator should be degree 2: s^2 + 3s + 3
        Assert.Equal(2, closed.Denominator.Degree);
        Assert.True(closed.IsStable);

        // Evaluate at s = 0: (0 + 2)/(0 + 0 + 3) = 2/3
        Assert.Equal(2.0 / 3.0, closed.DcGain, 0.001);
    }

    [Fact]
    public void NyquistAnalyzer_CauchyEncirclement_DeterminesClosedLoopStability()
    {
        // Open-loop stable: L(s) = 2 / (s + 1)^2
        // P = 0, critical point -1
        var loopTf = new TransferFunction(new[] { 2.0 }, new[] { 1.0, 2.0, 1.0 });
        var nyq = NyquistAnalyzer.Analyze(loopTf);

        Assert.Equal(0, nyq.OpenLoopRhpPoles);
        Assert.Equal(0, nyq.ClockwiseEncirclements);
        Assert.Equal(0, nyq.ClosedLoopRhpPoles);
        Assert.True(nyq.IsClosedLoopStable);
        Assert.True(nyq.DistanceToCriticalPoint > 0.0);
    }
}
