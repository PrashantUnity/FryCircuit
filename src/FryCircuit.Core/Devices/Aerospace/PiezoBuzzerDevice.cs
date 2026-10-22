using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// Piezoelectric Drone Loss-Alarm, Telemetry Chirper, and Battery Warning Buzzer.
/// Simulates resonant acoustic transducer physics, sound pressure level (dB SPL @ 10cm),
/// minimum activation threshold, and dynamic acoustic wave emission.
/// </summary>
public class PiezoBuzzerDevice : Device, IDeviceSimulation
{
    /// <summary>Resonant acoustic audio frequency in Hertz (typically 2700 Hz or 4000 Hz)</summary>
    public double ResonantFrequencyHz { get; set; } = 2700.0;

    /// <summary>Nominal design operating voltage (e.g. 5.0V or 12.0V DC)</summary>
    public double NominalVoltage { get; set; } = 5.0;

    /// <summary>Sound pressure level in decibels (dBA @ 10cm) at nominal voltage</summary>
    public double RatedDecibelsSpl { get; set; } = 85.0;

    /// <summary>Operating current at nominal voltage in Amperes (typically 15 mA to 30 mA)</summary>
    public double NominalCurrent { get; set; } = 0.018; // 18 mA

    /// <summary>Minimum turn-on trigger voltage in Volts</summary>
    public double MinOperateVoltage { get; set; } = 2.5;

    // --- Dynamic Simulation & Animation State ---
    /// <summary>Indicates whether the buzzer is currently emitting audible sound</summary>
    public bool IsSounding => Math.Abs(VoltageDrop) >= MinOperateVoltage;

    /// <summary>Instantaneous sound pressure level in decibels (0.0 when silent)</summary>
    public double ActualDecibelsSpl
    {
        get
        {
            double v = Math.Abs(VoltageDrop);
            if (v < MinOperateVoltage) return 0.0;
            double ratio = v / Math.Max(0.5, NominalVoltage);
            return Math.Clamp(RatedDecibelsSpl + 20.0 * Math.Log10(ratio), 40.0, 110.0);
        }
    }

    /// <summary>Acoustic animation wave phase for UI ripple expansion</summary>
    public double AcousticPhase { get; set; } = 0.0;

    /// <summary>Equivalent DC coil/piezo impedance in Ohms</summary>
    public double EquivalentResistance => NominalVoltage / Math.Max(0.001, NominalCurrent);

    public PiezoBuzzerDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "buzzer", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Positive terminal (+) [Top]
            new(1, 0, 0, 1)   // Negative terminal (-) [Bottom]
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        double g = 1.0 / Math.Max(1.0, EquivalentResistance);
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, g);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = v0 - v1;

        if (IsSounding)
        {
            Current = Math.Abs(VoltageDrop) / EquivalentResistance;
            double dt = ctx.Dt;
            if (dt > 0)
            {
                AcousticPhase = (AcousticPhase + 2.0 * Math.PI * 8.0 * dt) % (2.0 * Math.PI);
            }
        }
        else
        {
            Current = Math.Abs(VoltageDrop) / (EquivalentResistance * 10.0);
        }
    }
}
