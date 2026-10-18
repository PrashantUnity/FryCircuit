using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Logic;

/// <summary>
/// 74193 4-Bit Synchronous Up/Down Binary Counter IC.
/// Inputs:
///   0: CLOCK / UP (rising edge triggers count up)
///   1: DOWN (rising edge triggers count down)
///   2: RESET (active high asynchronous clear)
/// Outputs:
///   3: Q0 (1)
///   4: Q1 (2)
///   5: Q2 (4)
///   6: Q3 (8)
///   7: CARRY (active low on overflow 15->0)
///   8: BORROW (active low on underflow 0->15)
/// </summary>
public class BinaryCounterDevice : Device, IDeviceSimulation
{
    public int CounterValue { get; set; } = 0;
    private bool _lastClockHigh = false;
    private bool _lastDownHigh = false;

    public BinaryCounterDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "counter4", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1.5, -0.9),  // 0: UP / CLK
            new(1, 0, -1.5, -0.3),  // 1: DOWN
            new(2, 0, -1.5, 0.9),   // 2: RESET

            new(3, 0, 1.5, -0.9),   // 3: Q0 (1)
            new(4, 0, 1.5, -0.3),   // 4: Q1 (2)
            new(5, 0, 1.5, 0.3),    // 5: Q2 (4)
            new(6, 0, 1.5, 0.9),    // 6: Q3 (8)
            new(7, 0, 1.5, 1.5),    // 7: CARRY
            new(8, 0, -1.5, 1.5)    // 8: BORROW
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 7) return;

        double rOut = 50.0;
        for (int bit = 0; bit < 4; bit++)
        {
            int outPin = Pins[3 + bit].NodeId;
            if (outPin <= 0) continue;

            bool bitHigh = ((CounterValue >> bit) & 1) != 0;
            double targetV = bitHigh ? 5.0 : 0.0;

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
        if (Pins.Count < 3) return;

        double vReset = ctx.GetNodeVoltage(Pins[2].NodeId);
        if (vReset > 2.0)
        {
            CounterValue = 0;
            return;
        }

        double vUp = ctx.GetNodeVoltage(Pins[0].NodeId);
        bool clockHigh = vUp > 2.0;
        if (clockHigh && !_lastClockHigh)
        {
            // Rising edge on UP/CLOCK
            CounterValue = (CounterValue + 1) & 0x0F;
        }
        _lastClockHigh = clockHigh;

        double vDown = ctx.GetNodeVoltage(Pins[1].NodeId);
        bool downHigh = vDown > 2.0;
        if (downHigh && !_lastDownHigh)
        {
            // Rising edge on DOWN
            CounterValue = (CounterValue - 1) & 0x0F;
        }
        _lastDownHigh = downHigh;
    }
}
