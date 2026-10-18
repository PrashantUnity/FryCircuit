using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Quantum Tunnel Diode (Esaki Diode) exhibiting Negative Differential Resistance (NDR).
/// Used in picosecond pulse generators, space radar reflection amplifiers, and microwave drone links.
/// Physics:
///   I_total(V) = I_tunnel(V) + I_excess(V) + I_diffusion(V)
///   I_tunnel = I_p * (V / V_p) * exp(1 - V / V_p)
///   I_excess = I_v * exp(A * (V - V_v))
///   I_diff   = I_0 * (exp(V / V_t) - 1)
/// </summary>
public class TunnelDiodeDevice : Device, IDeviceSimulation
{
    /// <summary>Peak tunneling current I_p in Amperes</summary>
    public double PeakCurrent { get; set; } = 0.005; // 5 mA

    /// <summary>Valley current I_v in Amperes (typically 1/5 to 1/10 of I_p)</summary>
    public double ValleyCurrent { get; set; } = 0.0006; // 0.6 mA

    /// <summary>Peak voltage V_p in Volts (typically 50 mV to 100 mV)</summary>
    public double PeakVoltage { get; set; } = 0.065; // 65 mV

    /// <summary>Valley voltage V_v in Volts (typically 300 mV to 500 mV)</summary>
    public double ValleyVoltage { get; set; } = 0.35; // 350 mV

    /// <summary>Parasitic p-n junction capacitance in Farads (e.g. 5 pF)</summary>
    public double JunctionCapacitance { get; set; } = 5e-12; // 5 pF

    /// <summary>Series parasitic lead resistance</summary>
    public double SeriesResistance { get; set; } = 1.0; // 1 Ω

    public double StateVoltage { get; set; } = 0.0;
    public double EquivCurrent { get; set; } = 0.0;

    public TunnelDiodeDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "tunneldiode", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Anode (+)
            new(1, 0, 1, 0)   // Cathode (-)
        };
    }

    /// <summary>
    /// Computes the total quantum diode current and differential conductance at voltage V.
    /// </summary>
    public (double Current, double Conductance) Evaluate(double v, double vThermal)
    {
        if (v < -0.5)
        {
            // Reverse breakdown conduction
            double gRev = 0.1;
            return ((v + 0.5) * gRev, gRev);
        }

        // 1. Quantum band-to-band tunneling current
        double x = v / Math.Max(0.001, PeakVoltage);
        double expTun = Math.Exp(Math.Clamp(1.0 - x, -30.0, 30.0));
        double iTun = PeakCurrent * x * expTun;
        double gTun = (PeakCurrent / PeakVoltage) * (1.0 - x) * expTun;

        // 2. Excess tunneling current through bandgap defect states
        double vDiff = v - ValleyVoltage;
        double a = 3.0; // shape factor
        double expExcess = Math.Exp(Math.Clamp(a * vDiff, -30.0, 30.0));
        double iExcess = ValleyCurrent * expExcess;
        double gExcess = ValleyCurrent * a * expExcess;

        // 3. Normal thermal diffusion current (Shockley forward conduction)
        double vt = Math.Max(0.001, vThermal);
        double i0 = 1e-11;
        double expDiff = Math.Exp(Math.Clamp((v - 0.5) / vt, -30.0, 40.0));
        double iDiff = i0 * expDiff;
        double gDiff = (i0 / vt) * expDiff;

        double iTotal = iTun + iExcess + iDiff;
        double gTotal = gTun + gExcess + gDiff;

        return (iTotal, gTotal);
    }

    public void StampConductance(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        // Capacitive companion conductance (Backward Euler: C / dt)
        if (ctx.Dt > 0)
        {
            double gCap = JunctionCapacitance / ctx.Dt;
            ctx.StampConductance(n0, n1, gCap);
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        if (ctx.Dt > 0)
        {
            double gCap = JunctionCapacitance / ctx.Dt;
            double iEq = gCap * StateVoltage;
            ctx.StampCurrentSource(n1, n0, iEq);
        }
    }

    public void StampNonLinear(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        double v0 = ctx.GetNodeVoltage(n0);
        double v1 = ctx.GetNodeVoltage(n1);
        double vD = v0 - v1;

        var (iTotal, gTotal) = Evaluate(vD, ctx.ThermalVoltage);

        // Newton-Raphson linearized stamp:
        // G_eq = gTotal
        // I_eq = iTotal - gTotal * vD
        ctx.StampConductance(n0, n1, gTotal);
        ctx.StampCurrentSource(n0, n1, iTotal - gTotal * vD);
    }

    public void UpdateState(MnaContext ctx)
    {
        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vD = v0 - v1;
        VoltageDrop = vD;

        var (iTotal, _) = Evaluate(vD, ctx.ThermalVoltage);

        // Update capacitive companion
        double gCap = ctx.Dt > 0 ? (JunctionCapacitance / ctx.Dt) : 0.0;
        double iCap = gCap * (vD - StateVoltage);
        EquivCurrent = gCap * vD;
        StateVoltage = vD;

        Current = iTotal + iCap;
    }
}
