using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FryCircuit.Core.Control;

public record RootLocusBranchPoint(
    double GainK,
    IReadOnlyList<Complex> Roots
);

public record RootLocusResult(
    IReadOnlyList<Complex> OpenLoopPoles,
    IReadOnlyList<Complex> OpenLoopZeros,
    int AsymptoteCount,
    double AsymptoteCentroid,
    IReadOnlyList<double> AsymptoteAnglesDeg,
    IReadOnlyList<Complex> BreakawayPoints,
    IReadOnlyList<RootLocusBranchPoint> Trajectories
);

public static class RootLocusAnalyzer
{
    public static RootLocusResult Analyze(
        TransferFunction openLoopTf,
        double maxK = 100.0,
        int numSteps = 200)
    {
        var D = openLoopTf.Denominator;
        var N = openLoopTf.Numerator;

        var poles = D.FindRoots();
        var zeros = N.FindRoots();

        int n = D.Degree;
        int m = N.Degree;
        int numAsymptotes = Math.Max(0, n - m);

        double centroid = 0.0;
        var anglesDeg = new List<double>();

        if (numAsymptotes > 0)
        {
            double sumPoles = poles.Sum(p => p.Real);
            double sumZeros = zeros.Sum(z => z.Real);
            centroid = (sumPoles - sumZeros) / numAsymptotes;

            for (int k = 0; k < numAsymptotes; k++)
            {
                double angleRad = (2 * k + 1) * Math.PI / numAsymptotes;
                anglesDeg.Add(angleRad * (180.0 / Math.PI));
            }
        }

        // Breakaway / break-in points: roots of N'*D - N*D' = 0
        var breakawayList = new List<Complex>();
        var dPoly = N.Derivative() * D - N * D.Derivative();
        if (dPoly.Degree > 0)
        {
            var candRoots = dPoly.FindRoots();
            foreach (var r in candRoots)
            {
                if (Math.Abs(r.Imaginary) < 1e-4)
                {
                    // Check if K = -D(s)/N(s) > 0
                    double valD = D.Evaluate(r.Real);
                    double valN = N.Evaluate(r.Real);
                    if (Math.Abs(valN) > 1e-12)
                    {
                        double kVal = -valD / valN;
                        if (kVal >= 0)
                        {
                            breakawayList.Add(new Complex(r.Real, 0.0));
                        }
                    }
                }
            }
        }

        // Generate trajectory points for K from 0 to maxK using log-linear distribution
        var trajectories = new List<RootLocusBranchPoint>(numSteps);
        for (int i = 0; i < numSteps; i++)
        {
            // Distribution emphasizing small K values: K = maxK * (i / (N-1))^2.5
            double frac = (double)i / (numSteps - 1);
            double k = maxK * Math.Pow(frac, 2.5);

            // Characteristic equation: D(s) + K * N(s) = 0
            var charPoly = D + N * k;
            var roots = charPoly.FindRoots();
            trajectories.Add(new RootLocusBranchPoint(k, roots));
        }

        return new RootLocusResult(
            poles,
            zeros,
            numAsymptotes,
            centroid,
            anglesDeg,
            breakawayList,
            trajectories
        );
    }
}
