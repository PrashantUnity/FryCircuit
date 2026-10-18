using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

/// <summary>
/// High-Speed Distributed Transmission Line for Aerospace, Radar, and RF Drone telemetry.
/// Uses Bergeron's Method for wave propagation and reflection modeling:
///   Z_0: Characteristic impedance (e.g. 50 Ohm microstrip / coax)
///   T_d: Delay time (e.g. 1.0 ns propagation time)
/// </summary>
public class TransmissionLineDevice : Device, IDeviceSimulation
{
    public double CharacteristicImpedance { get; set; } = 50.0; // 50 Ω
    public double DelaySeconds { get; set; } = 1.0e-9; // 1.0 ns delay

    private readonly Queue<(double Time, double V1, double I1, double V2, double I2)> _history = new();
    private double _prevI1 = 0.0;
    private double _prevI2 = 0.0;

    public TransmissionLineDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "tline", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, -1.0), // Port 1 (+) In
            new(1, 0, -2, 1.0),  // Port 1 (-) Ref
            new(2, 0, 2, -1.0),  // Port 2 (+) Out
            new(3, 0, 2, 1.0)   // Port 2 (-) Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        double g0 = 1.0 / Math.Max(0.1, CharacteristicImpedance);

        // Port 1 admittance stamp
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, g0);

        // Port 2 admittance stamp
        ctx.StampConductance(Pins[2].NodeId, Pins[3].NodeId, g0);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        double targetTime = ctx.CurrentTime - DelaySeconds;
        double z0 = Math.Max(0.1, CharacteristicImpedance);

        // Find past state at (t - T_d)
        var past = GetPastState(targetTime);

        // Bergeron equivalent current source at Port 1:
        // I_eq1 = - (V2(t - Td) - Z0 * I2(t - Td)) / Z0
        double ieq1 = -(past.V2 - z0 * past.I2) / z0;

        // Bergeron equivalent current source at Port 2:
        // I_eq2 = - (V1(t - Td) + Z0 * I1(t - Td)) / Z0
        double ieq2 = -(past.V1 + z0 * past.I1) / z0;

        ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, ieq1);
        ctx.StampCurrentSource(Pins[2].NodeId, Pins[3].NodeId, ieq2);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        double v1 = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        double v2 = ctx.GetNodeVoltage(Pins[2].NodeId) - ctx.GetNodeVoltage(Pins[3].NodeId);
        double z0 = Math.Max(0.1, CharacteristicImpedance);

        var past = GetPastState(ctx.CurrentTime - DelaySeconds);

        // Compute port currents from Bergeron relations
        double i1 = (v1 / z0) - (past.V2 - z0 * past.I2) / z0;
        double i2 = (v2 / z0) - (past.V1 + z0 * past.I1) / z0;

        _prevI1 = i1;
        _prevI2 = i2;

        VoltageDrop = v1;
        Current = Math.Abs(i1);

        // Record history
        _history.Enqueue((ctx.CurrentTime, v1, i1, v2, i2));

        // Purge old states older than 2x delay
        while (_history.Count > 1000 || (_history.Count > 2 && _history.Peek().Time < ctx.CurrentTime - 3.0 * DelaySeconds))
        {
            _history.Dequeue();
        }
    }

    private (double V1, double I1, double V2, double I2) GetPastState(double targetTime)
    {
        if (_history.Count == 0 || targetTime <= 0)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        // Interpolate nearest state in history
        (double Time, double V1, double I1, double V2, double I2) prev = _history.Peek();
        foreach (var entry in _history)
        {
            if (entry.Time >= targetTime)
            {
                double dt = entry.Time - prev.Time;
                if (dt <= 1e-15) return (entry.V1, entry.I1, entry.V2, entry.I2);
                double alpha = (targetTime - prev.Time) / dt;
                return (
                    prev.V1 + alpha * (entry.V1 - prev.V1),
                    prev.I1 + alpha * (entry.I1 - prev.I1),
                    prev.V2 + alpha * (entry.V2 - prev.V2),
                    prev.I2 + alpha * (entry.I2 - prev.I2)
                );
            }
            prev = entry;
        }

        return (prev.V1, prev.I1, prev.V2, prev.I2);
    }
}
