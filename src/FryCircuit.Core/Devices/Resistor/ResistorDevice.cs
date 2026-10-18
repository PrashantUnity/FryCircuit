using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class ResistorDevice : Device, IDeviceSimulation
{
    public double Resistance
    {
        get => GetParameter("resistance", 1000.0);
        set => SetParameter("resistance", Math.Max(1e-6, value));
    }

    public ResistorDevice(int id = 0, double x = 0, double y = 0, double resistance = 1000.0)
        : base(id, "resistor", x, y)
    {
        Resistance = resistance;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1.0 / Resistance);
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;
            Current = VoltageDrop / Resistance;
        }
    }
}
