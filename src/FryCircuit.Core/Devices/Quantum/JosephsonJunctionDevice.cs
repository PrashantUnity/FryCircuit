using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Superconducting Josephson Junction for Quantum Computers (Transmon Qubit, SQUID, RSFQ Logic).
/// Physics: RCSJ (Resistively and Capacitively Shunted Junction) model.
/// Dynamic equations:
///   d(phi)/dt = (2e / hbar) * V(t)
///   I(t) = I_c * sin(phi(t)) + V(t)/R_n + C_j * dV/dt
/// </summary>
public class JosephsonJunctionDevice : Device, IDeviceSimulation
{
    /// <summary>Critical current I_c in Amperes (typically 1 uA to 100 uA for qubits)</summary>
    public double CriticalCurrent { get; set; } = 10e-6; // 10 µA

    /// <summary>Normal-state quasiparticle resistance R_n in Ohms (typically 100 to 1000 Ohms)</summary>
    public double NormalResistance { get; set; } = 250.0; // 250 Ω

    /// <summary>Intrinsic junction capacitance C_j in Farads (femtofarad scale, e.g. 50 fF)</summary>
    public double JunctionCapacitance { get; set; } = 50e-15; // 50 fF

    /// <summary>Superconducting quantum phase difference phi across the insulating barrier (radians)</summary>
    public double Phase { get; set; } = 0.0;

    /// <summary>Previous voltage for companion capacitive integration</summary>
    public double StateVoltage { get; set; } = 0.0;
    public double EquivCurrent { get; set; } = 0.0;

    /// <summary>
    /// Quantum Josephson Plasma Frequency f_p = (1 / 2pi) * sqrt(2e * I_c / (hbar * C_j))
    /// </summary>
    public double PlasmaFrequencyHz
    {
        get
        {
            double num = (2.0 * PhysicalConstants.ElementaryCharge * CriticalCurrent);
            double den = (PhysicalConstants.Hbar * JunctionCapacitance);
            return (den > 0 && num > 0) ? (Math.Sqrt(num / den) / (2.0 * Math.PI)) : 0.0;
        }
    }

    /// <summary>
    /// Nonlinear Quantum Josephson Inductance L_J(phi) = Phi_0 / (2pi * I_c * cos(phi))
    /// </summary>
    public double JosephsonInductanceH
    {
        get
        {
            double cosPhi = Math.Cos(Phase);
            if (Math.Abs(cosPhi) < 1e-4) cosPhi = 1e-4;
            return PhysicalConstants.FluxQuantum / (2.0 * Math.PI * CriticalCurrent * cosPhi);
        }
    }

    public JosephsonJunctionDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "josephson", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Anode / Phase +
            new(1, 0, 1, 0)   // Cathode / Phase -
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        // Normal shunt resistance conductance
        double gNormal = 1.0 / Math.Max(0.01, NormalResistance);

        // Capacitive companion conductance (Backward Euler: C / dt)
        double gCap = ctx.Dt > 0 ? (JunctionCapacitance / ctx.Dt) : 0.0;

        ctx.StampConductance(n0, n1, gNormal + gCap);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        // Supercurrent source: I_s = I_c * sin(phi) flowing from n0 to n1
        double supercurrent = CriticalCurrent * Math.Sin(Phase);
        ctx.StampCurrentSource(n0, n1, supercurrent);

        // Capacitive companion Norton history current flowing from n1 into n0
        if (ctx.Dt > 0)
        {
            double gCap = JunctionCapacitance / ctx.Dt;
            double iEq = gCap * StateVoltage;
            ctx.StampCurrentSource(n1, n0, iEq);
        }
    }

    public void StampNonLinear(MnaContext ctx)
    {
        // Small-signal Josephson differential conductance around operating point:
        // d(I_s)/dV = I_c * cos(phi) * d(phi)/dV
        // With d(phi)/dt = (2e / hbar) * V => Delta(phi) ~ (2e / hbar) * (Delta(t) / 2) * Delta(V)
        double dPhiDv = (PhysicalConstants.ElementaryCharge / PhysicalConstants.Hbar) * ctx.Dt;
        double gJosephson = CriticalCurrent * Math.Cos(Phase) * dPhiDv;

        // Keep physically bounded to avoid divergence near critical transitions
        gJosephson = Math.Clamp(gJosephson, -1e3, 1e3);

        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gJosephson);
    }

    public void UpdateState(MnaContext ctx)
    {
        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vJunction = v0 - v1;
        VoltageDrop = vJunction;

        // Quantum phase evolution: d(phi)/dt = (2e / hbar) * V(t)
        // Midpoint integration across time-step
        double vAvg = 0.5 * (StateVoltage + vJunction);
        double deltaPhi = (2.0 * PhysicalConstants.ElementaryCharge / PhysicalConstants.Hbar) * vAvg * ctx.Dt;
        Phase = (Phase + deltaPhi) % (2.0 * Math.PI);

        // Update capacitive companion history
        double gCap = ctx.Dt > 0 ? (JunctionCapacitance / ctx.Dt) : 0.0;
        double iCap = gCap * (vJunction - StateVoltage);
        EquivCurrent = gCap * vJunction;
        StateVoltage = vJunction;

        // Total physical current through junction
        double supercurrent = CriticalCurrent * Math.Sin(Phase);
        double iNormal = vJunction / NormalResistance;
        Current = supercurrent + iNormal + iCap;
    }
}
