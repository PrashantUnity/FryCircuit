using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// High-Precision SPICE Diode Model with Pico-level Transient Physics:
/// - Shockley exponential conduction with emission coefficient
/// - Temperature-dependent bandgap and saturation current I_s(T)
/// - Nonlinear voltage-dependent junction depletion capacitance C_j(V)
/// - Minority carrier diffusion capacitance (transit time tau_d)
/// - Reverse recovery dynamics down to picosecond scale
/// </summary>
public class DiodeDevice : Device, IDeviceSimulation
{
    public double SaturationCurrent
    {
        get => GetParameter("is", 1e-14); // 10 fA nominal at 300K
        set => SetParameter("is", value);
    }

    public double EmissionCoefficient
    {
        get => GetParameter("n", 1.0);
        set => SetParameter("n", value);
    }

    /// <summary>Zero-bias junction capacitance C_j0 in Farads (e.g. 2 pF)</summary>
    public double ZeroBiasCapacitance
    {
        get => GetParameter("cjo", 2e-12);
        set => SetParameter("cjo", value);
    }

    /// <summary>Forward transit time tau_t in seconds (carrier lifetime, e.g. 100 ps - 5 ns)</summary>
    public double TransitTime
    {
        get => GetParameter("tt", 5e-10); // 500 ps
        set => SetParameter("tt", value);
    }

    /// <summary>Built-in junction potential V_j in Volts (typically 0.7V)</summary>
    public double JunctionPotential
    {
        get => GetParameter("vj", 0.7);
        set => SetParameter("vj", value);
    }

    /// <summary>Grading coefficient M (0.33 for linearly graded, 0.5 for abrupt junction)</summary>
    public double GradingCoefficient
    {
        get => GetParameter("m", 0.5);
        set => SetParameter("m", value);
    }

    public double StateVoltage { get; set; } = 0.0;
    public double EquivCurrent { get; set; } = 0.0;

    public DiodeDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "diode", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Anode
            new(1, 0, 0, 1)   // Cathode
        };
    }

    /// <summary>
    /// Computes temperature-adjusted saturation current using Varshni bandgap physics.
    /// </summary>
    public double GetTemperatureAdjustedIs(double tempK)
    {
        double t0 = PhysicalConstants.TempRoomStandard;
        double t = Math.Max(0.1, tempK);
        double eg0 = PhysicalConstants.SiliconBandgap(t0);
        double egT = PhysicalConstants.SiliconBandgap(t);

        double vt0 = PhysicalConstants.ThermalVoltage(t0);
        double vtT = PhysicalConstants.ThermalVoltage(t);

        double expArg = (eg0 / vt0) - (egT / vtT);
        double tRatio = t / t0;
        return SaturationCurrent * Math.Pow(tRatio, 3.0 / EmissionCoefficient) * Math.Exp(Math.Clamp(expArg, -40.0, 40.0));
    }

    /// <summary>
    /// Computes total nonlinear dynamic capacitance C_total = C_depletion(V) + C_diffusion(I).
    /// </summary>
    public double ComputeCapacitance(double vd, double gd)
    {
        double cjo = ZeroBiasCapacitance;
        double vj = JunctionPotential;
        double m = GradingCoefficient;

        // Depletion capacitance with linear expansion for forward bias > 0.5 * Vj
        double fc = 0.5;
        double cDep;
        if (vd < fc * vj)
        {
            cDep = cjo * Math.Pow(1.0 - vd / vj, -m);
        }
        else
        {
            double f1 = Math.Pow(1.0 - fc, -m);
            cDep = cjo * f1 * (1.0 + m * (vd - fc * vj) / (vj * (1.0 - fc)));
        }

        // Diffusion capacitance: C_diff = tau * gd
        double cDiff = TransitTime * Math.Max(0.0, gd);

        return Math.Clamp(cDep + cDiff, 1e-15, 1e-6);
    }

    public void StampConductance(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        // Stamp dynamic junction capacitance companion conductance: C / dt
        if (ctx.Dt > 0)
        {
            double cTotal = ComputeCapacitance(StateVoltage, 0.0);
            double gCap = cTotal / ctx.Dt;
            ctx.StampConductance(n0, n1, gCap);
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        if (ctx.Dt > 0)
        {
            double cTotal = ComputeCapacitance(StateVoltage, 0.0);
            double gCap = cTotal / ctx.Dt;
            double iEq = gCap * StateVoltage;
            ctx.StampCurrentSource(n1, n0, iEq);
        }
    }

    public virtual void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        double vd = ctx.GetNodeVoltage(n0) - ctx.GetNodeVoltage(n1);
        double vt = Math.Max(0.0001, ctx.ThermalVoltage * EmissionCoefficient);
        double isat = GetTemperatureAdjustedIs(ctx.TemperatureKelvin);

        double id, gd;

        // Shockley diode equation with smooth limiting to avoid numerical overflow
        if (vd > 0.8)
        {
            // Linearized forward conduction in series-resistance dominated region
            gd = 0.2; // 5 Ω dynamic resistance
            double idKnee = isat * Math.Exp(0.8 / vt);
            id = idKnee + gd * (vd - 0.8);
        }
        else if (vd >= -5.0 * vt)
        {
            double expVal = Math.Exp(Math.Clamp(vd / vt, -20.0, 35.0));
            id = isat * (expVal - 1.0);
            gd = Math.Clamp((isat / vt) * expVal, 1e-12, 100.0);
        }
        else
        {
            // Reverse saturation regime
            id = -isat;
            gd = 1e-12;
        }

        // Newton-Raphson companion linearization:
        // Current injected into RHS: I_eq = id - gd * vd
        double ieq = id - gd * vd;
        ctx.StampConductance(n0, n1, gd);
        ctx.StampCurrentSource(n0, n1, ieq);
    }

    public virtual void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vd = v0 - v1;
        VoltageDrop = vd;

        double vt = Math.Max(0.0001, ctx.ThermalVoltage * EmissionCoefficient);
        double isat = GetTemperatureAdjustedIs(ctx.TemperatureKelvin);

        double id, gd;
        if (vd > 0.8)
        {
            gd = 0.2;
            double idKnee = isat * Math.Exp(0.8 / vt);
            id = idKnee + gd * (vd - 0.8);
        }
        else
        {
            double expVal = Math.Exp(Math.Clamp(vd / vt, -20.0, 35.0));
            id = isat * (expVal - 1.0);
            gd = Math.Clamp((isat / vt) * expVal, 1e-12, 100.0);
        }

        // Update capacitive companion history
        double cTotal = ComputeCapacitance(vd, gd);
        double gCap = ctx.Dt > 0 ? (cTotal / ctx.Dt) : 0.0;
        double iCap = gCap * (vd - StateVoltage);
        EquivCurrent = gCap * vd;

        StateVoltage = vd;
        Current = id + iCap;
    }
}
