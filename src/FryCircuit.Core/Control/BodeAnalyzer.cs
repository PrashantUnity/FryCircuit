using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FryCircuit.Core.Control;

public record BodePoint(
    double Omega,
    double FrequencyHz,
    double MagnitudeLinear,
    double MagnitudeDb,
    double PhaseDeg
);

public record BodeResult(
    IReadOnlyList<BodePoint> Points,
    double? GainCrossoverFrequency,
    double? PhaseCrossoverFrequency,
    double? GainMarginDb,
    double? PhaseMarginDeg,
    double? DelayMarginSeconds,
    bool IsStable
);

public static class BodeAnalyzer
{
    public static BodeResult Analyze(
        TransferFunction tf,
        double minOmega = 1e-2,
        double maxOmega = 1e5,
        int numPoints = 400)
    {
        if (minOmega <= 0) minOmega = 1e-2;
        if (maxOmega <= minOmega) maxOmega = minOmega * 1000;
        if (numPoints < 20) numPoints = 20;

        double logMin = Math.Log10(minOmega);
        double logMax = Math.Log10(maxOmega);
        double logStep = (logMax - logMin) / (numPoints - 1);

        var points = new List<BodePoint>(numPoints);
        double prevPhaseRad = 0.0;
        double unwrappedPhaseRad = 0.0;

        for (int i = 0; i < numPoints; i++)
        {
            double omega = Math.Pow(10, logMin + i * logStep);
            Complex resp = tf.Evaluate(omega);

            double magLinear = resp.Magnitude;
            double magDb = 20.0 * Math.Log10(Math.Max(1e-12, magLinear));

            double rawPhase = Math.Atan2(resp.Imaginary, resp.Real);
            if (i == 0)
            {
                unwrappedPhaseRad = rawPhase;
            }
            else
            {
                double diff = rawPhase - prevPhaseRad;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff < -Math.PI) diff += 2 * Math.PI;
                unwrappedPhaseRad += diff;
            }
            prevPhaseRad = rawPhase;

            double phaseDeg = unwrappedPhaseRad * (180.0 / Math.PI);
            points.Add(new BodePoint(omega, omega / (2 * Math.PI), magLinear, magDb, phaseDeg));
        }

        // Crossover detection:
        // Gain Crossover Frequency (omega_gc): where magDb crosses 0 dB (|G| = 1)
        double? omegaGc = null;
        for (int i = 0; i < points.Count - 1; i++)
        {
            double m1 = points[i].MagnitudeDb;
            double m2 = points[i + 1].MagnitudeDb;
            if ((m1 >= 0 && m2 <= 0) || (m1 <= 0 && m2 >= 0))
            {
                double frac = Math.Abs(m1) / (Math.Abs(m1) + Math.Abs(m2) + 1e-15);
                double logW = Math.Log10(points[i].Omega) + frac * (Math.Log10(points[i + 1].Omega) - Math.Log10(points[i].Omega));
                omegaGc = Math.Pow(10, logW);
                break;
            }
        }

        // Phase Crossover Frequency (omega_pc): where phase crosses -180 deg (or odd multiples)
        double? omegaPc = null;
        for (int i = 0; i < points.Count - 1; i++)
        {
            double p1 = points[i].PhaseDeg;
            double p2 = points[i + 1].PhaseDeg;

            // Normalize around -180
            double target = -180.0;
            if ((p1 >= target && p2 <= target) || (p1 <= target && p2 >= target))
            {
                double frac = Math.Abs(p1 - target) / (Math.Abs(p1 - p2) + 1e-15);
                double logW = Math.Log10(points[i].Omega) + frac * (Math.Log10(points[i + 1].Omega) - Math.Log10(points[i].Omega));
                omegaPc = Math.Pow(10, logW);
                break;
            }
        }

        // Gain Margin (GM): -20 * log10 |G(j * omega_pc)|
        double? gmDb = null;
        if (omegaPc.HasValue)
        {
            Complex gAtPc = tf.Evaluate(omegaPc.Value);
            double magAtPc = gAtPc.Magnitude;
            gmDb = -20.0 * Math.Log10(Math.Max(1e-12, magAtPc));
        }

        // Phase Margin (PM): 180 + Phase(omega_gc)
        double? pmDeg = null;
        double? delayMargin = null;
        if (omegaGc.HasValue)
        {
            Complex gAtGc = tf.Evaluate(omegaGc.Value);
            double phaseRadAtGc = Math.Atan2(gAtGc.Imaginary, gAtGc.Real);
            double phaseDegAtGc = phaseRadAtGc * (180.0 / Math.PI);
            while (phaseDegAtGc < -180.0) phaseDegAtGc += 360.0;
            while (phaseDegAtGc > 180.0) phaseDegAtGc -= 360.0;

            pmDeg = 180.0 + phaseDegAtGc;

            if (omegaGc.Value > 1e-9 && pmDeg.Value > 0)
            {
                delayMargin = (pmDeg.Value * Math.PI / 180.0) / omegaGc.Value;
            }
        }

        bool openLoopMarginallyOrAsymptoticallyStable = tf.Poles.All(p => p.Real <= 1e-9);
        bool isStable = (gmDb == null || gmDb.Value > 0) && (pmDeg == null || pmDeg.Value > 0) && openLoopMarginallyOrAsymptoticallyStable;

        return new BodeResult(points, omegaGc, omegaPc, gmDb, pmDeg, delayMargin, isStable);
    }
}
