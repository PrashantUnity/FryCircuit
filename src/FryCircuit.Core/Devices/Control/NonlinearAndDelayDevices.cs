using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Saturation Limiter Block:
/// y = clamp(u, Min, Max)
/// </summary>
public class SaturationDevice : Device, IDeviceSimulation
{
    public double MinValue
    {
        get => GetParameter("min", -5.0);
        set => SetParameter("min", value);
    }

    public double MaxValue
    {
        get => GetParameter("max", 5.0);
        set => SetParameter("max", value);
    }

    public double OutputVoltage { get; set; }

    public SaturationDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "saturation", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input
            new(1, 0, 1, 0),  // Output
            new(2, 0, 0, 1)   // Common Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double rOut = 1.0;
        ctx.StampConductance(outNode, refNode, 1.0 / rOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = vIn - vRef;

        double y = Math.Clamp(u, MinValue, MaxValue);
        OutputVoltage = y;

        double rOut = 1.0;
        ctx.StampCurrentSource(refNode, outNode, y / rOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class SaturationDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "saturation";
    public override string DisplayName => "Saturation Limiter";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Max", "Upper Limit (V)", "V", 0.0, 50.0, 5.0, 0.5,
        d => d is SaturationDevice sat ? sat.MaxValue : 5.0,
        (d, v) => { if (d is SaturationDevice sat) sat.MaxValue = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("Min", "Lower Limit (V)", "V", -50.0, 0.0, -5.0, 0.5,
            d => d is SaturationDevice sat ? sat.MinValue : -5.0,
            (d, v) => { if (d is SaturationDevice sat) sat.MinValue = v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new SaturationDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is SaturationDevice sat)
        {
            return $"[{sat.MinValue:G2}, {sat.MaxValue:G2}]";
        }
        return "Sat";
    }
}

/// <summary>
/// Transport Delay Block:
/// y(t) = u(t - DelaySeconds)
/// Implemented using a circular FIFO buffer.
/// </summary>
public class TimeDelayDevice : Device, IDeviceSimulation
{
    public double DelaySeconds
    {
        get => GetParameter("delay", 0.02);
        set => SetParameter("delay", Math.Max(1e-5, value));
    }

    private readonly Queue<(double Time, double Value)> _buffer = new();
    public double OutputVoltage { get; set; }

    public TimeDelayDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "delay", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input
            new(1, 0, 1, 0),  // Output
            new(2, 0, 0, 1)   // Common Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double rOut = 1.0;
        ctx.StampConductance(outNode, refNode, 1.0 / rOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = vIn - vRef;

        double t = ctx.CurrentTime;
        _buffer.Enqueue((t, u));

        double targetTime = t - DelaySeconds;
        double y = u;

        while (_buffer.Count > 1 && _buffer.Peek().Time < targetTime)
        {
            y = _buffer.Dequeue().Value;
        }

        if (_buffer.Count > 0)
        {
            y = _buffer.Peek().Value;
        }

        OutputVoltage = y;

        double rOut = 1.0;
        ctx.StampCurrentSource(refNode, outNode, y / rOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class TimeDelayDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "delay";
    public override string DisplayName => "Transport Delay";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Delay", "Delay (s)", "s", 0.001, 10.0, 0.02, 0.005,
        d => d is TimeDelayDevice delay ? delay.DelaySeconds : 0.02,
        (d, v) => { if (d is TimeDelayDevice delay) delay.DelaySeconds = Math.Max(1e-5, v); }
    );

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new TimeDelayDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is TimeDelayDevice d)
        {
            return $"Delay ({d.DelaySeconds * 1000:G2}ms)";
        }
        return "Delay";
    }
}
