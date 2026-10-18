namespace FryCircuit.Core.Devices;

public class ZenerDiodeDevice : DiodeDevice
{
    public double BreakdownVoltage
    {
        get => GetParameter("breakdown", 5.1);
        set => SetParameter("breakdown", Math.Max(0.1, value));
    }

    public double ZenerResistance
    {
        get => GetParameter("zener_r", 5.0);
        set => SetParameter("zener_r", Math.Max(0.01, value));
    }

    public ZenerDiodeDevice(int id = 0, double x = 0, double y = 0, double breakdown = 5.1, double zenerR = 5.0)
        : base(id, x, y)
    {
        Type = "zener";
        BreakdownVoltage = breakdown;
        ZenerResistance = zenerR;
    }

    public override void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        if (vd < -BreakdownVoltage)
        {
            // Zener reverse breakdown with dynamic resistance
            double gz = 1.0 / Math.Max(0.01, ZenerResistance);
            double id = -gz * (-vd - BreakdownVoltage);
            double ieq = id - gz * vd;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gz);
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, ieq);
        }
        else
        {
            base.StampNonLinear(ctx);
        }
    }

    public override void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vd;
        if (vd < -BreakdownVoltage)
        {
            double gz = 1.0 / Math.Max(0.01, ZenerResistance);
            Current = -gz * (-vd - BreakdownVoltage);
        }
        else
        {
            base.UpdateState(ctx);
        }
    }
}
