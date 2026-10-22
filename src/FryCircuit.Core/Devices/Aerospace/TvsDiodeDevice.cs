using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// High-Energy Transient Voltage Suppressor (TVS) Avalanche Diode.
/// Protects drone Flight Controllers, ESCs, and sensitive avionics from high-voltage
/// inductive motor back-EMF spikes (active braking / damped light flyback) and ESD strikes.
/// </summary>
public class TvsDiodeDevice : Device, IDeviceSimulation
{
    /// <summary>Reverse standoff / breakdown voltage V_BR in Volts (e.g. 18V for 4S LiPo, 28V for 6S)</summary>
    public double BreakdownVoltage
    {
        get => GetParameter("breakdown", 18.0);
        set => SetParameter("breakdown", Math.Max(1.0, value));
    }

    /// <summary>Ultra-low dynamic clamping resistance in Ohms during avalanche conduction</summary>
    public double ClampingResistance
    {
        get => GetParameter("r_clamp", 0.08);
        set => SetParameter("r_clamp", Math.Max(0.001, value));
    }

    /// <summary>Forward conduction series resistance in Ohms</summary>
    public double ForwardResistance { get; set; } = 0.05;

    /// <summary>Peak pulse power dissipation rating in Watts (e.g. 1500W SMCJ package)</summary>
    public double PeakPowerWatts
    {
        get => GetParameter("peak_power", 1500.0);
        set => SetParameter("peak_power", Math.Max(100.0, value));
    }

    /// <summary>Indicates whether the TVS diode is currently in active avalanche clamping mode</summary>
    public bool IsClamping => Math.Abs(VoltageDrop) >= BreakdownVoltage;

    public TvsDiodeDevice(int id = 0, double x = 0, double y = 0, double breakdown = 18.0)
        : base(id, "tvs", x, y)
    {
        BreakdownVoltage = breakdown;
        ClampingResistance = 0.08;
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Anode (Left)
            new(1, 0, 1, 0)   // Cathode (Right)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // Off-state standby leakage conductance (10 MΩ)
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1e-7);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
    }

    public void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        if (vd < -BreakdownVoltage)
        {
            // Reverse avalanche breakdown clamping
            double gClamp = 1.0 / Math.Max(0.001, ClampingResistance);
            double id = -gClamp * (-vd - BreakdownVoltage);
            double ieq = id - gClamp * vd;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gClamp);
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, ieq);
        }
        else if (vd > 0.7)
        {
            // Forward diode conduction
            double gFwd = 1.0 / Math.Max(0.001, ForwardResistance);
            double id = gFwd * (vd - 0.7);
            double ieq = id - gFwd * vd;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gFwd);
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, -ieq);
        }
    }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vd;
        if (vd < -BreakdownVoltage)
        {
            double gClamp = 1.0 / Math.Max(0.001, ClampingResistance);
            Current = -gClamp * (-vd - BreakdownVoltage);
        }
        else if (vd > 0.7)
        {
            double gFwd = 1.0 / Math.Max(0.001, ForwardResistance);
            Current = gFwd * (vd - 0.7);
        }
        else
        {
            Current = vd * 1e-7;
        }
    }
}
