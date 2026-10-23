using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Industrial-grade PID Controller block:
/// u(t) = Kp * e(t) + Ki * integral(e) + Kd * d(e)/dt
/// Includes filtered derivative pole (N), output clamping [Min, Max], and anti-windup integration.
/// Pin 0: Error Input e(t)
/// Pin 1: Control Output u(t)
/// Pin 2: Ref / Ground
/// </summary>
public class PidControllerDevice : Device, IDeviceSimulation
{
    public double Kp
    {
        get => GetParameter("kp", 1.0);
        set => SetParameter("kp", value);
    }

    public double Ki
    {
        get => GetParameter("ki", 0.0);
        set => SetParameter("ki", value);
    }

    public double Kd
    {
        get => GetParameter("kd", 0.0);
        set => SetParameter("kd", value);
    }

    public double FilterN
    {
        get => GetParameter("n_filter", 20.0);
        set => SetParameter("n_filter", Math.Max(1.0, value));
    }

    public double OutMin
    {
        get => GetParameter("out_min", -10.0);
        set => SetParameter("out_min", value);
    }

    public double OutMax
    {
        get => GetParameter("out_max", 10.0);
        set => SetParameter("out_max", value);
    }

    public bool AntiWindup
    {
        get => GetParameter("anti_windup", 1.0) > 0.5;
        set => SetParameter("anti_windup", value ? 1.0 : 0.0);
    }

    private double _integralState;
    private double _derivState;
    private double _prevError;
    public double OutputVoltage { get; set; }

    public PidControllerDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "pid", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0),  // Pin 0: Error In
            new(1, 0, 1, 0),   // Pin 1: Output
            new(2, 0, 0, 1)    // Pin 2: Ground Ref
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
        double error = vIn - vRef;

        double pTerm = Kp * error;
        double dTerm = _derivState;
        double rawOutput = pTerm + _integralState + dTerm;
        double clampedOutput = Math.Clamp(rawOutput, OutMin, OutMax);
        OutputVoltage = clampedOutput;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        double iNorton = clampedOutput * gOut;
        ctx.StampCurrentSource(refNode, outNode, iNorton);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;
            double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
            double error = vIn - vRef;

            double dt = ctx.Dt > 0 ? ctx.Dt : 1e-4;

            double pTerm = Kp * error;
            double de = (error - _prevError) / dt;
            double dDeriv = FilterN * (Kd * de - _derivState);
            _derivState += dt * dDeriv;

            double rawOutput = pTerm + _integralState + _derivState;
            bool isSaturated = rawOutput > OutMax || rawOutput < OutMin;
            bool errorReversesSaturation = (rawOutput > OutMax && error < 0) || (rawOutput < OutMin && error > 0);

            if (!AntiWindup || !isSaturated || errorReversesSaturation)
            {
                _integralState += Ki * error * dt;
            }

            _prevError = error;

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class PidControllerDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "pid";
    public override string DisplayName => "PID Controller";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Error In", -1, 0, PinDirection.Left),
        new(1, "Control Out", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.4, 2.0);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Kp", "Proportional Gain (Kp)", "", 0.0, 1000.0, 1.0, 0.1,
        d => d is PidControllerDevice pid ? pid.Kp : 1.0,
        (d, v) => { if (d is PidControllerDevice pid) pid.Kp = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("Ki", "Integral Gain (Ki)", "", 0.0, 1000.0, 0.0, 0.1,
            d => d is PidControllerDevice pid ? pid.Ki : 0.0,
            (d, v) => { if (d is PidControllerDevice pid) pid.Ki = v; }),
        new("Kd", "Derivative Gain (Kd)", "", 0.0, 100.0, 0.0, 0.01,
            d => d is PidControllerDevice pid ? pid.Kd : 0.0,
            (d, v) => { if (d is PidControllerDevice pid) pid.Kd = v; }),
        new("N", "Derivative Filter (N)", "", 1.0, 100.0, 20.0, 1.0,
            d => d is PidControllerDevice pid ? pid.FilterN : 20.0,
            (d, v) => { if (d is PidControllerDevice pid) pid.FilterN = Math.Max(1.0, v); })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var pid = new PidControllerDevice(id, x, y);
        pid.SetupDefaultPins();
        return pid;
    }

    public override string FormatLabel(Device device)
    {
        if (device is PidControllerDevice pid)
        {
            return $"PID (P={pid.Kp:G3}, I={pid.Ki:G3}, D={pid.Kd:G3})";
        }
        return "PID";
    }
}
