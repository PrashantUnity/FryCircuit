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

    private static readonly double[,] EmptyMatrix = new double[0, 0];
    private static readonly double[] EmptyVector = Array.Empty<double>();

    private double[,] _cachedG = new double[0, 0];
    private double[] _cachedB = Array.Empty<double>();
    private double[] _cachedSolution = Array.Empty<double>();
    private double[] _cachedPrevSolution = Array.Empty<double>();
    private double[,] _cachedSolveM = new double[0, 0];
    private double[] _cachedSolveRhs = Array.Empty<double>();

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
                cap.StateCurrent = 0.0;
                cap.EquivCurrent = 0.0;
            }
            else if (dev is InductorDevice ind)
            {
                ind.StateVoltage = 0.0;
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

        // Net labels establish connectivity across separate wires sharing the same NetName
        ApplyNetLabelAliasing();

        // Ensure there is at least one ground node, unifying all ground components
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

        // Ensure cached matrix and vector buffers match the active matrix dimensions
        if (_cachedG.GetLength(0) != matrixSize)
        {
            _cachedG = new double[matrixSize, matrixSize];
            _cachedB = new double[matrixSize];
            _cachedSolution = new double[matrixSize];
            _cachedPrevSolution = new double[matrixSize];
            _cachedSolveM = new double[matrixSize, matrixSize];
            _cachedSolveRhs = new double[matrixSize];
        }

        Array.Clear(_cachedSolution, 0, matrixSize);
        Array.Clear(_cachedPrevSolution, 0, matrixSize);

        // High-precision Newton-Raphson iteration with convergence tolerance
        const int maxIterations = 24;
        const double voltageTolerance = 1e-6; // 1 µV

        for (int iter = 0; iter < maxIterations; iter++)
        {
            Array.Clear(_cachedG, 0, matrixSize * matrixSize);
            Array.Clear(_cachedB, 0, matrixSize);

            var ctx = new MnaContext(
                _cachedG,
                _cachedB,
                nodeIndexMap,
                dt,
                _currentTime,
                _cachedSolution,
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
                    _cachedG[auxRow, pIdx] += 1.0;
                    _cachedG[pIdx, auxRow] += 1.0;
                }

                if (nodeIndexMap.TryGetValue(negNode, out int nIdx))
                {
                    _cachedG[auxRow, nIdx] -= 1.0;
                    _cachedG[nIdx, auxRow] -= 1.0;
                }

                _cachedB[auxRow] = vInstant;
            }

            // Ensure diagonal stability (Gmin shunt for floating nodes: 1e-10 S)
            for (int i = 0; i < numNodes; i++)
            {
                _cachedG[i, i] += 1e-10;
            }

            // 3. Solve Linear System G * x = b without per-iteration heap allocations
            if (!SolveLinearSystem(_cachedG, _cachedB, _cachedSolution, _cachedSolveM, _cachedSolveRhs, matrixSize))
            {
                break;
            }

            // Check convergence across all nodes
            double maxDeltaV = 0.0;
            for (int k = 0; k < numNodes; k++)
            {
                double delta = Math.Abs(_cachedSolution[k] - _cachedPrevSolution[k]);
                if (delta > maxDeltaV) maxDeltaV = delta;
            }

            Array.Copy(_cachedSolution, _cachedPrevSolution, matrixSize);

            // Update intermediate voltages for Newton-Raphson device evaluations
            foreach (var node in nonGroundNodes)
            {
                if (nodeIndexMap.TryGetValue(node.Id, out int idx))
                {
                    node.Voltage = _cachedSolution[idx];
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
                node.Voltage = _cachedSolution[idx];
            }
        }

        // 5. Update Device Dynamic States and Currents
        var updateCtx = new MnaContext(
            EmptyMatrix,
            EmptyVector,
            nodeIndexMap,
            dt,
            _currentTime,
            _cachedSolution,
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
        // 1. Explicit Ground components placed on schematic (Highest priority)
        var groundDevices = _circuit.Devices.OfType<GroundDevice>().ToList();
        if (groundDevices.Count > 0)
        {
            int canonicalGndId = 0;
            var existingGnd = _circuit.FindGroundNode();
            if (existingGnd != null)
            {
                canonicalGndId = existingGnd.Id;
            }
            else
            {
                var firstPin = groundDevices[0].Pins.FirstOrDefault();
                if (firstPin != null && firstPin.NodeId >= 0)
                {
                    canonicalGndId = firstPin.NodeId;
                }
            }

            var gndNode = _circuit.GetOrCreateNode(canonicalGndId, isGround: true);
            gndNode.IsGround = true;

            // Merge all ground devices into canonical ground node
            foreach (var gd in groundDevices)
            {
                if (gd.Pins.Count > 0)
                {
                    var pin = gd.Pins[0];
                    if (pin.NodeId < 0 || !_circuit.Nodes.ContainsKey(pin.NodeId))
                    {
                        pin.NodeId = canonicalGndId;
                    }
                    else if (pin.NodeId != canonicalGndId)
                    {
                        canonicalGndId = _circuit.MergeNodes(canonicalGndId, pin.NodeId);
                    }
                }
            }

            // Ensure canonical node is ground and clear any other node's IsGround flag
            foreach (var node in _circuit.Nodes.Values)
            {
                if (node.Id != canonicalGndId && node.IsGround)
                {
                    node.IsGround = false;
                }
            }
            _circuit.GetOrCreateNode(canonicalGndId, isGround: true);
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

    private void ApplyNetLabelAliasing()
    {
        var netLabels = _circuit.Devices.OfType<NetLabelDevice>().ToList();
        if (netLabels.Count == 0) return;

        var groups = netLabels
            .Where(nl => !string.IsNullOrWhiteSpace(nl.NetName) && nl.Pins.Count > 0)
            .GroupBy(nl => nl.NetName.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            bool isGroundNet = string.Equals(group.Key, "GND", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(group.Key, "GROUND", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(group.Key, "0", StringComparison.OrdinalIgnoreCase);

            if (isGroundNet)
            {
                var gndNode = _circuit.FindGroundNode();
                int gndId = gndNode?.Id ?? 0;
                _circuit.GetOrCreateNode(gndId, isGround: true);

                foreach (var nl in group)
                {
                    var pin = nl.Pins[0];
                    if (pin.NodeId < 0 || !_circuit.Nodes.ContainsKey(pin.NodeId))
                    {
                        pin.NodeId = gndId;
                    }
                    else if (pin.NodeId != gndId)
                    {
                        gndId = _circuit.MergeNodes(gndId, pin.NodeId);
                    }
                }
            }
            else
            {
                int canonicalNodeId = -1;
                foreach (var nl in group)
                {
                    var pin = nl.Pins[0];
                    if (pin.NodeId >= 0 && _circuit.Nodes.ContainsKey(pin.NodeId))
                    {
                        canonicalNodeId = pin.NodeId;
                        break;
                    }
                }

                if (canonicalNodeId < 0)
                {
                    canonicalNodeId = (_circuit.Nodes.Count > 0 ? _circuit.Nodes.Keys.Max() : 0) + 1;
                    _circuit.GetOrCreateNode(canonicalNodeId);
                }

                foreach (var nl in group)
                {
                    var pin = nl.Pins[0];
                    if (pin.NodeId < 0 || !_circuit.Nodes.ContainsKey(pin.NodeId))
                    {
                        pin.NodeId = canonicalNodeId;
                    }
                    else if (pin.NodeId != canonicalNodeId)
                    {
                        canonicalNodeId = _circuit.MergeNodes(canonicalNodeId, pin.NodeId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Solves A * x = b using Gaussian elimination with partial pivoting into reusable scratch buffers.
    /// </summary>
    private static bool SolveLinearSystem(double[,] a, double[] b, double[] x, double[,] m, double[] rhs, int n)
    {
        Array.Copy(a, m, n * n);
        Array.Copy(b, rhs, n);

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
