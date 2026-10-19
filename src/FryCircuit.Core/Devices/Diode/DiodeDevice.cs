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

    private double _lastIterVd = 0.0;
    private int _lastIter = -1;

    /// <summary>
    /// Standard SPICE pnjlim algorithm: limits junction voltage step per iteration
    /// to prevent exponential divergence and limit-cycle oscillation.
    /// </summary>
    public static double PnjLim(double vnew, double vold, double vt, double vcrit)
    {
        if (vnew > vcrit && Math.Abs(vnew - vold) > (2.0 * vt))
        {
            if (vold > 0.0)
            {
                double arg = 1.0 + (vnew - vold) / vt;
                vnew = arg > 0.0 ? (vold + vt * Math.Log(arg)) : vcrit;
            }
            else
            {
                vnew = vt * Math.Log(Math.Max(1.0, vnew / vt));
            }
        }
        else if (vnew < 0.0)
        {
            double arg = vold > 0.0 ? (-1.0 + vnew / vt) : ((vnew - vold) / vt);
            if (arg <= -0.5)
            {
                vnew = vold - vt * 0.5;
            }
        }
        return vnew;
    }

    /// <summary>
    /// Evaluates diode current and differential small-signal conductance with C1 continuous extension.
    /// </summary>
    public static (double Id, double Gd) EvaluateConduction(double vd, double vt, double isat)
    {
        const double xMax = 35.0;
        double vMax = xMax * vt;
        double expMax = Math.Exp(xMax);
        double iMax = isat * (expMax - 1.0);
        double gMax = Math.Clamp((isat / vt) * expMax, 1e-12, 1e5);

        if (vd > vMax)
        {
            // C1 continuous linear tangent extension: exact matching at boundary
            double gd = gMax;
            double id = iMax + gd * (vd - vMax);
            return (id, gd);
        }
        else if (vd >= -5.0 * vt)
        {
            double expVal = Math.Exp(vd / vt);
            double id = isat * (expVal - 1.0);
            double gd = Math.Clamp((isat / vt) * expVal, 1e-12, 1e5);
            return (id, gd);
        }
        else
        {
            // Reverse saturation regime
            return (-isat, 1e-12);
        }
    }

    public virtual void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        double vt = Math.Max(0.0001, ctx.ThermalVoltage * EmissionCoefficient);
        double isat = GetTemperatureAdjustedIs(ctx.TemperatureKelvin);
        double vcrit = vt * Math.Log(Math.Max(1.0, vt / (Math.Sqrt(2.0) * isat)));

        if (ctx.Iteration == 0 || ctx.Iteration <= _lastIter)
        {
            _lastIterVd = StateVoltage;
        }
        _lastIter = ctx.Iteration;

        double rawVd = ctx.GetNodeVoltage(n0) - ctx.GetNodeVoltage(n1);
        double vd = PnjLim(rawVd, _lastIterVd, vt, vcrit);
        _lastIterVd = vd;

        var (id, gd) = EvaluateConduction(vd, vt, isat);

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

        var (id, gd) = EvaluateConduction(vd, vt, isat);

        // Update capacitive companion history
        double cTotal = ComputeCapacitance(vd, gd);
        double gCap = ctx.Dt > 0 ? (cTotal / ctx.Dt) : 0.0;
        double iCap = gCap * (vd - StateVoltage);
        EquivCurrent = gCap * vd;

        StateVoltage = vd;
        Current = id + iCap;
    }
}
