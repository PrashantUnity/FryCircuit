using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Display;

/// <summary>
/// 7448/7447 BCD to 7-Segment Active-High Decoder Driver IC.
/// Inputs (Left):
///   0: A (Bit 0, LSB)
///   1: B (Bit 1)
///   2: C (Bit 2)
///   3: D (Bit 3, MSB)
/// Outputs (Right):
///   4: a
///   5: b
///   6: c
///   7: d
///   8: e
///   9: f
///   10: g
/// </summary>
public class BcdTo7SegmentDevice : Device, IDeviceSimulation
{
    public int DecodedValue { get; private set; } = 0;
    public byte CurrentPattern { get; private set; } = 0;

    public BcdTo7SegmentDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "bcd7seg", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1.5, -0.6),  // In 0: A
            new(1, 0, -1.5, -0.2),  // In 1: B
            new(2, 0, -1.5, 0.2),   // In 2: C
            new(3, 0, -1.5, 0.6),   // In 3: D

            new(4, 0, 1.5, -1.2),   // Out 0: a
            new(5, 0, 1.5, -0.8),   // Out 1: b
            new(6, 0, 1.5, -0.4),   // Out 2: c
            new(7, 0, 1.5, 0.0),    // Out 3: d
            new(8, 0, 1.5, 0.4),    // Out 4: e
            new(9, 0, 1.5, 0.8),    // Out 5: f
            new(10, 0, 1.5, 1.2),   // Out 6: g
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 11) return;

        // Evaluate BCD inputs
        bool a = ctx.GetNodeVoltage(Pins[0].NodeId) > 2.0;
        bool b = ctx.GetNodeVoltage(Pins[1].NodeId) > 2.0;
        bool c = ctx.GetNodeVoltage(Pins[2].NodeId) > 2.0;
        bool d = ctx.GetNodeVoltage(Pins[3].NodeId) > 2.0;

        int val = (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);
        DecodedValue = val;
        byte pattern = SevenSegmentDisplayDevice.HexSegmentPatterns[val];
        CurrentPattern = pattern;

        // Drive the 7 output lines
        double rOut = 50.0;
        for (int i = 0; i < 7; i++)
        {
            int outPin = Pins[4 + i].NodeId;
            if (outPin <= 0) continue;

            bool segHigh = ((pattern >> i) & 1) != 0;
            double targetV = segHigh ? 5.0 : 0.0;

            ctx.StampConductance(outPin, -1, 1.0 / rOut);
            if (ctx.NodeIndexMap.TryGetValue(outPin, out int outRow))
            {
                ctx.B[outRow] += targetV / rOut;
            }
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 11) return;

        bool a = ctx.GetNodeVoltage(Pins[0].NodeId) > 2.0;
        bool b = ctx.GetNodeVoltage(Pins[1].NodeId) > 2.0;
        bool c = ctx.GetNodeVoltage(Pins[2].NodeId) > 2.0;
        bool d = ctx.GetNodeVoltage(Pins[3].NodeId) > 2.0;

        DecodedValue = (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);
        CurrentPattern = SevenSegmentDisplayDevice.HexSegmentPatterns[DecodedValue];
    }
}
