using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Simulation;

public class MnaSolver
{
    private readonly Circuit _circuit;
    private double _currentTime;

    public double CurrentTime => _currentTime;

    /// <summary>Operating ambient temperature in Kelvin (defaults to 300.15K ~ 27°C)</summary>
    public double TemperatureKelvin { get; set; } = PhysicalConstants.TempRoomStandard;

    /// <summary>Numerical integration method for dynamic elements (Trapezoidal / Gear2)</summary>
    public IntegrationMethod IntegrationMethod { get; set; } = IntegrationMethod.Trapezoidal;

    public MnaSolver(Circuit circuit)
    {
        _circuit = circuit;
        _currentTime = 0.0;
    }

    public void Reset()
    {
        _currentTime = 0.0;
        _circuit.ResetSimulationState();
        foreach (var dev in _circuit.Devices)
        {
            if (dev is CapacitorDevice cap)
            {
                cap.StateVoltage = 0.0;
                cap.EquivCurrent = 0.0;
            }
            else if (dev is InductorDevice ind)
            {
                ind.StateCurrent = 0.0;
                ind.EquivCurrent = 0.0;
            }
            else if (dev is DiodeDevice diode)
            {
                diode.StateVoltage = 0.0;
                diode.EquivCurrent = 0.0;
            }
        }
    }

    /// <summary>
    /// Executes one simulation step of duration dt (seconds).
    /// </summary>
    public void Step(double dt)
    {
        if (dt <= 0) return;

        // Ensure there is at least one ground node
        EnsureGroundNode();

        // Map non-ground nodes to matrix indices [0..numNodes-1]
        var nonGroundNodes = _circuit.Nodes.Values
            .Where(n => !n.IsGround)
            .OrderBy(n => n.Id)
            .ToList();

        var nodeIndexMap = new Dictionary<int, int>();
        for (int i = 0; i < nonGroundNodes.Count; i++)
        {
            nodeIndexMap[nonGroundNodes[i].Id] = i;
        }

        // Count auxiliary voltage source equations
        var voltageSources = _circuit.Devices
            .OfType<VoltageSourceDevice>()
            .ToList();

        int numNodes = nonGroundNodes.Count;
        int numVs = voltageSources.Count;
        int matrixSize = numNodes + numVs;

        if (matrixSize == 0)
        {
            _currentTime += dt;
            return;
        }

        // High-precision Newton-Raphson iteration with convergence tolerance
        const int maxIterations = 24;
        const double voltageTolerance = 1e-6; // 1 µV
        double[] solution = new double[matrixSize];
        double[] prevSolution = new double[matrixSize];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double[,] g = new double[matrixSize, matrixSize];
            double[] b = new double[matrixSize];

            var ctx = new MnaContext(
                g,
                b,
                nodeIndexMap,
                dt,
                _currentTime,
                solution,
                numNodes,
                voltageSources,
                GetNodeVoltage,
                TemperatureKelvin,
                iter,
                IntegrationMethod
            );

            // 1. Stamp Passive, Linear, and Non-Linear Devices via IDeviceSimulation
            foreach (var dev in _circuit.Devices)
            {
                if (dev is IDeviceSimulation sim)
                {
                    sim.StampConductance(ctx);
                    sim.StampCurrentSources(ctx);
                    sim.StampNonLinear(ctx);
                }
            }

            // 2. Stamp Independent Voltage Sources (Auxiliary rows)
            for (int vsIndex = 0; vsIndex < voltageSources.Count; vsIndex++)
            {
                var vs = voltageSources[vsIndex];
                int auxRow = numNodes + vsIndex;
                int posNode = vs.Pins[0].NodeId;
                int negNode = vs.Pins[1].NodeId;

                double vInstant = vs.GetInstantaneousVoltage(_currentTime);

                if (nodeIndexMap.TryGetValue(posNode, out int pIdx))
                {
                    g[auxRow, pIdx] += 1.0;
                    g[pIdx, auxRow] += 1.0;
                }

                if (nodeIndexMap.TryGetValue(negNode, out int nIdx))
                {
                    g[auxRow, nIdx] -= 1.0;
                    g[nIdx, auxRow] -= 1.0;
                }

                b[auxRow] = vInstant;
            }

            // Ensure diagonal stability (Gmin shunt for floating nodes: 1e-10 S)
            for (int i = 0; i < numNodes; i++)
            {
                g[i, i] += 1e-10;
            }

            // 3. Solve Linear System G * x = b
            if (!SolveLinearSystem(g, b, solution))
            {
                break;
            }

            // Check convergence across all nodes
            double maxDeltaV = 0.0;
            for (int k = 0; k < numNodes; k++)
            {
                double delta = Math.Abs(solution[k] - prevSolution[k]);
                if (delta > maxDeltaV) maxDeltaV = delta;
            }

            Array.Copy(solution, prevSolution, matrixSize);

            // Update intermediate voltages for Newton-Raphson device evaluations
            foreach (var node in nonGroundNodes)
            {
                if (nodeIndexMap.TryGetValue(node.Id, out int idx))
                {
                    node.Voltage = solution[idx];
                }
            }

            if (iter > 0 && maxDeltaV < voltageTolerance)
            {
                // Convergence achieved!
                break;
            }
        }

        // 4. Update Final Node Voltages
        foreach (var node in _circuit.Nodes.Values)
        {
            if (node.IsGround)
            {
                node.Voltage = 0.0;
            }
            else if (nodeIndexMap.TryGetValue(node.Id, out int idx))
            {
                node.Voltage = solution[idx];
            }
        }

        // 5. Update Device Dynamic States and Currents
        var updateCtx = new MnaContext(
            new double[0, 0],
            new double[0],
            nodeIndexMap,
            dt,
            _currentTime,
            solution,
            numNodes,
            voltageSources,
            GetNodeVoltage,
            TemperatureKelvin,
            0,
            IntegrationMethod
        );

        foreach (var dev in _circuit.Devices)
        {
            if (dev is IDeviceSimulation sim)
            {
                sim.UpdateState(updateCtx);
            }
            else if (dev.Pins.Count >= 2)
            {
                double v0 = GetNodeVoltage(dev.Pins[0].NodeId);
                double v1 = GetNodeVoltage(dev.Pins[1].NodeId);
                dev.VoltageDrop = v0 - v1;
            }
            else if (dev.Pins.Count == 1)
            {
                dev.VoltageDrop = GetNodeVoltage(dev.Pins[0].NodeId);
            }
        }

        _currentTime += dt;
    }

    public double GetNodeVoltage(int nodeId)
    {
        return _circuit.Nodes.TryGetValue(nodeId, out var node) ? node.Voltage : 0.0;
    }

    private void EnsureGroundNode()
    {
        // 1. Explicit Ground component placed on schematic (Highest priority)
        var groundDev = _circuit.Devices.OfType<GroundDevice>().FirstOrDefault();
        if (groundDev != null && groundDev.Pins.Count > 0)
        {
            int gNodeId = groundDev.Pins[0].NodeId;
            // Clear any previously assigned fallback ground flags
            foreach (var node in _circuit.Nodes.Values)
            {
                if (node.Id != gNodeId && node.IsGround) node.IsGround = false;
            }
            _circuit.GetOrCreateNode(gNodeId, isGround: true);
            return;
        }

        // If an explicit ground node exists from an earlier ground component, retain it
        var currentGnd = _circuit.FindGroundNode();
        if (currentGnd != null && _circuit.Devices.Any(d => d.Type == "ground" && d.Pins.Any(p => p.NodeId == currentGnd.Id)))
        {
            return;
        }

        // 2. Intelligent Reference Ground (when no Ground symbol is present):
        // Prioritize the negative terminal (Pin 1) of the primary DC Voltage Source or Battery
        var primarySource = _circuit.Devices.FirstOrDefault(d =>
            (d is VoltageSourceDevice || d.Type == "vsource" || d.Type == "battery") &&
            d.Pins.Count > 1 && d.Pins[1].NodeId >= 0 && _circuit.Nodes.ContainsKey(d.Pins[1].NodeId));

        int refNodeId = -1;
        if (primarySource != null)
        {
            refNodeId = primarySource.Pins[1].NodeId;
        }
        else if (_circuit.Nodes.Count > 0)
        {
            refNodeId = _circuit.Nodes.Keys.Min();
        }

        if (refNodeId >= 0 && _circuit.Nodes.ContainsKey(refNodeId))
        {
            foreach (var node in _circuit.Nodes.Values)
            {
                if (node.Id != refNodeId && node.IsGround) node.IsGround = false;
            }
            _circuit.GetOrCreateNode(refNodeId, isGround: true);
        }
    }

    /// <summary>
    /// Solves A * x = b using Gaussian elimination with partial pivoting.
    /// </summary>
    private static bool SolveLinearSystem(double[,] a, double[] b, double[] x)
    {
        int n = b.Length;
        double[,] m = (double[,])a.Clone();
        double[] rhs = (double[])b.Clone();

        for (int i = 0; i < n; i++)
        {
            // Find pivot
            int maxRow = i;
            double maxVal = Math.Abs(m[i, i]);
            for (int k = i + 1; k < n; k++)
            {
                double val = Math.Abs(m[k, i]);
                if (val > maxVal)
                {
                    maxVal = val;
                    maxRow = k;
                }
            }

            if (maxVal < 1e-15)
            {
                // Near-singular system
                continue;
            }

            // Swap rows
            if (maxRow != i)
            {
                for (int k = i; k < n; k++)
                {
                    (m[i, k], m[maxRow, k]) = (m[maxRow, k], m[i, k]);
                }
                (rhs[i], rhs[maxRow]) = (rhs[maxRow], rhs[i]);
            }

            // Eliminate column below pivot
            double pivot = m[i, i];
            for (int k = i + 1; k < n; k++)
            {
                double factor = m[k, i] / pivot;
                m[k, i] = 0.0;
                for (int j = i + 1; j < n; j++)
                {
                    m[k, j] -= factor * m[i, j];
                }
                rhs[k] -= factor * rhs[i];
            }
        }

        // Back substitution
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = rhs[i];
            for (int j = i + 1; j < n; j++)
            {
                sum -= m[i, j] * x[j];
            }
            double diag = m[i, i];
            x[i] = Math.Abs(diag) > 1e-15 ? (sum / diag) : 0.0;
        }

        return true;
    }
}
