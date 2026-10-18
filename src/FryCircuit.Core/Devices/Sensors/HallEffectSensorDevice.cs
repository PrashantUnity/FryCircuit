using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Semiconductor Hall Effect Sensor for Drone BLDC Motor Commutation, Electronic Speed Controllers (ESC),
/// and Aerospace contactless current/position sensing.
/// Converts rotor magnetic flux density B (Gauss / Tesla) into a differential voltage signal.
/// </summary>
public class HallEffectSensorDevice : Device, IDeviceSimulation
{
    /// <summary>Magnetic sensitivity in V/Tesla (typically 10 to 50 V/T or 1.0 to 5.0 mV/Gauss)</summary>
    public double SensitivityVoltsPerTesla { get; set; } = 25.0; // 25 V/T = 2.5 mV/G

    /// <summary>External rotor magnetic field B in Tesla (typically -0.1 T to +0.1 T for drone motors)</summary>
    public double MagneticFieldTesla { get; set; } = 0.05; // 500 Gauss default rotor field

    /// <summary>Internal bridge input resistance in Ohms</summary>
    public double InputResistance { get; set; } = 1000.0; // 1 kΩ

    /// <summary>Output impedance in Ohms</summary>
    public double OutputResistance { get; set; } = 100.0; // 100 Ω

    /// <summary>Quiescent offset voltage at B=0 (typically Vcc / 2)</summary>
    public double OffsetVoltage { get; set; } = 0.0;

    public HallEffectSensorDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "hallsensor", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -1), // Pin 0: Vcc (Bias +)
            new(1, 0, -1, 1),  // Pin 1: GND (Bias -)
            new(2, 0, 1, -1),  // Pin 2: Out (+)
            new(3, 0, 1, 1)    // Pin 3: Out (-)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // 1. Input supply bridge resistance
        double gIn = 1.0 / Math.Max(1.0, InputResistance);
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gIn);

        // 2. Output stage internal conductance
        double gOut = 1.0 / Math.Max(1.0, OutputResistance);
        ctx.StampConductance(Pins[2].NodeId, Pins[3].NodeId, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        // Compute Hall voltage: V_H = S_H * B + V_offset
        double vHall = SensitivityVoltsPerTesla * MagneticFieldTesla + OffsetVoltage;

        // Norton equivalent output current source: I_eq = V_H / R_out
        double iEq = vHall / Math.Max(1.0, OutputResistance);

        ctx.StampCurrentSource(Pins[3].NodeId, Pins[2].NodeId, iEq);
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vOutPos = ctx.GetNodeVoltage(Pins[2].NodeId);
        double vOutNeg = ctx.GetNodeVoltage(Pins[3].NodeId);
        VoltageDrop = vOutPos - vOutNeg;
        double vHall = SensitivityVoltsPerTesla * MagneticFieldTesla + OffsetVoltage;
        double rOut = Math.Max(1.0, OutputResistance);
        Current = (vHall - VoltageDrop) / rOut;
    }
}
