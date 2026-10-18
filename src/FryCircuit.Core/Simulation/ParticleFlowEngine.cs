using FryCircuit.Core.Models;

namespace FryCircuit.Core.Simulation;

public class CircuitParticle
{
    public double Progress { get; set; } // 0.0 to 1.0 along the branch
}

public class ParticleBranch
{
    public Device? Device { get; set; }
    public Func<double>? CurrentProvider { get; set; }
    public IReadOnlyList<(double X, double Y)> PathPoints { get; set; }
    public List<CircuitParticle> Particles { get; set; } = new();

    private readonly double _totalLength;
    private readonly List<double> _segmentLengths = new();

    public ParticleBranch(Device? device, IReadOnlyList<(double X, double Y)> pathPoints, int particleCount = 3, Func<double>? currentProvider = null)
    {
        Device = device;
        CurrentProvider = currentProvider;
        PathPoints = pathPoints.Count >= 2 ? pathPoints : new List<(double X, double Y)> { (0, 0), (0, 0) };

        double sum = 0.0;
        for (int i = 0; i < PathPoints.Count - 1; i++)
        {
            double dx = PathPoints[i + 1].X - PathPoints[i].X;
            double dy = PathPoints[i + 1].Y - PathPoints[i].Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            _segmentLengths.Add(len);
            sum += len;
        }
        _totalLength = Math.Max(0.001, sum);

        for (int i = 0; i < particleCount; i++)
        {
            Particles.Add(new CircuitParticle { Progress = (double)i / particleCount });
        }
    }

    public void Update(double dt, double speedMultiplier = 1.0)
    {
        double current = CurrentProvider != null ? CurrentProvider() : (Device?.Current ?? 0.0);
        if (Math.Abs(current) < 1e-9) return;

        // Speed is non-linearly scaled for aesthetic particle movement
        double velocity = Math.Sign(current) * Math.Min(3.0, 0.45 * Math.Sqrt(Math.Abs(current) * 1000.0) * speedMultiplier);

        foreach (var p in Particles)
        {
            p.Progress += velocity * dt;
            if (p.Progress > 1.0) p.Progress -= 1.0;
            if (p.Progress < 0.0) p.Progress += 1.0;
        }
    }

    public (double X, double Y) GetParticlePosition(CircuitParticle p)
    {
        if (PathPoints.Count == 0) return (0, 0);
        if (PathPoints.Count == 1) return PathPoints[0];

        double targetDist = p.Progress * _totalLength;
        double accumulated = 0.0;

        for (int i = 0; i < _segmentLengths.Count; i++)
        {
            double segLen = _segmentLengths[i];
            if (accumulated + segLen >= targetDist || i == _segmentLengths.Count - 1)
            {
                double segT = segLen > 1e-9 ? Math.Clamp((targetDist - accumulated) / segLen, 0.0, 1.0) : 0.0;
                var pt1 = PathPoints[i];
                var pt2 = PathPoints[i + 1];
                return (pt1.X + (pt2.X - pt1.X) * segT, pt1.Y + (pt2.Y - pt1.Y) * segT);
            }
            accumulated += segLen;
        }

        return PathPoints[^1];
    }
}

public class ParticleFlowEngine
{
    private readonly List<ParticleBranch> _branches = new();

    public IReadOnlyList<ParticleBranch> Branches => _branches;

    public void SyncWithCircuit(Circuit circuit)
    {
        _branches.Clear();

        // Wire segments flow along intelligent orthogonal routed polyline
        circuit.EnsureWiresSynthesized();
        foreach (var wire in circuit.Wires)
        {
            var dev1 = circuit.Devices.FirstOrDefault(d => d.Id == wire.FromDeviceId);
            var dev2 = circuit.Devices.FirstOrDefault(d => d.Id == wire.ToDeviceId);
            if (dev1 == null || dev2 == null) continue;

            if (wire.PathPoints == null || wire.PathPoints.Count < 2)
            {
                wire.PathPoints = SchematicWireRouter.RouteWire(circuit, dev1, wire.FromPinIndex, dev2, wire.ToPinIndex);
            }

            Func<double> currentFunc = () =>
            {
                double exit1 = GetPinExitCurrent(dev1, wire.FromPinIndex);
                double exit2 = GetPinExitCurrent(dev2, wire.ToPinIndex);

                if (Math.Abs(exit1) > 1e-9 && Math.Abs(exit2) > 1e-9)
                {
                    return (exit1 - exit2) / 2.0;
                }
                if (Math.Abs(exit1) > 1e-9) return exit1;
                if (Math.Abs(exit2) > 1e-9) return -exit2;

                return 0.0;
            };

            int pCount = Math.Max(2, wire.PathPoints.Count);
            _branches.Add(new ParticleBranch(null, wire.PathPoints, particleCount: pCount, currentProvider: currentFunc));
        }
    }

    /// <summary>
    /// Computes conventional current (in Amperes) exiting a specific pin into an attached conductor.
    /// Positive value means current flows OUT of the pin into the wire.
    /// Negative value means current flows INTO the pin from the wire.
    /// </summary>
    public static double GetPinExitCurrent(Device dev, int pinIndex)
    {
        if (dev == null) return 0.0;

        switch (dev.Type)
        {
            case "vsource":
            case "battery":
                // In MNA, voltage source auxiliary current enters pin 0 and exits pin 1 internally.
                // Current exiting Pin 0 (Positive terminal) is -dev.Current (positive when supplying power).
                // Current exiting Pin 1 (Negative terminal) is +dev.Current (negative, i.e. return current enters).
                return (pinIndex == 0) ? -dev.Current : dev.Current;

            case "csource":
                return (pinIndex == 0) ? dev.Current : -dev.Current;

            case "npn":
                if (pinIndex == 2) return Math.Abs(dev.Current);            // Emitter exits
                if (pinIndex == 1) return -Math.Abs(dev.Current) * 0.98;    // Collector enters
                if (pinIndex == 0) return -Math.Abs(dev.Current) * 0.02;    // Base enters
                return 0.0;

            case "pnp":
                if (pinIndex == 2) return -Math.Abs(dev.Current);           // Emitter enters
                if (pinIndex == 1) return Math.Abs(dev.Current) * 0.98;     // Collector exits
                if (pinIndex == 0) return Math.Abs(dev.Current) * 0.02;     // Base exits
                return 0.0;

            case "nmos":
                if (pinIndex == 2) return Math.Abs(dev.Current);            // Source exits
                if (pinIndex == 1) return -Math.Abs(dev.Current);           // Drain enters
                return 0.0;                                                 // Gate isolated

            case "pmos":
                if (pinIndex == 2) return -Math.Abs(dev.Current);           // Source enters
                if (pinIndex == 1) return Math.Abs(dev.Current);            // Drain exits
                return 0.0;                                                 // Gate isolated

            case "ground":
                return 0.0;

            default:
                // Standard 2-terminal passive or semiconductor device:
                // Current enters Pin 0 and exits Pin 1.
                // Current exiting Pin 0 into wire: -dev.Current
                // Current exiting Pin 1 into wire: +dev.Current
                if (dev.Pins.Count >= 2)
                {
                    return (pinIndex == 1) ? dev.Current : -dev.Current;
                }
                return dev.Current;
        }
    }

    public void Update(double dt, double speedMultiplier = 1.0)
    {
        foreach (var branch in _branches)
        {
            branch.Update(dt, speedMultiplier);
        }
    }
}
