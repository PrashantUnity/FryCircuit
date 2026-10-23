using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Continuous Integrator Block:
/// y(t) = y(0) + integral(u(tau) dtau)
/// </summary>
public class IntegratorDevice : Device, IDeviceSimulation
{
    public double InitialCondition
    {
        get => GetParameter("init_cond", 0.0);
        set => SetParameter("init_cond", value);
    }

    private double _state;
    public double OutputVoltage { get; set; }

    public IntegratorDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "integrator", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input
            new(1, 0, 1, 0),  // Output
            new(2, 0, 0, 1)   // Common Ref
        };
        _state = InitialCondition;
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

        OutputVoltage = _state;

        double rOut = 1.0;
        ctx.StampCurrentSource(refNode, outNode, _state / rOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;
            double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
            double u = vIn - vRef;

            double dt = ctx.Dt > 0 ? ctx.Dt : 1e-4;
            _state += u * dt;

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class IntegratorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "integrator";
    public override string DisplayName => "Integrator (1/s)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "InitialCondition", "Initial Condition (V)", "V", -100.0, 100.0, 0.0, 0.1,
        d => d is IntegratorDevice integ ? integ.InitialCondition : 0.0,
        (d, v) => { if (d is IntegratorDevice integ) integ.InitialCondition = v; }
    );

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new IntegratorDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device) => "1 / s";
}

/// <summary>
/// Filtered Derivative Block:
/// G(s) = s / (tau * s + 1)
/// </summary>
public class DerivativeDevice : Device, IDeviceSimulation
{
    public double TimeConstantTau
    {
        get => GetParameter("tau", 0.005);
        set => SetParameter("tau", Math.Max(1e-6, value));
    }

    private double _prevInput;
    private double _output;
    public double OutputVoltage { get; set; }

    public DerivativeDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "derivative", x, y)
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

        OutputVoltage = _output;

        double rOut = 1.0;
        ctx.StampCurrentSource(refNode, outNode, _output / rOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;
            double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
            double u = vIn - vRef;

            double dt = ctx.Dt > 0 ? ctx.Dt : 1e-4;
            double tau = TimeConstantTau;
            double alpha = dt / (tau + dt);
            double rawDeriv = (u - _prevInput) / dt;

            _output = (1.0 - alpha) * _output + alpha * rawDeriv;
            _prevInput = u;

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class DerivativeDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "derivative";
    public override string DisplayName => "Derivative (d/dt)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Tau", "Filter Tau (s)", "s", 1e-5, 1.0, 0.005, 0.001,
        d => d is DerivativeDevice deriv ? deriv.TimeConstantTau : 0.005,
        (d, v) => { if (d is DerivativeDevice deriv) deriv.TimeConstantTau = Math.Max(1e-6, v); }
    );

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new DerivativeDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device) => "d/dt";
}
