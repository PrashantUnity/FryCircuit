using System;
using System.Collections.Generic;
using System.Linq;

namespace FryCircuit.Core.Control;

public record StepResponsePoint(
    double Time,
    double Value
);

public record StepResponseResult(
    IReadOnlyList<StepResponsePoint> Points,
    double RiseTime,
    double PeakTime,
    double PeakValue,
    double OvershootPercent,
    double SettlingTime,
    double SteadyStateValue,
    double SteadyStateError
);

public static class StepResponseAnalyzer
{
    public static StepResponseResult Analyze(
        TransferFunction tf,
        double? customDuration = null,
        int numPoints = 600)
    {
        if (numPoints < 50) numPoints = 50;

        // Estimate simulation horizon based on dominant pole
        double tFinal;
        if (customDuration.HasValue && customDuration.Value > 0)
        {
            tFinal = customDuration.Value;
        }
        else
        {
            var poles = tf.Poles;
            double minAbsReal = 1.0;
            var stablePoles = poles.Where(p => p.Real < -1e-6).ToList();
            if (stablePoles.Count > 0)
            {
                minAbsReal = stablePoles.Min(p => Math.Abs(p.Real));
            }
            tFinal = Math.Clamp(6.0 / minAbsReal, 0.1, 100.0);
        }

        double dt = tFinal / (numPoints - 1);
        var ss = tf.ToStateSpace();

        var points = new List<StepResponsePoint>(numPoints);
        double peakVal = double.MinValue;
        double peakTime = 0.0;

        for (int i = 0; i < numPoints; i++)
        {
            double t = i * dt;
            double y = ss.Step(1.0, dt);
            points.Add(new StepResponsePoint(t, y));

            if (y > peakVal)
            {
                peakVal = y;
                peakTime = t;
            }
        }

        // Steady-state value: average of last 5% of samples or DC gain
        double ssVal = tf.IsStable ? tf.DcGain : points[^1].Value;
        if (double.IsNaN(ssVal) || double.IsInfinity(ssVal))
        {
            ssVal = points[^1].Value;
        }

        double ssErr = Math.Abs(1.0 - ssVal);

        // Percent Overshoot:
        double osPercent = 0.0;
        if (peakVal > ssVal && Math.Abs(ssVal) > 1e-6)
        {
            osPercent = ((peakVal - ssVal) / Math.Abs(ssVal)) * 100.0;
        }

        // Rise Time (10% to 90% of ssVal)
        double target10 = 0.10 * ssVal;
        double target90 = 0.90 * ssVal;
        double t10 = -1.0;
        double t90 = -1.0;

        for (int i = 0; i < points.Count; i++)
        {
            if (t10 < 0 && points[i].Value >= target10)
            {
                t10 = points[i].Time;
            }
            if (t90 < 0 && points[i].Value >= target90)
            {
                t90 = points[i].Time;
            }
        }
        double riseTime = (t10 >= 0 && t90 >= t10) ? (t90 - t10) : 0.0;

        // Settling Time (2% error band: [0.98 * ssVal, 1.02 * ssVal])
        double band2 = 0.02 * Math.Abs(ssVal);
        double settlingTime = tFinal;
        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(points[i].Value - ssVal) > band2)
            {
                settlingTime = points[Math.Min(i + 1, points.Count - 1)].Time;
                break;
            }
        }

        return new StepResponseResult(
            points,
            riseTime,
            peakTime,
            peakVal,
            osPercent,
            settlingTime,
            ssVal,
            ssErr
        );
    }
}
