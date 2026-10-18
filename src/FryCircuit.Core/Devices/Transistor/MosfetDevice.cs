using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Advanced Aerospace & Space Grade MOSFET Model (Level 3 / Subthreshold / Parasitic Capacitances):
/// - Weak inversion / subthreshold exponential conduction for cryogenic CMOS & low-power space sensors
/// - Strong inversion triode and saturation regimes with channel-length modulation (lambda)
/// - Parasitic gate-to-source (C_gs) and gate-to-drain Miller (C_gd) charge storage down to femtofarads
/// </summary>
public class MosfetDevice : Device, IDeviceSimulation
{
    public bool IsNChannel { get; set; }

    public double Vth
    {
        get => GetParameter("vto", IsNChannel ? 1.5 : -1.5);
        set => SetParameter("vto", value);
    }

    public double Kp
    {
        get => GetParameter("kp", 0.005); // 5 mA/V^2
        set => SetParameter("kp", value);
    }

    /// <summary>Channel-length modulation factor lambda (1/V)</summary>
    public double Lambda
    {
        get => GetParameter("lambda", 0.02); // 0.02 / V
        set => SetParameter("lambda", value);
    }

    /// <summary>Subthreshold slope ideality factor n (typically 1.2 to 1.8)</summary>
    public double SubthresholdFactor
    {
        get => GetParameter("nsub", 1.4);
        set => SetParameter("nsub", value);
    }

    /// <summary>Gate-to-source capacitance C_gs in Farads (e.g. 20 pF)</summary>
    public double Cgs
    {
        get => GetParameter("cgs", 20e-12);
        set => SetParameter("cgs", value);
    }

    /// <summary>Gate-to-drain Miller capacitance C_gd in Farads (e.g. 5 pF)</summary>
    public double Cgd
    {
        get => GetParameter("cgd", 5e-12);
        set => SetParameter("cgd", value);
    }

    // Dynamic state history for capacitive companions
    public double StateVgs { get; set; } = 0.0;
    public double EquivCurrentGs { get; set; } = 0.0;

    public double StateVgd { get; set; } = 0.0;
    public double EquivCurrentGd { get; set; } = 0.0;

    public MosfetDevice(int id = 0, string type = "nmos", double x = 0, double y = 0)
        : base(id, type, x, y)
    {
        IsNChannel = type.Equals("nmos", StringComparison.OrdinalIgnoreCase);
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0),  // Gate
            new(1, 0, 1, -1),  // Drain
            new(2, 0, 1, 1)    // Source
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        int gNode = Pins[0].NodeId;
        int dNode = Pins[1].NodeId;
        int sNode = Pins[2].NodeId;

        if (ctx.Dt > 0)
        {
            // Backward Euler dynamic gate capacitance companions
            double gCgs = Cgs / ctx.Dt;
            ctx.StampConductance(gNode, sNode, gCgs);

            double gCgd = Cgd / ctx.Dt;
            ctx.StampConductance(gNode, dNode, gCgd);
        }

        // Numerical conditioning Gmin shunt to guarantee non-singular matrix for floating gates
        ctx.StampConductance(gNode, sNode, 1e-10);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 3 || ctx.Dt <= 0) return;

        int gNode = Pins[0].NodeId;
        int dNode = Pins[1].NodeId;
        int sNode = Pins[2].NodeId;

        // Stamp dynamic gate capacitive history currents (Backward Euler companion: flows into gate)
        double gCgs = Cgs / ctx.Dt;
        double gCgd = Cgd / ctx.Dt;

        ctx.StampCurrentSource(sNode, gNode, gCgs * StateVgs);
        ctx.StampCurrentSource(dNode, gNode, gCgd * StateVgd);
    }

    public void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        int g = Pins[0].NodeId;
        int d = Pins[1].NodeId;
        int s = Pins[2].NodeId;

        double vg = ctx.GetNodeVoltage(g);
        double vd = ctx.GetNodeVoltage(d);
        double vs = ctx.GetNodeVoltage(s);

        double vgs = IsNChannel ? (vg - vs) : (vs - vg);
        double vds = Math.Max(0.0, IsNChannel ? (vd - vs) : (vs - vd));
        double vth = IsNChannel ? Vth : -Vth;
        double vt = Math.Max(0.0001, ctx.ThermalVoltage);

        double ids, gds, gm;

        if (vgs < vth)
        {
            // 1. Weak Inversion / Subthreshold conduction
            double n = SubthresholdFactor;
            double expSub = Math.Exp(Math.Clamp((vgs - vth) / (n * vt), -30.0, 0.0));
            double expVds = 1.0 - Math.Exp(Math.Clamp(-vds / vt, -30.0, 0.0));
            double isub0 = 1e-11; // 10 pA subthreshold current
            ids = isub0 * expSub * expVds;
            gm = ids / (n * vt);
            gds = Math.Max(1e-12, (isub0 * expSub / vt) * Math.Exp(-vds / vt));
        }
        else
        {
            // Strong Inversion
            double vov = vgs - vth; // Overdrive voltage
            double lambdaMod = 1.0 + Lambda * vds;

            if (vds < vov)
            {
                // 2. Linear / Triode Regime
                ids = Kp * (vov * vds - 0.5 * vds * vds) * lambdaMod;
                gm = Kp * vds * lambdaMod;
                gds = Kp * (vov - vds) * lambdaMod + Kp * (vov * vds - 0.5 * vds * vds) * Lambda;
            }
            else
            {
                // 3. Saturation Regime
                ids = 0.5 * Kp * vov * vov * lambdaMod;
                gm = Kp * vov * lambdaMod;
                gds = 0.5 * Kp * vov * vov * Lambda;
            }
        }

        gds = Math.Clamp(gds, 1e-9, 1e4);
        gm = Math.Clamp(gm, 0.0, 1e4);

        // Linearized Newton-Raphson Stamp:
        // I_ds(vgs, vds) ~ I_0 + gm * (vgs - vgs0) + gds * (vds - vds0)
        double ieq = ids - gm * vgs - gds * vds;

        // Drain-to-Source conductance
        ctx.StampConductance(d, s, gds);

        // Voltage-controlled transconductance gm
        // Injects gm * vgs into drain/source
        if (ctx.NodeIndexMap.TryGetValue(d, out int dRow))
        {
            if (ctx.NodeIndexMap.TryGetValue(g, out int gCol)) ctx.G[dRow, gCol] += (IsNChannel ? gm : -gm);
            if (ctx.NodeIndexMap.TryGetValue(s, out int sCol)) ctx.G[dRow, sCol] -= (IsNChannel ? gm : -gm);
            ctx.B[dRow] -= (IsNChannel ? ieq : -ieq);
        }

        if (ctx.NodeIndexMap.TryGetValue(s, out int sRow))
        {
            if (ctx.NodeIndexMap.TryGetValue(g, out int gCol)) ctx.G[sRow, gCol] -= (IsNChannel ? gm : -gm);
            if (ctx.NodeIndexMap.TryGetValue(s, out int sCol)) ctx.G[sRow, sCol] += (IsNChannel ? gm : -gm);
            ctx.B[sRow] += (IsNChannel ? ieq : -ieq);
        }
    }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        double vg = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vd = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vs = ctx.GetNodeVoltage(Pins[2].NodeId);

        double vgs = IsNChannel ? (vg - vs) : (vs - vg);
        double vds = Math.Max(0.0, IsNChannel ? (vd - vs) : (vs - vd));
        VoltageDrop = vd - vs;

        double vth = IsNChannel ? Vth : -Vth;
        double lambdaMod = 1.0 + Lambda * vds;

        if (vgs < vth)
        {
            double n = SubthresholdFactor;
            double vt = Math.Max(0.0001, ctx.ThermalVoltage);
            Current = 1e-11 * Math.Exp(Math.Clamp((vgs - vth) / (n * vt), -30.0, 0.0)) * (1.0 - Math.Exp(Math.Clamp(-vds / vt, -30.0, 0.0)));
        }
        else
        {
            double vov = vgs - vth;
            if (vds < vov)
            {
                Current = Kp * (vov * vds - 0.5 * vds * vds) * lambdaMod;
            }
            else
            {
                Current = 0.5 * Kp * vov * vov * lambdaMod;
            }
        }

        // Update capacitive companion history
        double vgsInst = vg - vs;
        double vgdInst = vg - vd;

        StateVgs = vgsInst;
        StateVgd = vgdInst;
        EquivCurrentGs = ctx.Dt > 0 ? (Cgs / ctx.Dt) * StateVgs : 0.0;
        EquivCurrentGd = ctx.Dt > 0 ? (Cgd / ctx.Dt) * StateVgd : 0.0;
    }
}
