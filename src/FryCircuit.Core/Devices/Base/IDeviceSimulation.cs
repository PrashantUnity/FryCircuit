using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

public enum IntegrationMethod
{
    Trapezoidal,
    Gear2
}

/// <summary>
/// Execution context provided to devices during MNA matrix stamping and simulation.
/// Supports quantum cryogenic temperatures, picosecond transient timesteps, and high-order integration.
/// </summary>
public class MnaContext
{
    public double[,] G { get; }
    public double[] B { get; }
    public IReadOnlyDictionary<int, int> NodeIndexMap { get; }
    public double Dt { get; }
    public double CurrentTime { get; }
    public double[]? Solution { get; }
    public int NumNodes { get; }
    public IReadOnlyList<VoltageSourceDevice> VoltageSources { get; }
    public Func<int, double> GetNodeVoltage { get; }
    public double TemperatureKelvin { get; }
    public int Iteration { get; }
    public IntegrationMethod Method { get; }

    public double ThermalVoltage => PhysicalConstants.ThermalVoltage(TemperatureKelvin);

    public MnaContext(
        double[,] g,
        double[] b,
        IReadOnlyDictionary<int, int> nodeIndexMap,
        double dt,
        double currentTime,
        double[]? solution,
        int numNodes,
        IReadOnlyList<VoltageSourceDevice> voltageSources,
        Func<int, double> getNodeVoltage,
        double temperatureKelvin = PhysicalConstants.TempRoomStandard,
        int iteration = 0,
        IntegrationMethod method = IntegrationMethod.Trapezoidal)
    {
        G = g;
        B = b;
        NodeIndexMap = nodeIndexMap;
        Dt = dt;
        CurrentTime = currentTime;
        Solution = solution;
        NumNodes = numNodes;
        VoltageSources = voltageSources;
        GetNodeVoltage = getNodeVoltage;
        TemperatureKelvin = temperatureKelvin;
        Iteration = iteration;
        Method = method;
    }

    public void StampConductance(int node1, int node2, double conductance)
    {
        if (double.IsNaN(conductance) || double.IsInfinity(conductance) || Math.Abs(conductance) < 1e-15)
            return;

        bool has1 = NodeIndexMap.TryGetValue(node1, out int i);
        bool has2 = NodeIndexMap.TryGetValue(node2, out int j);

        if (has1) G[i, i] += conductance;
        if (has2) G[j, j] += conductance;
        if (has1 && has2)
        {
            G[i, j] -= conductance;
            G[j, i] -= conductance;
        }
    }

    public void StampCurrentSource(int fromNode, int toNode, double current)
    {
        if (double.IsNaN(current) || double.IsInfinity(current) || Math.Abs(current) < 1e-15)
            return;

        if (NodeIndexMap.TryGetValue(fromNode, out int fromIdx))
            B[fromIdx] -= current;

        if (NodeIndexMap.TryGetValue(toNode, out int toIdx))
            B[toIdx] += current;
    }
}

/// <summary>
/// Interface for devices that participate directly in MNA circuit simulation.
/// </summary>
public interface IDeviceSimulation
{
    void StampConductance(MnaContext ctx);
    void StampCurrentSources(MnaContext ctx);
    void StampNonLinear(MnaContext ctx);
    void UpdateState(MnaContext ctx);
}
