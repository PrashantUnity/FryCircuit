using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Display;

/// <summary>
/// High-fidelity 7-Segment LED Numerical & Alphanumeric Display.
/// Pins:
///   0: Segment a (Top horizontal)
///   1: Segment b (Top-right vertical)
///   2: Segment c (Bottom-right vertical)
///   3: Segment d (Bottom horizontal)
///   4: Segment e (Bottom-left vertical)
///   5: Segment f (Top-left vertical)
///   6: Segment g (Middle horizontal)
///   7: COM (Common Cathode / Ground)
///   8: DP (Decimal Point)
/// </summary>
public class SevenSegmentDisplayDevice : Device, IDeviceSimulation
{
    public bool[] SegmentState { get; } = new bool[7];
    public double[] SegmentBrightness { get; } = new double[7];
    public bool DpState { get; set; } = false;
    public double DpBrightness { get; set; } = 0.0;

    /// <summary>
    /// Optional manual hex override (0-15: '0'-'9', 'A'-'F').
    /// If null, segments are driven dynamically by pin voltages.
    /// </summary>
    public int? ManualHexValue { get; set; } = null;

    /// <summary>
    /// LED emission color: "green", "red", "amber", "blue", "cyan", "white".
    /// </summary>
    public string LedColor { get; set; } = "green";

    public SevenSegmentDisplayDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "seg7", x, y)
    {
        // 7 Segment inputs on left edge, common cathode at bottom, DP at right
        Pins = new List<Pin>
        {
            new(0, 0, -1.5, -1.2),  // a: Pin 0 (left top)
            new(1, 0, -1.5, -0.8),  // b: Pin 1
            new(2, 0, -1.5, -0.4),  // c: Pin 2
            new(3, 0, -1.5, 0.0),   // d: Pin 3 (left middle)
            new(4, 0, -1.5, 0.4),   // e: Pin 4
            new(5, 0, -1.5, 0.8),   // f: Pin 5
            new(6, 0, -1.5, 1.2),   // g: Pin 6 (left bottom)
            new(7, 0, 0.0, 1.8),    // COM: Pin 7 (bottom center)
            new(8, 0, 1.5, 1.2)     // DP: Pin 8 (right bottom)
        };
    }

    public static readonly byte[] HexSegmentPatterns = new byte[]
    {
        0b00111111, // 0: a,b,c,d,e,f
        0b00000110, // 1: b,c
        0b01011011, // 2: a,b,d,e,g
        0b01001111, // 3: a,b,c,d,g
        0b01100110, // 4: b,c,f,g
        0b01101101, // 5: a,c,d,f,g
        0b01111101, // 6: a,c,d,e,f,g
        0b00000111, // 7: a,b,c
        0b01111111, // 8: a,b,c,d,e,f,g
        0b01101111, // 9: a,b,c,d,f,g
        0b01110111, // A: a,b,c,e,f,g
        0b01111100, // b: c,d,e,f,g
        0b00111001, // C: a,d,e,f
        0b01011110, // d: b,c,d,e,g
        0b01111001, // E: a,d,e,f,g
        0b01110001  // F: a,e,f,g
    };

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 8) return;
        int comNode = Pins[7].NodeId;

        // Stamp forward LED conductance for each connected segment
        for (int i = 0; i < 7; i++)
        {
            int pinNode = Pins[i].NodeId;
            if (pinNode <= 0) continue;

            double vPin = ctx.GetNodeVoltage(pinNode);
            double vCom = ctx.GetNodeVoltage(comNode);
            double vDrop = vPin - vCom;

            if (vDrop > 1.6)
            {
                // Dynamic forward resistance ~100 Ohm
                double g = 1.0 / 100.0;
                ctx.StampConductance(pinNode, comNode, g);
                ctx.StampCurrentSource(pinNode, comNode, 1.6 * g);
            }
            else
            {
                // Tiny leakage to keep matrix well-conditioned
                ctx.StampConductance(pinNode, comNode, 1e-6);
            }
        }

        // DP pin
        if (Pins.Count > 8 && Pins[8].NodeId > 0)
        {
            int dpNode = Pins[8].NodeId;
            double vDrop = ctx.GetNodeVoltage(dpNode) - ctx.GetNodeVoltage(comNode);
            if (vDrop > 1.6)
            {
                double g = 1.0 / 100.0;
                ctx.StampConductance(dpNode, comNode, g);
                ctx.StampCurrentSource(dpNode, comNode, 1.6 * g);
            }
            else
            {
                ctx.StampConductance(dpNode, comNode, 1e-6);
            }
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (ManualHexValue.HasValue)
        {
            int val = Math.Clamp(ManualHexValue.Value, 0, 15);
            byte pattern = HexSegmentPatterns[val];
            for (int i = 0; i < 7; i++)
            {
                bool lit = ((pattern >> i) & 1) != 0;
                SegmentState[i] = lit;
                SegmentBrightness[i] = lit ? 1.0 : 0.0;
            }
            return;
        }

        if (Pins.Count < 8) return;
        int comNode = Pins[7].NodeId;
        double vCom = ctx.GetNodeVoltage(comNode);

        for (int i = 0; i < 7; i++)
        {
            int pinNode = Pins[i].NodeId;
            double vPin = ctx.GetNodeVoltage(pinNode);
            double vDrop = vPin - vCom;

            double brightness = Math.Clamp((vDrop - 1.5) / 1.0, 0.0, 1.0);
            SegmentState[i] = brightness > 0.25;
            SegmentBrightness[i] = brightness;
        }

        if (Pins.Count > 8 && Pins[8].NodeId > 0)
        {
            double vDrop = ctx.GetNodeVoltage(Pins[8].NodeId) - vCom;
            double brightness = Math.Clamp((vDrop - 1.5) / 1.0, 0.0, 1.0);
            DpState = brightness > 0.25;
            DpBrightness = brightness;
        }
    }
}
