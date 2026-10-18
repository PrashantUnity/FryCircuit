using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Professional EDA Net Label / Global Power Rail / Test Point marker.
/// Electrically connects distant schematic pins sharing the same NetName without routing physical wires across the canvas.
/// Commonly used for power rails (+3V3, +5V, +12V, GND), clock lines (CLK), and digital/RF bus nets.
/// </summary>
public class NetLabelDevice : Device, IDeviceSimulation
{
    /// <summary>Net label name (e.g. "+5V", "GND", "RF_IN", "GATE_U", "QUBIT_READOUT")</summary>
    public string NetName { get; set; } = "NET_1";

    public NetLabelDevice(int id = 0, double x = 0, double y = 0, string netName = "NET_1")
        : base(id, "netlabel", x, y)
    {
        NetName = netName;
        Pins = new List<Pin>
        {
            new(0, 0, 0, 0) // Single connection terminal
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // Net labels establish connectivity; pure virtual interconnect
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        // Connectivity is maintained via nodal aliasing in circuit matrix solver
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count > 0)
        {
            VoltageDrop = ctx.GetNodeVoltage(Pins[0].NodeId);
        }
        else
        {
            VoltageDrop = 0.0;
        }
        Current = 0.0;
    }
}
