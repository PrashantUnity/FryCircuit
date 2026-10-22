using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// Professional Drone Electronic Speed Controller (ESC) 3-Phase Inverter Power Stage.
/// Bridges DC power bus (+, -) and flight controller throttle PWM command (0 - 3.3V / 5V)
/// to commutated 3-Phase outputs (U, V, W) with low-RDS(on) GaN/MOSFET switching and active braking.
/// </summary>
public class EscDevice : Device, IDeviceSimulation
{
    /// <summary>Continuous current rating in Amperes (e.g. 45A 4-in-1 ESC)</summary>
    public double ContinuousCurrentRating { get; set; } = 45.0;

    /// <summary>MOSFET half-bridge channel on-resistance Rds(on) in Ohms</summary>
    public double MosfetRdsOn { get; set; } = 0.0025; // 2.5 mΩ

    /// <summary>Manual throttle override (0.0 = 0%, 1.0 = 100%) when no external PWM signal is wired</summary>
    public double ThrottlePercent { get; set; } = 0.75; // 75% default throttle

    /// <summary>Quiescent logic operating current in Amperes</summary>
    public double QuiescentCurrent { get; set; } = 0.035; // 35 mA

    /// <summary>Instantaneous computed throttle fraction (0.0 to 1.0)</summary>
    public double EffectiveThrottle { get; private set; } = 0.0;

    /// <summary>Internal commutation electrical phase angle in radians</summary>
    private double _commutationAngle = 0.0;

    public EscDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "esc", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, -1), // Pin 0: DC Bus (+) [Top Left]
            new(1, 0, -2, 1),  // Pin 1: DC Bus (-) / GND [Bottom Left]
            new(2, 0, -2, 0),  // Pin 2: PWM Throttle Input [Middle Left]
            new(3, 0, 2, -1),  // Pin 3: Motor Phase U [Top Right]
            new(4, 0, 2, 0),   // Pin 4: Motor Phase V [Middle Right]
            new(5, 0, 2, 1)    // Pin 5: Motor Phase W [Bottom Right]
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        int vbat = Pins[0].NodeId;
        int gnd = Pins[1].NodeId;
        int u = Pins[3].NodeId;
        int v = Pins[4].NodeId;
        int w = Pins[5].NodeId;

        // Quiescent supply conductance
        ctx.StampConductance(vbat, gnd, QuiescentCurrent / 12.0);

        // High-side and low-side bridge conduction based on effective throttle
        double duty = Math.Clamp(EffectiveThrottle, 0.0, 1.0);
        if (duty > 0.01)
        {
            // Effective switch branch resistance including MOSFET Rds(on), current sense shunt resistor, and active cycle-by-cycle current limit
            double rSwitch = Math.Max(0.12, (12.0 / (ContinuousCurrentRating * 1.15)) * (1.0 - 0.4 * duty));
            double gSwitch = duty / rSwitch;

            // 6-step trapezoidal commutation modulation across phases
            double ca = _commutationAngle;
            double weightU = Math.Max(0.0, Math.Sin(ca));
            double weightV = Math.Max(0.0, Math.Sin(ca - 2.0 * Math.PI / 3.0));
            double weightW = Math.Max(0.0, Math.Sin(ca + 2.0 * Math.PI / 3.0));

            // Conductance from DC+ to high phases
            if (weightU > 0.1) ctx.StampConductance(vbat, u, gSwitch * weightU);
            if (weightV > 0.1) ctx.StampConductance(vbat, v, gSwitch * weightV);
            if (weightW > 0.1) ctx.StampConductance(vbat, w, gSwitch * weightW);

            // Conductance from low phases to GND
            double lowU = Math.Max(0.0, -Math.Sin(ca));
            double lowV = Math.Max(0.0, -Math.Sin(ca - 2.0 * Math.PI / 3.0));
            double lowW = Math.Max(0.0, -Math.Sin(ca + 2.0 * Math.PI / 3.0));

            if (lowU > 0.1) ctx.StampConductance(u, gnd, gSwitch * lowU);
            if (lowV > 0.1) ctx.StampConductance(v, gnd, gSwitch * lowV);
            if (lowW > 0.1) ctx.StampConductance(w, gnd, gSwitch * lowW);
        }
        else
        {
            // Low-impedance dynamic braking or high-Z freewheeling
            ctx.StampConductance(u, gnd, 1e-4);
            ctx.StampConductance(v, gnd, 1e-4);
            ctx.StampConductance(w, gnd, 1e-4);
        }

        // PWM input pin high impedance
        if (Pins.Count > 2)
        {
            ctx.StampConductance(Pins[2].NodeId, gnd, 1e-5); // 100 kΩ input pull-down
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vSupply = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vGnd = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vSupply - vGnd;

        // Determine effective throttle from PWM pin if connected
        double vPwm = ctx.GetNodeVoltage(Pins[2].NodeId) - vGnd;
        if (vPwm > 0.4)
        {
            // Linear throttle map from 0.5V to 3.3V
            EffectiveThrottle = Math.Clamp((vPwm - 0.5) / (3.3 - 0.5), 0.0, 1.0);
        }
        else
        {
            EffectiveThrottle = Math.Clamp(ThrottlePercent, 0.0, 1.0);
        }

        // Advance electrical commutation angle based on throttle and time
        double dt = ctx.Dt;
        if (dt > 0 && EffectiveThrottle > 0.01)
        {
            double commutationFreqRadS = 2.0 * Math.PI * (100.0 + EffectiveThrottle * 800.0);
            _commutationAngle = (_commutationAngle + commutationFreqRadS * dt) % (2.0 * Math.PI);
        }

        // Estimate total battery supply current
        double iPhaseU = Math.Abs(ctx.GetNodeVoltage(Pins[3].NodeId) - vGnd) / (MosfetRdsOn * 4.0 + 0.1);
        double iPhaseV = Math.Abs(ctx.GetNodeVoltage(Pins[4].NodeId) - vGnd) / (MosfetRdsOn * 4.0 + 0.1);
        double iPhaseW = Math.Abs(ctx.GetNodeVoltage(Pins[5].NodeId) - vGnd) / (MosfetRdsOn * 4.0 + 0.1);
        Current = QuiescentCurrent + (iPhaseU + iPhaseV + iPhaseW) * EffectiveThrottle * 0.33;
    }
}
