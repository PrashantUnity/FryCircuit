using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// DC-SQUID (Superconducting Quantum Interference Device) for Quantum Computing,
/// Flux-Tunable Qubits (Transmons), Quantum Magnetometers, and Dark Matter Detection.
/// Physics: Two parallel Josephson junctions in a superconducting loop:
///   I_c(Phi_ext) = 2 * I_c0 * |cos(pi * Phi_ext / Phi_0)|
///   d(phi)/dt = (2e / hbar) * V(t)
///   I(t) = I_c(Phi_ext) * sin(phi) + V/R_eff + C_eff * dV/dt
/// </summary>
public class SquidDevice : Device, IDeviceSimulation
{
    /// <summary>Single-junction critical current I_c0 in Amperes (typically 1 µA to 50 µA)</summary>
    public double BaseCriticalCurrent { get; set; } = 10.0e-6; // 10 µA

    /// <summary>External magnetic flux bias in units of magnetic flux quantum (Phi_ext / Phi_0)</summary>
    public double ExternalFluxNormalized { get; set; } = 0.0; // 0.0 to 1.0 Phi_0

    /// <summary>Effective parallel normal-state resistance R_n/2 in Ohms</summary>
    public double NormalResistance { get; set; } = 150.0; // 150 Ω

    /// <summary>Combined loop junction capacitance 2 * C_j in Farads (femtofarad scale)</summary>
    public double TotalCapacitance { get; set; } = 100.0e-15; // 100 fF

    /// <summary>Superconducting quantum phase difference phi across the SQUID</summary>
    public double Phase { get; set; } = 0.0;

    /// <summary>Previous voltage for capacitive companion integration</summary>
    public double StateVoltage { get; set; } = 0.0;
    public double EquivCurrent { get; set; } = 0.0;

    /// <summary>
    /// Effective critical current modulated by external magnetic flux:
    /// I_c(Phi_ext) = 2 * I_c0 * |cos(pi * Phi_ext / Phi_0)|
    /// </summary>
    public double ModulatedCriticalCurrent
    {
        get
        {
            double arg = Math.PI * ExternalFluxNormalized;
            return 2.0 * BaseCriticalCurrent * Math.Abs(Math.Cos(arg));
        }
    }

    /// <summary>
    /// Tunable Josephson plasma frequency in Hz
    /// </summary>
    public double PlasmaFrequencyHz
    {
        get
        {
            double ic = ModulatedCriticalCurrent;
            double num = 2.0 * PhysicalConstants.ElementaryCharge * Math.Max(1e-12, ic);
            double den = PhysicalConstants.Hbar * Math.Max(1e-18, TotalCapacitance);
            return Math.Sqrt(num / den) / (2.0 * Math.PI);
        }
    }

    /// <summary>
    /// Tunable kinetic inductance L_J(Phi_ext, phi) in Henries
    /// </summary>
    public double TunableInductanceH
    {
        get
        {
            double ic = Math.Max(1e-12, ModulatedCriticalCurrent);
            double cosPhi = Math.Max(1e-4, Math.Abs(Math.Cos(Phase)));
            return PhysicalConstants.FluxQuantum / (2.0 * Math.PI * ic * cosPhi);
        }
    }

    public SquidDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "squid", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Anode (Port +)
            new(1, 0, 0, 1)   // Cathode (Port -)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // 1. Shunt resistance conductance
        double gNorm = 1.0 / Math.Max(1.0, NormalResistance);

        // 2. Capacitive companion conductance (Backward Euler: C / dt)
        double gCap = ctx.Dt > 0 ? (TotalCapacitance / ctx.Dt) : 0.0;

        // 3. Dynamic differential Josephson conductance g_J = dI/dV
        double gJosephson = 1e-6; // baseline numerical stabilization

        double totalG = gNorm + gCap + gJosephson;
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, totalG);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        // Supercurrent through SQUID from pin 0 to pin 1
        double isuper = ModulatedCriticalCurrent * Math.Sin(Phase);
        ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, isuper);

        // Capacitive companion Norton history current flowing from pin 1 into pin 0
        if (ctx.Dt > 0)
        {
            double gCap = TotalCapacitance / ctx.Dt;
            double iEq = gCap * StateVoltage;
            ctx.StampCurrentSource(Pins[1].NodeId, Pins[0].NodeId, iEq);
        }
    }

    public void StampNonLinear(MnaContext ctx)
    {
        // Josephson non-linear supercurrent handled dynamically via companion history stamp
    }

    public void UpdateState(MnaContext ctx)
    {
        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        double v = v0 - v1;
        VoltageDrop = v;

        // Quantum phase evolution: d(phi)/dt = (2e / hbar) * V(t)
        double dPhi = ((2.0 * PhysicalConstants.ElementaryCharge) / PhysicalConstants.Hbar) * v * ctx.Dt;
        Phase = (Phase + dPhi) % (2.0 * Math.PI);

        double isuper = ModulatedCriticalCurrent * Math.Sin(Phase);
        double gCap = ctx.Dt > 0 ? (TotalCapacitance / ctx.Dt) : 0.0;
        double iCap = gCap * (v - StateVoltage);
        EquivCurrent = gCap * v;
        StateVoltage = v;

        Current = isuper + (v / NormalResistance) + iCap;
    }
}
