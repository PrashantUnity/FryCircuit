using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class LogicGateDevice : Device, IDeviceSimulation
{
    public string GateType { get; set; }

    public LogicGateDevice(int id = 0, string type = "and", double x = 0, double y = 0)
        : base(id, type, x, y)
    {
        GateType = type.ToLowerInvariant();
        if (GateType == "not")
        {
            Pins = new List<Pin>
            {
                new(0, 0, -1, 0), // Input
                new(1, 0, 1, 0)   // Output
            };
        }
        else
        {
            Pins = new List<Pin>
            {
                new(0, 0, -1, -1), // Input A
                new(1, 0, -1, 1),  // Input B
                new(2, 0, 1, 0)    // Output
            };
        }
    }

    public bool Evaluate(bool a, bool b = false)
    {
        return GateType switch
        {
            "not" => !a,
            "and" => a && b,
            "or" => a || b,
            "nand" => !(a && b),
            "nor" => !(a || b),
            "xor" => a ^ b,
            "xnor" => !(a ^ b),
            _ => false
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        int outPinIdx = GateType == "not" ? 1 : 2;
        if (Pins.Count > outPinIdx)
        {
            // Evaluate logic from inputs
            double vA = ctx.GetNodeVoltage(Pins[0].NodeId);
            bool logicA = vA > 2.0;

            bool logicB = false;
            if (GateType != "not" && Pins.Count >= 2)
            {
                double vB = ctx.GetNodeVoltage(Pins[1].NodeId);
                logicB = vB > 2.0;
            }

            bool result = Evaluate(logicA, logicB);
            double vOutTarget = result ? 5.0 : 0.0;

            // Output acts as 50 Ohm driver
            double rOut = 50.0;
            ctx.StampConductance(Pins[outPinIdx].NodeId, -1, 1.0 / rOut);
            if (ctx.NodeIndexMap.TryGetValue(Pins[outPinIdx].NodeId, out int outRow))
            {
                ctx.B[outRow] += vOutTarget / rOut;
            }
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        int outPinIdx = GateType == "not" ? 1 : 2;
        if (Pins.Count > outPinIdx)
        {
            double vA = ctx.GetNodeVoltage(Pins[0].NodeId);
            bool logicA = vA > 2.0;

            bool logicB = false;
            if (GateType != "not" && Pins.Count >= 2)
            {
                double vB = ctx.GetNodeVoltage(Pins[1].NodeId);
                logicB = vB > 2.0;
            }

            bool result = Evaluate(logicA, logicB);
            double vOutTarget = result ? 5.0 : 0.0;
            double vOut = ctx.GetNodeVoltage(Pins[outPinIdx].NodeId);

            VoltageDrop = vOut;
            double rOut = 50.0;
            Current = (vOutTarget - vOut) / rOut;
        }
    }
}
