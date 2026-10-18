using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class BjtDevice : Device, IDeviceSimulation
{
    public bool IsNpn { get; set; }

    public double BetaF
    {
        get => GetParameter("betaF", 100.0);
        set => SetParameter("betaF", Math.Max(1.0, value));
    }

    public double BetaR
    {
        get => GetParameter("betaR", 1.0);
        set => SetParameter("betaR", Math.Max(0.1, value));
    }

    public double Isat
    {
        get => GetParameter("is", 1e-14);
        set => SetParameter("is", Math.Max(1e-18, value));
    }

    public double EarlyVoltage
    {
        get => GetParameter("vaf", 100.0);
        set => SetParameter("vaf", Math.Max(1.0, value));
    }

    public BjtDevice(int id = 0, string type = "npn", double x = 0, double y = 0)
        : base(id, type, x, y)
    {
        IsNpn = type.Equals("npn", StringComparison.OrdinalIgnoreCase);
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0),  // Base
            new(1, 0, 1, -1),  // Collector
            new(2, 0, 1, 1)    // Emitter
        };
    }

    public void StampConductance(MnaContext ctx) { }
    public void StampCurrentSources(MnaContext ctx) { }

    public void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        int b = Pins[0].NodeId;
        int c = Pins[1].NodeId;
        int e = Pins[2].NodeId;

        double vb = ctx.GetNodeVoltage(b);
        double vc = ctx.GetNodeVoltage(c);
        double ve = ctx.GetNodeVoltage(e);

        double vbe = IsNpn ? (vb - ve) : (ve - vb);

        // Simple linearized active region model
        if (vbe > 0.65)
        {
            double rBe = 1000.0; // Base-emitter dynamic resistance
            double gm = BetaF / rBe; // Transconductance
            double vDiff = vbe - 0.65;

            ctx.StampConductance(b, e, 1.0 / rBe);
            if (IsNpn)
            {
                // Current from C to E = gm * (vbe - 0.65)
                ctx.StampCurrentSource(c, e, gm * vDiff);
            }
            else
            {
                ctx.StampCurrentSource(e, c, gm * vDiff);
            }
        }
    }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 3)
        {
            double vb = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vc = ctx.GetNodeVoltage(Pins[1].NodeId);
            double ve = ctx.GetNodeVoltage(Pins[2].NodeId);

            VoltageDrop = vc - ve;
            double vbe = IsNpn ? (vb - ve) : (ve - vb);
            double rBe = 1000.0;
            double gm = BetaF / rBe;
            Current = vbe > 0.65 ? gm * (vbe - 0.65) : 0.0;
        }
    }
}
