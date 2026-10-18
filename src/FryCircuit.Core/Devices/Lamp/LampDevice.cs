using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class LampDevice : Device, IDeviceSimulation
{
    public double NominalPower
    {
        get => GetParameter("power", 60.0);
        set => SetParameter("power", Math.Max(0.1, value));
    }

    public double NominalVoltage
    {
        get => GetParameter("voltage", 12.0);
        set => SetParameter("voltage", Math.Max(0.1, value));
    }

    public double Resistance => (NominalVoltage * NominalVoltage) / NominalPower;

    public LampDevice(int id = 0, double x = 0, double y = 0, double nominalVoltage = 12.0, double nominalPower = 60.0)
        : base(id, "lamp", x, y)
    {
        NominalVoltage = nominalVoltage;
        NominalPower = nominalPower;
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
