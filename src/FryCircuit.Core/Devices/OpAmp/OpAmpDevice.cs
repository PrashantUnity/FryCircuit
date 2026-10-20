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

        int invNode = Pins[0].NodeId;
        int nonInvNode = Pins[1].NodeId;
        int outNode = Pins[2].NodeId;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, -1, gOut);

        double vInverting = ctx.GetNodeVoltage(invNode);
        double vNonInverting = ctx.GetNodeVoltage(nonInvNode);
        double vDiff = vNonInverting - vInverting;

        double vDiffMax = MaxVoltage / Gain;
        double vDiffMin = MinVoltage / Gain;

        if (vDiff >= vDiffMin && vDiff <= vDiffMax)
        {
            // Linear operating region: stamp small-signal transconductance into matrix G
            // Equation: I_out = gOut * V_out - gm * V_plus + gm * V_minus
            double gm = Gain * gOut;
            if (ctx.NodeIndexMap.TryGetValue(outNode, out int outRow))
            {
                if (ctx.NodeIndexMap.TryGetValue(nonInvNode, out int nonInvCol))
                {
                    ctx.G[outRow, nonInvCol] -= gm;
                }
                if (ctx.NodeIndexMap.TryGetValue(invNode, out int invCol))
                {
                    ctx.G[outRow, invCol] += gm;
                }
            }
        }
        else
        {
            // Saturated region: clamped at power rails
            double targetV = vDiff > vDiffMax ? MaxVoltage : MinVoltage;
            if (ctx.NodeIndexMap.TryGetValue(outNode, out int outRow))
            {
                ctx.B[outRow] += targetV * gOut;
            }
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
