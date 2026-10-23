using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FryCircuit.Core.Control;

public record NyquistPoint(
    double Omega,
    double Real,
    double Imaginary,
    double Magnitude
);

public record NyquistResult(
    IReadOnlyList<NyquistPoint> Points,
    int ClockwiseEncirclements,
    int OpenLoopRhpPoles,
    int ClosedLoopRhpPoles,
    bool IsClosedLoopStable,
    double DistanceToCriticalPoint,
    double PeakSensitivity
);

public static class NyquistAnalyzer
{
    public static NyquistResult Analyze(
        TransferFunction loopTf,
        double minOmega = 1e-3,
        double maxOmega = 1e4,
        int numPoints = 500)
    {
        if (minOmega <= 0) minOmega = 1e-3;
        if (maxOmega <= minOmega) maxOmega = minOmega * 1000;
        if (numPoints < 50) numPoints = 50;

        // Open-loop RHP poles P
        int P = loopTf.Poles.Count(p => p.Real > 1e-9);

        // Generate points along imaginary axis s = +j*omega
        double logMin = Math.Log10(minOmega);
        double logMax = Math.Log10(maxOmega);
        double logStep = (logMax - logMin) / (numPoints - 1);

        var points = new List<NyquistPoint>(numPoints * 2);
        var fullContour = new List<Complex>(numPoints * 2);

        // Positive frequency branch: omega from min to max
        for (int i = 0; i < numPoints; i++)
        {
            double omega = Math.Pow(10, logMin + i * logStep);
            Complex resp = loopTf.Evaluate(omega);
            points.Add(new NyquistPoint(omega, resp.Real, resp.Imaginary, resp.Magnitude));
            fullContour.Add(resp);
        }

        // Negative frequency branch (complex conjugate): omega from -max to -min
        var negBranch = new List<Complex>(numPoints);
        for (int i = numPoints - 1; i >= 0; i--)
        {
            Complex pos = fullContour[i];
            negBranch.Add(new Complex(pos.Real, -pos.Imaginary));
        }

        var closedLoopContour = new List<Complex>(fullContour.Count + negBranch.Count);
        closedLoopContour.AddRange(fullContour);
        closedLoopContour.AddRange(negBranch);

        // Compute encirclements of (-1, 0)
        // Angle variation of (L(s) + 1)
        double totalAngleChange = 0.0;
        Complex criticalPoint = new Complex(-1.0, 0.0);
        double minDistance = double.PositiveInfinity;

        for (int i = 0; i < closedLoopContour.Count; i++)
        {
            Complex current = closedLoopContour[i];
            Complex next = closedLoopContour[(i + 1) % closedLoopContour.Count];

            Complex vCurrent = current - criticalPoint;
            Complex vNext = next - criticalPoint;

            double dist = vCurrent.Magnitude;
            if (dist < minDistance) minDistance = dist;

            // Angle difference between vCurrent and vNext
            double a1 = Math.Atan2(vCurrent.Imaginary, vCurrent.Real);
            double a2 = Math.Atan2(vNext.Imaginary, vNext.Real);
            double da = a2 - a1;
            while (da > Math.PI) da -= 2 * Math.PI;
            while (da < -Math.PI) da += 2 * Math.PI;

            totalAngleChange += da;
        }

        // Clockwise encirclements: negative of counter-clockwise angle change divided by 2*pi
        // Standard convention: clockwise is positive encirclements N_cw
        int N_cw = (int)Math.Round(-totalAngleChange / (2.0 * Math.PI));

        // Closed loop poles in RHP: Z = N + P
        int Z = N_cw + P;
        bool isStable = (Z == 0);

        if (minDistance < 1e-9) minDistance = 1e-9;
        double Ms = 1.0 / minDistance;

        return new NyquistResult(
            points,
            N_cw,
            P,
            Z,
            isStable,
            minDistance,
            Ms
        );
    }
}
