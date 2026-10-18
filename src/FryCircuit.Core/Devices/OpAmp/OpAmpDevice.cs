using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class OpAmpDevice : Device, IDeviceSimulation
{
    public double Gain
    {
        get => GetParameter("gain", 100000.0);
        set => SetParameter("gain", Math.Max(1.0, value));
    }

    public double MaxVoltage
    {
        get => GetParameter("max", 15.0);
        set => SetParameter("max", value);
    }

    public double MinVoltage
    {
        get => GetParameter("min", -15.0);
        set => SetParameter("min", value);
    }

    public OpAmpDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "opamp", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -1), // Inverting (-)
            new(1, 0, -1, 1),  // Non-inverting (+)
            new(2, 0, 1, 0)    // Output
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 3) return;

        double vInverting = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vNonInverting = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vDiff = vNonInverting - vInverting;
        double targetV = Math.Clamp(vDiff * Gain, MinVoltage, MaxVoltage);

        // Output behaves as voltage source with small series resistance
        double rOut = 1.0;
        ctx.StampConductance(Pins[2].NodeId, -1, 1.0 / rOut);
        if (ctx.NodeIndexMap.TryGetValue(Pins[2].NodeId, out int outRow))
        {
            ctx.B[outRow] += targetV / rOut;
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 3)
        {
            double vInverting = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vNonInverting = ctx.GetNodeVoltage(Pins[1].NodeId);
            double vDiff = vNonInverting - vInverting;
            double targetV = Math.Clamp(vDiff * Gain, MinVoltage, MaxVoltage);

            double vOut = ctx.GetNodeVoltage(Pins[2].NodeId);
            VoltageDrop = vOut;
            double rOut = 1.0;
            Current = (targetV - vOut) / rOut;
        }
    }
}
