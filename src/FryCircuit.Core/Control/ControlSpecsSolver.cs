using System;

namespace FryCircuit.Core.Control;

public record SecondOrderSpecsResult(
    double DampingRatioZeta,
    double NaturalFrequencyWn,
    double PercentOvershoot,
    double SettlingTime2Percent,
    double PeakTime,
    double RiseTime
);

public record SteadyStateErrorResult(
    double PositionErrorConstantKp,
    double VelocityErrorConstantKv,
    double AccelerationErrorConstantKa,
    double StepSteadyStateError,
    double RampSteadyStateError,
    double DisturbanceSteadyStateError
);

public record Problem4SpecsSolution(
    double DampingRatioZeta,
    double NaturalFrequencyWn,
    double RequiredGainK,
    double ParameterA,
    double DampedFrequencyWd,
    double PeakTimeTp,
    System.Numerics.Complex Pole1,
    System.Numerics.Complex Pole2
);

public record Problem5SpeedPlantSolution(
    double ClosedLoopPoleReal,
    double LowFrequencySensitivity,
    double SteadyStateErrorStep,
    double SteadyStateErrorDisturbance
);

public static class ControlSpecsSolver
{
    /// <summary>
    /// Computes analytical second-order specifications for:
    /// T(s) = wn^2 / (s^2 + 2*zeta*wn*s + wn^2)
    /// </summary>
    public static SecondOrderSpecsResult SolveSecondOrder(double zeta, double wn)
    {
        if (wn <= 0) wn = 1.0;
        zeta = Math.Max(0.0, zeta);

        double os = 0.0;
        double tp = 0.0;
        double tr = 0.0;
        double ts = 0.0;

        if (zeta < 1.0)
        {
            // Underdamped
            double wd = wn * Math.Sqrt(1.0 - zeta * zeta);
            os = Math.Exp(-zeta * Math.PI / Math.Sqrt(1.0 - zeta * zeta)) * 100.0;
            tp = Math.PI / wd;
            double beta = Math.Atan2(Math.Sqrt(1.0 - zeta * zeta), zeta);
            tr = (Math.PI - beta) / wd;
            ts = 4.0 / (zeta * wn); // 2% settling time criterion
        }
        else if (Math.Abs(zeta - 1.0) < 1e-6)
        {
            // Critically damped
            os = 0.0;
            tp = double.PositiveInfinity;
            tr = 3.36 / wn;
            ts = 5.83 / wn;
        }
        else
        {
            // Overdamped
            os = 0.0;
            double r1 = -zeta * wn + wn * Math.Sqrt(zeta * zeta - 1.0); // dominant pole
            ts = 4.0 / Math.Abs(r1);
            tr = (1.0 + 1.1 * zeta + 1.4 * zeta * zeta) / wn;
        }

        return new SecondOrderSpecsResult(zeta, wn, os, ts, tp, tr);
    }

    /// <summary>
    /// Problem 4 Solver:
    /// T(s) = K / (s^2 + 6s + K)
    /// Here 2*zeta*wn = 6 => zeta*wn = 3, wn = sqrt(K), zeta = 3 / sqrt(K).
    /// </summary>
    public static SecondOrderSpecsResult SolveProblem4PartA(double K = 100.0)
    {
        double wn = Math.Sqrt(K);
        double zeta = 3.0 / wn;
        return SolveSecondOrder(zeta, wn);
    }

    /// <summary>
    /// Problem 4 Part (b):
    /// Finds max K such that Percent Overshoot &lt; desiredOvershootPercent (e.g. 15%).
    /// %OS &lt;= target => zeta &gt;= ln(OS/100) / sqrt(pi^2 + ln(OS/100)^2).
    /// Since zeta = 3 / sqrt(K), K &lt;= (3 / zeta_target)^2.
    /// </summary>
    public static (double RequiredZeta, double MaxAllowableK) SolveProblem4PartB(double desiredOvershootPercent = 15.0)
    {
        double osFrac = desiredOvershootPercent / 100.0;
        double lnOS = Math.Log(osFrac);
        double requiredZeta = Math.Abs(lnOS) / Math.Sqrt(Math.PI * Math.PI + lnOS * lnOS);
        double maxK = Math.Pow(3.0 / requiredZeta, 2.0);
        return (requiredZeta, maxK);
    }

    /// <summary>
    /// Problem 5 Steady-State Errors:
    /// Plant G(s) = 1 / (s * (s + 2)), Controller Gc(s) = K.
    /// </summary>
    public static SteadyStateErrorResult SolveProblem5SteadyStateErrors(double K)
    {
        // Loop gain L(s) = K / (s * (s + 2))
        // Type 1 system:
        // Kp = lim_{s->0} L(s) = infinity => e_ss(step) = 0
        // Kv = lim_{s->0} s * L(s) = K / 2 => e_ss(ramp) = 1 / Kv = 2 / K
        // Ka = lim_{s->0} s^2 * L(s) = 0 => e_ss(parabola) = infinity
        double kp = double.PositiveInfinity;
        double kv = K / 2.0;
        double ka = 0.0;

        double essStep = 0.0;
        double essRamp = kv > 1e-12 ? 1.0 / kv : double.PositiveInfinity;

        // Disturbance tracking error:
        // Y(s) = (G(s) / (1 + K*G(s))) * D(s)
        // With D(s) = 1/s: y_ss = lim_{s->0} s * (1/(s(s+2))) / (1 + K/(s(s+2))) * (1/s)
        //       y_ss = 1 / K
        // e_ss = -y_ss = -1/K
        double essDist = (Math.Abs(K) > 1e-12) ? (1.0 / K) : double.PositiveInfinity;

        return new SteadyStateErrorResult(kp, kv, ka, essStep, essRamp, essDist);
    }

    public static Problem4SpecsSolution SolveProblem4Specs(double desiredOvershootPercent, double desiredSettlingTime)
    {
        double os = Math.Clamp(desiredOvershootPercent, 0.01, 99.9);
        double ts = Math.Max(desiredSettlingTime, 0.01);

        double osFrac = os / 100.0;
        double lnOs = Math.Log(osFrac);
        double zeta = Math.Abs(lnOs) / Math.Sqrt(Math.PI * Math.PI + lnOs * lnOs);

        double wn = 4.0 / (zeta * ts);
        double K = wn * wn;
        double a = 2.0 * zeta * wn;
        double wd = wn * Math.Sqrt(Math.Max(0.0, 1.0 - zeta * zeta));
        double tp = wd > 1e-9 ? Math.PI / wd : 0.0;

        var p1 = new System.Numerics.Complex(-zeta * wn, wd);
        var p2 = new System.Numerics.Complex(-zeta * wn, -wd);

        return new Problem4SpecsSolution(zeta, wn, K, a, wd, tp, p1, p2);
    }

    public static Problem5SpeedPlantSolution SolveProblem5SpeedPlant(double poleA, double gainK)
    {
        double a = poleA;
        double K = gainK;
        double poleReal = a + K;
        double dcSens = Math.Abs(a + K) > 1e-12 ? Math.Abs(a / (a + K)) : 1.0;
        double essStep = Math.Abs(a + K) > 1e-12 ? Math.Abs(a / (a + K)) : 1.0;
        double essDist = Math.Abs(a + K) > 1e-12 ? Math.Abs(1.0 / (a + K)) : 1.0;

        return new Problem5SpeedPlantSolution(poleReal, dcSens, essStep, essDist);
    }
}
