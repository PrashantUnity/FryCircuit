using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class FuseDevice : Device, IDeviceSimulation
{
    /// <summary>
    /// Rated continuous current in Amperes. If current exceeds this rating, the internal fuse link melts open.
    /// </summary>
    public double CurrentRating
    {
        get => GetParameter("rating", 1.0); // 1.0 A default
        set => SetParameter("rating", Math.Max(0.01, value));
    }

    /// <summary>
    /// Internal resistance in Ohms when intact (cold resistance).
    /// </summary>
    public double InternalResistance
    {
        get => GetParameter("resistance", 0.05); // 50 mΩ default
        set => SetParameter("resistance", Math.Max(1e-4, value));
    }

    public FuseDevice(int id = 0, double x = 0, double y = 0, double currentRating = 1.0)
        : base(id, "fuse", x, y)
    {
        CurrentRating = currentRating;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            if (IsBlown)
            {
                ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1e-12);
            }
            else
            {
                ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1.0 / InternalResistance);
            }
        }
    }

    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;

            if (IsBlown)
            {
                Current = 0.0;
                return;
            }

            Current = VoltageDrop / InternalResistance;

            // Check if current exceeds the blow rating
            if (Math.Abs(Current) > CurrentRating)
            {
                IsBlown = true;
                BlownReason = $"Blown: {FormatEngineering(Math.Abs(Current), "A")} exceeded {FormatEngineering(CurrentRating, "A")} rating";
                Current = 0.0;
            }
        }
    }
}
