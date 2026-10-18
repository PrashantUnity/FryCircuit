using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

/// <summary>
/// NE555 Precision Timer Integrated Circuit.
/// Pins:
///   0: GND (Pin 1)
///   1: TRIG (Pin 2)
///   2: OUT (Pin 3)
///   3: RESET (Pin 4)
///   4: CTRL (Pin 5)
///   5: THRESH (Pin 6)
///   6: DISCH (Pin 7)
///   7: VCC (Pin 8)
/// </summary>
public class Timer555Device : Device, IDeviceSimulation
{
    public bool LatchState { get; set; } = true;
    public double OutputResistance { get; set; } = 20.0;
    public double DischargeOnResistance { get; set; } = 10.0;
    public double DividerResistor { get; set; } = 5000.0; // 5kΩ internal ladder

    public Timer555Device(int id = 0, double x = 0, double y = 0)
        : base(id, "timer555", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, 1.5),      // Pin 1: GND (bottom)
            new(1, 0, -1.5, 0.8),   // Pin 2: TRIG (left-bottom)
            new(2, 0, 1.5, 0.0),    // Pin 3: OUT (right)
            new(3, 0, -1.5, -1.0),  // Pin 4: RESET (left-top)
            new(4, 0, 1.5, 1.0),    // Pin 5: CTRL (right-bottom)
            new(5, 0, -1.5, 0.2),   // Pin 6: THRESH (left-middle)
            new(6, 0, -1.5, -0.4),  // Pin 7: DISCH (left-upper)
            new(7, 0, 0, -1.5)      // Pin 8: VCC (top)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        int gndNode = Pins[0].NodeId;
        int trigNode = Pins[1].NodeId;
        int outNode = Pins[2].NodeId;
        int resetNode = Pins[3].NodeId;
        int ctrlNode = Pins[4].NodeId;
        int threshNode = Pins[5].NodeId;
        int dischNode = Pins[6].NodeId;
        int vccNode = Pins[7].NodeId;

        double vGnd = ctx.GetNodeVoltage(gndNode);
        double vVcc = ctx.GetNodeVoltage(vccNode);
        double vThresh = ctx.GetNodeVoltage(threshNode);
        double vTrig = ctx.GetNodeVoltage(trigNode);
        double vReset = ctx.GetNodeVoltage(resetNode);
        double vCtrl = ctx.GetNodeVoltage(ctrlNode);

        // Internal reference levels
        double supplySpan = Math.Max(0.1, vVcc - vGnd);
        double refUpper = (Math.Abs(vCtrl) > 0.05) ? vCtrl : vGnd + (2.0 / 3.0) * supplySpan;
        double refLower = (Math.Abs(vCtrl) > 0.05) ? (vGnd + 0.5 * (vCtrl - vGnd)) : vGnd + (1.0 / 3.0) * supplySpan;

        // Reset pin active low (< 0.7V)
        if (vReset < vGnd + 0.7)
        {
            LatchState = false;
        }
        else
        {
            // Comparator 2 (Trigger) sets output HIGH if V_trig < 1/3 Vcc
            if (vTrig < refLower)
            {
                LatchState = true;
            }
            // Comparator 1 (Threshold) sets output LOW if V_thresh > 2/3 Vcc
            else if (vThresh > refUpper)
            {
                LatchState = false;
            }
        }

        // 1. Output Driver
        double vOutTarget = LatchState ? Math.Max(vGnd, vVcc - 1.2) : (vGnd + 0.1);
        double gOut = 1.0 / OutputResistance;
        ctx.StampConductance(outNode, -1, gOut);
        if (ctx.NodeIndexMap.TryGetValue(outNode, out int outRow))
        {
            ctx.B[outRow] += vOutTarget * gOut;
        }

        // 2. Discharge Transistor (Conducts to GND when Latch is false)
        double gDisch = LatchState ? 1e-7 : (1.0 / DischargeOnResistance);
        ctx.StampConductance(dischNode, gndNode, gDisch);

        // 3. Internal 5k divider resistance across supply
        double gDivider = 1.0 / (3.0 * DividerResistor);
        ctx.StampConductance(vccNode, gndNode, gDivider);
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        int gndNode = Pins[0].NodeId;
        int outNode = Pins[2].NodeId;
        int vccNode = Pins[7].NodeId;

        double vGnd = ctx.GetNodeVoltage(gndNode);
        double vOut = ctx.GetNodeVoltage(outNode);
        double vVcc = ctx.GetNodeVoltage(vccNode);

        VoltageDrop = vOut - vGnd;
        double vOutTarget = LatchState ? Math.Max(vGnd, vVcc - 1.2) : (vGnd + 0.1);
        double iOut = (vOutTarget - vOut) / OutputResistance;
        double iSupply = (vVcc - vGnd) / (3.0 * DividerResistor);
        Current = iSupply + Math.Max(0.0, iOut);
    }
}
