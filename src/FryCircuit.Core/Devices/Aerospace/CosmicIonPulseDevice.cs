using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Heavy-ion cosmic ray strike current generator for Spacecraft, Satellite, and Avionics SEU testing.
/// Simulates Single Event Transients (SET) and Single Event Upsets (SEU) from galactic cosmic rays (GCR).
/// Double exponential pulse model:
///   I(t) = I_peak * (exp(-(t - t_0)/tau_2) - exp(-(t - t_0)/tau_1)) for t >= t_0
///   Q_total = I_peak * (tau_2 - tau_1)  (charge deposited by ion track)
/// </summary>
public class CosmicIonPulseDevice : Device, IDeviceSimulation
{
    /// <summary>Total collected ion charge in Coulombs (typically 0.1 pC to 10 pC based on Linear Energy Transfer LET)</summary>
    public double CollectedCharge { get; set; } = 1.0e-12; // 1.0 pC

    /// <summary>Picosecond rise time of the cosmic ion track (typically 5 ps to 25 ps)</summary>
    public double RiseTimeSeconds { get; set; } = 10.0e-12; // 10 ps

    /// <summary>Picosecond collection/fall time (typically 50 ps to 500 ps)</summary>
    public double FallTimeSeconds { get; set; } = 150.0e-12; // 150 ps

    /// <summary>Cosmic strike time t_0 in seconds</summary>
    public double StrikeTimeSeconds { get; set; } = 1.0e-6; // 1.0 µs default strike

    /// <summary>Periodic strike interval (0.0 for one-shot strike)</summary>
    public double StrikePeriodSeconds { get; set; } = 0.0;

    /// <summary>Peak current of the radiation strike in Amperes</summary>
    public double PeakCurrent
    {
        get
        {
            double dt = Math.Max(1e-15, FallTimeSeconds - RiseTimeSeconds);
            return CollectedCharge / dt;
        }
    }

    public CosmicIonPulseDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "cosmicipulse", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Positive terminal (Strike target node)
            new(1, 0, 0, 1)   // Negative terminal (Reference / Substrate)
        };
    }

    public double GetCurrentAtTime(double t)
    {
        double effectiveTime = t;
        if (StrikePeriodSeconds > 0)
        {
            effectiveTime = t % StrikePeriodSeconds;
        }

        if (effectiveTime < StrikeTimeSeconds) return 0.0;

        double deltaT = effectiveTime - StrikeTimeSeconds;
        double tau1 = Math.Max(1e-15, RiseTimeSeconds);
        double tau2 = Math.Max(2e-15, FallTimeSeconds);

        // Double-exponential ionizing pulse
        double pulse = Math.Exp(-deltaT / tau2) - Math.Exp(-deltaT / tau1);
        return Math.Max(0.0, PeakCurrent * pulse);
    }

    public void StampConductance(MnaContext ctx)
    {
        // Pure current source does not affect conductance matrix
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        double current = GetCurrentAtTime(ctx.CurrentTime);
        Current = current;
        ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, current);
    }

    public void StampNonLinear(MnaContext ctx)
    {
        // Linear transient current source
    }

    public void UpdateState(MnaContext ctx)
    {
        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = v0 - v1;
    }
}
