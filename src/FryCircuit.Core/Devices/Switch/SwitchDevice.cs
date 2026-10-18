using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class SwitchDevice : Device, IDeviceSimulation
{
    public bool IsClosed
    {
        get => GetParameter("state", 0.0) > 0.5;
        set => SetParameter("state", value ? 1.0 : 0.0);
    }

    public SwitchDevice(int id = 0, string type = "spst", double x = 0, double y = 0)
        : base(id, type, x, y)
    {
        if (type.Equals("spdt", StringComparison.OrdinalIgnoreCase))
        {
            Pins = new List<Pin>
            {
                new(0, 0, -1, 0), // Common
                new(1, 0, 1, -1), // Throw 1
                new(2, 0, 1, 1)   // Throw 2
            };
        }
        else
        {
            Pins = new List<Pin>
            {
                new(0, 0, 0, -1),
                new(1, 0, 0, 1)
            };
        }
    }

    public void Toggle()
    {
        IsClosed = !IsClosed;
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Type.Equals("spdt", StringComparison.OrdinalIgnoreCase))
        {
            if (Pins.Count >= 3)
            {
                // When IsClosed: Common connected to Throw 1, else Common connected to Throw 2
                double r1 = IsClosed ? 1e-4 : 1e12;
                double r2 = IsClosed ? 1e12 : 1e-4;
                ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1.0 / r1);
                ctx.StampConductance(Pins[0].NodeId, Pins[2].NodeId, 1.0 / r2);
            }
        }
        else if (Pins.Count >= 2)
        {
            double r = IsClosed ? 1e-4 : 1e12;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1.0 / r);
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            if (Type.Equals("spdt", StringComparison.OrdinalIgnoreCase) && Pins.Count >= 3)
            {
                int activeThrow = IsClosed ? 1 : 2;
                double vActive = ctx.GetNodeVoltage(Pins[activeThrow].NodeId);
                VoltageDrop = v0 - vActive;
                Current = VoltageDrop / 1e-4;
            }
            else
            {
                double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
                VoltageDrop = v0 - v1;
                Current = IsClosed ? VoltageDrop / 1e-4 : 0.0;
            }
        }
    }
}
