using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class PotentiometerDevice : Device, IDeviceSimulation
{
    public double Resistance
    {
        get => GetParameter("resistance", 10000.0);
        set => SetParameter("resistance", Math.Max(1e-3, value));
    }

    public double Wiper
    {
        get => GetParameter("wiper", 0.5);
        set => SetParameter("wiper", Math.Clamp(value, 0.001, 0.999));
    }

    public PotentiometerDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "potentiometer", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Terminal A (Up)
            new(1, 0, 0, 1),  // Terminal B (Down)
            new(2, 0, 1, 0)   // Wiper (Right)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        double rTop = Resistance * (1.0 - Wiper);
        double rBottom = Resistance * Wiper;
        // Terminal A (pin 0, top) to Wiper (pin 2)
        ctx.StampConductance(Pins[0].NodeId, Pins[2].NodeId, 1.0 / Math.Max(1e-4, rTop));
        // Wiper (pin 2) to Terminal B (pin 1, bottom)
        ctx.StampConductance(Pins[2].NodeId, Pins[1].NodeId, 1.0 / Math.Max(1e-4, rBottom));
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 3)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            double v2 = ctx.GetNodeVoltage(Pins[2].NodeId);
            VoltageDrop = v0 - v1;
            double rTop = Math.Max(1e-4, Resistance * (1.0 - Wiper));
            Current = (v0 - v2) / rTop;
        }
    }
}
