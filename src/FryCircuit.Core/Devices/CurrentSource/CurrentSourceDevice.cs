using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class CurrentSourceDevice : Device, IDeviceSimulation
{
    public double CurrentValue
    {
        get => GetParameter("current", 0.001);
        set => SetParameter("current", value);
    }

    public CurrentSourceDevice(int id = 0, double x = 0, double y = 0, double current = 0.001)
        : base(id, "csource", x, y)
    {
        CurrentValue = current;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx) { }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, CurrentValue);
        }
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;
            Current = CurrentValue;
        }
    }
}
