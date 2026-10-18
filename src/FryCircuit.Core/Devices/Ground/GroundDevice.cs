using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class GroundDevice : Device, IDeviceSimulation
{
    public GroundDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "ground", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, 0)
        };
    }

    public void StampConductance(MnaContext ctx) { }
    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }
    public void UpdateState(MnaContext ctx)
    {
        VoltageDrop = 0.0;
        Current = 0.0;
    }
}
