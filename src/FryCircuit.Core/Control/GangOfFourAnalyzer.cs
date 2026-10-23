using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FryCircuit.Core.Control;

public record GangOfFourPoint(
    double Omega,
    double SensitivityMagDb,
    double CompSensitivityMagDb,
    double LoadDisturbanceMagDb,
    double NoiseControlMagDb
);

public record GangOfFourResult(
    TransferFunction Sensitivity,           // S(s) = 1 / (1 + PC)
    TransferFunction CompSensitivity,       // T(s) = PC / (1 + PC)
    TransferFunction LoadDisturbance,       // PS(s) = P / (1 + PC)
    TransferFunction NoiseControl,          // CS(s) = C / (1 + PC)
    IReadOnlyList<GangOfFourPoint> Points,
    double PeakSensitivityMs,               // Ms = max |S(j*w)|
    double PeakCompSensitivityMt,           // Mt = max |T(j*w)|
    double PeakFrequencyMs,                 // Frequency where Ms occurs
    double DisturbanceBandwidthWb,          // Frequency where |S| crosses -3dB (0.707)
    double BodeSensitivityIntegralValue,    // Integral of ln|S(j*w)| dw
    double TheoreticalWaterbedTarget        // pi * sum(Re(p_k) > 0)
);

public static class GangOfFourAnalyzer
{
    public static GangOfFourResult Analyze(
        TransferFunction plant,
        TransferFunction controller,
        double minOmega = 1e-2,
        double maxOmega = 1e5,
        int numPoints = 300)
    {
        // Loop transfer function: L(s) = P(s) * C(s)
        var L = TransferFunction.Series(plant, controller);

        // Gang of Four:
        // S(s) = 1 / (1 + L)
        var S = TransferFunction.Feedback(TransferFunction.One, L);

        // T(s) = L / (1 + L)
        var T = TransferFunction.Feedback(L, TransferFunction.One);

        // PS(s) = P / (1 + L)
        var PS = TransferFunction.Feedback(plant, controller);

        // CS(s) = C / (1 + L)
        var CS = TransferFunction.Feedback(controller, plant);

        double logMin = Math.Log10(minOmega);
        double logMax = Math.Log10(maxOmega);
        double logStep = (logMax - logMin) / (numPoints - 1);

        var points = new List<GangOfFourPoint>(numPoints);
        double maxS = 0.0;
        double wMs = 0.0;
        double maxT = 0.0;
        double wb = 0.0;
        bool wbFound = false;

        double prevOmega = minOmega;
        double integralLnS = 0.0;

        for (int i = 0; i < numPoints; i++)
        {
            double omega = Math.Pow(10, logMin + i * logStep);

            Complex valS = S.Evaluate(omega);
            Complex valT = T.Evaluate(omega);
            Complex valPS = PS.Evaluate(omega);
            Complex valCS = CS.Evaluate(omega);

            double magS = valS.Magnitude;
            double magT = valT.Magnitude;
            double magPS = valPS.Magnitude;
            double magCS = valCS.Magnitude;

            if (magS > maxS)
            {
                maxS = magS;
                wMs = omega;
            }
            if (magT > maxT)
            {
                maxT = magT;
            }

            if (!wbFound && magS >= 0.7071)
            {
                wb = omega;
                wbFound = true;
            }

            // Numerical trapezoidal integration: ln|S| dw
            if (i > 0)
            {
                double dW = omega - prevOmega;
                integralLnS += Math.Log(Math.Max(1e-12, magS)) * dW;
            }
            prevOmega = omega;

            points.Add(new GangOfFourPoint(
                omega,
                20.0 * Math.Log10(Math.Max(1e-12, magS)),
                20.0 * Math.Log10(Math.Max(1e-12, magT)),
                20.0 * Math.Log10(Math.Max(1e-12, magPS)),
                20.0 * Math.Log10(Math.Max(1e-12, magCS))
            ));
        }

        // Theoretical waterbed target: pi * sum_{p in RHP} Re(p)
        double theoreticalWaterbed = 0.0;
        foreach (var p in L.Poles)
        {
            if (p.Real > 1e-9)
            {
                theoreticalWaterbed += Math.PI * p.Real;
            }
        }

        return new GangOfFourResult(
            S,
            T,
            PS,
            CS,
            points,
            maxS,
            maxT,
            wMs,
            wb,
            integralLnS,
            theoreticalWaterbed
        );
    }
}
