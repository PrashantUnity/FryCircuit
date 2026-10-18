using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class VoltmeterDevice : Device, IDeviceSimulation
{
    public VoltmeterDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "voltmeter", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // 10 MOhm input impedance
        if (Pins.Count >= 2)
        {
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1e-7);
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
            Current = VoltageDrop * 1e-7;
        }
    }
}

public class AmmeterDevice : Device, IDeviceSimulation
{
    public AmmeterDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "amperemeter", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // Low resistance shunt (0.001 Ohm)
        if (Pins.Count >= 2)
        {
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1000.0);
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
            Current = VoltageDrop * 1000.0;
        }
    }
}

public class OhmmeterDevice : Device, IDeviceSimulation
{
    public double MeasuredResistance { get; private set; }

    public OhmmeterDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "ohmmeter", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // 100 GigaOhm ultra-high impedance protection
        if (Pins.Count >= 2)
        {
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1e-11);
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        // Inject 1.0 mA test current between Pin 0 and Pin 1
        if (Pins.Count >= 2)
        {
            ctx.StampCurrentSource(Pins[1].NodeId, Pins[0].NodeId, 0.001);
        }
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;
            Current = 0.001;
            MeasuredResistance = Math.Abs(VoltageDrop) / 0.001;
        }
    }
}

