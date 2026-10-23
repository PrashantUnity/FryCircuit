using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Proportional Gain Block:
/// y = K * u
/// </summary>
public class GainBlockDevice : Device, IDeviceSimulation
{
    public double Gain
    {
        get => GetParameter("gain", 1.0);
        set => SetParameter("gain", value);
    }

    public double OutputVoltage { get; set; }

    public GainBlockDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "gain", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input
            new(1, 0, 1, 0),  // Output
            new(2, 0, 0, 1)   // Ground Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = vIn - vRef;

        double y = Gain * u;
        OutputVoltage = y;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, y * gOut);
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

public class GainBlockDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "gain";
    public override string DisplayName => "Gain Block";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Gain", "Gain (K)", "", -1000.0, 1000.0, 1.0, 0.1,
        d => d is GainBlockDevice g ? g.Gain : 1.0,
        (d, v) => { if (d is GainBlockDevice g) g.Gain = v; }
    );

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new GainBlockDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        return (device is GainBlockDevice g) ? $"× {g.Gain:G3}" : "× K";
    }
}

/// <summary>
/// Step Signal Source:
/// y(t) = InitialValue for t &lt; StepTime, and FinalValue for t &gt;= StepTime.
/// </summary>
public class StepSourceDevice : Device, IDeviceSimulation
{
    public double StepTime
    {
        get => GetParameter("step_time", 0.01);
        set => SetParameter("step_time", Math.Max(0.0, value));
    }

    public double InitialValue
    {
        get => GetParameter("init_val", 0.0);
        set => SetParameter("init_val", value);
    }

    public double FinalValue
    {
        get => GetParameter("final_val", 1.0);
        set => SetParameter("final_val", value);
    }

    public double OutputVoltage { get; set; }

    public StepSourceDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "step", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, 1, 0),  // Positive Out
            new(1, 0, -1, 0)  // Ground / Neg
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        double rOut = 0.1; // 100 mOhm source impedance
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        double v = (ctx.CurrentTime >= StepTime) ? FinalValue : InitialValue;
        OutputVoltage = v;

        double rOut = 0.1;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, v * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double vOut = ctx.GetNodeVoltage(Pins[0].NodeId);
            VoltageDrop = vOut;
            Current = (OutputVoltage - vOut) / 0.1;
        }
    }
}

public class StepSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "step";
    public override string DisplayName => "Step Generator";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+ Out", 1, 0, PinDirection.Right),
        new(1, "- Ref", -1, 0, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.6);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "StepTime", "Step Time (s)", "s", 0.0, 10.0, 0.01, 0.001,
        d => d is StepSourceDevice s ? s.StepTime : 0.01,
        (d, v) => { if (d is StepSourceDevice s) s.StepTime = Math.Max(0.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("InitialValue", "Initial Value", "V", -100.0, 100.0, 0.0, 0.1,
            d => d is StepSourceDevice s ? s.InitialValue : 0.0,
            (d, v) => { if (d is StepSourceDevice s) s.InitialValue = v; }),
        new("FinalValue", "Final Value", "V", -100.0, 100.0, 1.0, 0.1,
            d => d is StepSourceDevice s ? s.FinalValue : 1.0,
            (d, v) => { if (d is StepSourceDevice s) s.FinalValue = v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new StepSourceDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is StepSourceDevice s)
        {
            return $"Step({s.InitialValue:G2}→{s.FinalValue:G2}V @ {s.StepTime * 1000:G2}ms)";
        }
        return "Step Generator";
    }
}
