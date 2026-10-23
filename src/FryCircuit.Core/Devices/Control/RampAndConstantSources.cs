using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices.Control;

#region 1. Ramp Signal Source
/// <summary>
/// Continuous linear ramp reference excitation:
/// y(t) = InitialValue for t &lt; StartTime
/// y(t) = InitialValue + Slope * (t - StartTime) for t &gt;= StartTime
/// Essential for Type 1 &amp; Type 2 control system velocity tracking error analysis (ess = 1/Kv).
/// </summary>
public class RampSourceDevice : Device, IDeviceSimulation
{
    public double StartTime
    {
        get => GetParameter("start_time", 0.0);
        set => SetParameter("start_time", Math.Max(0.0, value));
    }

    public double Slope
    {
        get => GetParameter("slope", 1.0);
        set => SetParameter("slope", value);
    }

    public double InitialValue
    {
        get => GetParameter("init_val", 0.0);
        set => SetParameter("init_val", value);
    }

    public double OutputVoltage { get; set; }

    public RampSourceDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "ramp", x, y)
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

        double rOut = 0.1;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        double v = ctx.CurrentTime < StartTime
            ? InitialValue
            : InitialValue + Slope * (ctx.CurrentTime - StartTime);

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

public class RampSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "ramp";
    public override string DisplayName => "Ramp Generator";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+ Out", 1, 0, PinDirection.Right),
        new(1, "- Ref", -1, 0, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.6);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Slope", "Slope (V/s)", "V/s", -100.0, 100.0, 1.0, 0.1,
        d => d is RampSourceDevice r ? r.Slope : 1.0,
        (d, v) => { if (d is RampSourceDevice r) r.Slope = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("StartTime", "Start Time", "s", 0.0, 100.0, 0.0, 0.01,
            d => d is RampSourceDevice r ? r.StartTime : 0.0,
            (d, v) => { if (d is RampSourceDevice r) r.StartTime = Math.Max(0.0, v); }),
        new("InitialValue", "Initial Value", "V", -100.0, 100.0, 0.0, 0.1,
            d => d is RampSourceDevice r ? r.InitialValue : 0.0,
            (d, v) => { if (d is RampSourceDevice r) r.InitialValue = v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new RampSourceDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is RampSourceDevice r)
        {
            return $"Ramp(m={r.Slope:G2}V/s @ t={r.StartTime:G2}s)";
        }
        return "Ramp Gen";
    }
}
#endregion

#region 2. Constant Setpoint Source
/// <summary>
/// Steady DC constant setpoint / offset source for control reference commands.
/// </summary>
public class ConstantSourceDevice : Device, IDeviceSimulation
{
    public double Value
    {
        get => GetParameter("value", 1.0);
        set => SetParameter("value", value);
    }

    public double OutputVoltage { get; set; }

    public ConstantSourceDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "const", x, y)
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

        double rOut = 0.05;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        OutputVoltage = Value;

        double rOut = 0.05;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, Value * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double vOut = ctx.GetNodeVoltage(Pins[0].NodeId);
            VoltageDrop = vOut;
            Current = (OutputVoltage - vOut) / 0.05;
        }
    }
}

public class ConstantSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "const";
    public override string DisplayName => "Constant Setpoint";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+ Out", 1, 0, PinDirection.Right),
        new(1, "- Ref", -1, 0, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (1.8, 1.4);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Value", "Setpoint Value", "V", -100.0, 100.0, 1.0, 0.1,
        d => d is ConstantSourceDevice c ? c.Value : 1.0,
        (d, v) => { if (d is ConstantSourceDevice c) c.Value = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new ConstantSourceDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is ConstantSourceDevice c)
        {
            return $"K={c.Value:G3}";
        }
        return "Setpoint";
    }
}
#endregion

#region 3. Gaussian White Noise Generator
/// <summary>
/// Band-limited Gaussian white noise generator for sensor noise simulation,
/// robust control testing, and Kalman filter benchmark experiments.
/// </summary>
public class NoiseSourceDevice : Device, IDeviceSimulation
{
    private Random? _rng;
    private double _currentSample;
    private double _lastSampleTime = -1.0;

    public double Amplitude
    {
        get => GetParameter("amplitude", 0.1);
        set => SetParameter("amplitude", Math.Max(0.0, value));
    }

    public double Mean
    {
        get => GetParameter("mean", 0.0);
        set => SetParameter("mean", value);
    }

    public int Seed
    {
        get => (int)GetParameter("seed", 42.0);
        set => SetParameter("seed", value);
    }

    public double OutputVoltage { get; set; }

    public NoiseSourceDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "noise", x, y)
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

        double rOut = 0.1;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        if (_rng == null)
        {
            _rng = new Random(Seed);
        }

        // Hold sample during Newton-Raphson iteration of the same timestep
        if (Math.Abs(ctx.CurrentTime - _lastSampleTime) > 1e-12)
        {
            // Box-Muller transform for standard Gaussian N(0, 1)
            double u1 = Math.Max(1e-10, _rng.NextDouble());
            double u2 = _rng.NextDouble();
            double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

            _currentSample = Mean + Amplitude * z0;
            _lastSampleTime = ctx.CurrentTime;
        }

        OutputVoltage = _currentSample;

        double rOut = 0.1;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, _currentSample * gOut);
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

public class NoiseSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "noise";
    public override string DisplayName => "Noise Generator";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+ Out", 1, 0, PinDirection.Right),
        new(1, "- Ref", -1, 0, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.6);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Amplitude", "Noise σ (V)", "V", 0.0, 50.0, 0.1, 0.01,
        d => d is NoiseSourceDevice n ? n.Amplitude : 0.1,
        (d, v) => { if (d is NoiseSourceDevice n) n.Amplitude = Math.Max(0.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("Mean", "Mean Offset", "V", -50.0, 50.0, 0.0, 0.1,
            d => d is NoiseSourceDevice n ? n.Mean : 0.0,
            (d, v) => { if (d is NoiseSourceDevice n) n.Mean = v; }),
        new("Seed", "RNG Seed", "", 1.0, 100000.0, 42.0, 1.0,
            d => d is NoiseSourceDevice n ? n.Seed : 42,
            (d, v) => { if (d is NoiseSourceDevice n) n.Seed = (int)v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new NoiseSourceDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is NoiseSourceDevice n)
        {
            return $"Noise(σ={n.Amplitude:G2}V)";
        }
        return "Noise Gen";
    }
}
#endregion

#region 4. Pulse / Square Reference Generator
/// <summary>
/// Periodic rectangular pulse / square wave generator for control loop excitation.
/// </summary>
public class PulseSourceDevice : Device, IDeviceSimulation
{
    public double Period
    {
        get => GetParameter("period", 0.1);
        set => SetParameter("period", Math.Max(1e-6, value));
    }

    public double DutyCycle
    {
        get => GetParameter("duty", 50.0);
        set => SetParameter("duty", Math.Clamp(value, 0.0, 100.0));
    }

    public double HighValue
    {
        get => GetParameter("high_val", 1.0);
        set => SetParameter("high_val", value);
    }

    public double LowValue
    {
        get => GetParameter("low_val", 0.0);
        set => SetParameter("low_val", value);
    }

    public double Delay
    {
        get => GetParameter("delay", 0.0);
        set => SetParameter("delay", Math.Max(0.0, value));
    }

    public double OutputVoltage { get; set; }

    public PulseSourceDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "pulse", x, y)
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

        double rOut = 0.1;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[0].NodeId;
        int refNode = Pins[1].NodeId;

        double t = ctx.CurrentTime - Delay;
        double v;
        if (t < 0.0)
        {
            v = LowValue;
        }
        else
        {
            double phase = t % Period;
            double highDuration = Period * (DutyCycle / 100.0);
            v = (phase < highDuration) ? HighValue : LowValue;
        }

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

public class PulseSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "pulse";
    public override string DisplayName => "Pulse Generator";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+ Out", 1, 0, PinDirection.Right),
        new(1, "- Ref", -1, 0, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.6);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Period", "Period (s)", "s", 1e-5, 100.0, 0.1, 0.01,
        d => d is PulseSourceDevice p ? p.Period : 0.1,
        (d, v) => { if (d is PulseSourceDevice p) p.Period = Math.Max(1e-6, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("DutyCycle", "Duty Cycle", "%", 0.0, 100.0, 50.0, 1.0,
            d => d is PulseSourceDevice p ? p.DutyCycle : 50.0,
            (d, v) => { if (d is PulseSourceDevice p) p.DutyCycle = Math.Clamp(v, 0.0, 100.0); }),
        new("HighValue", "High Value", "V", -100.0, 100.0, 1.0, 0.1,
            d => d is PulseSourceDevice p ? p.HighValue : 1.0,
            (d, v) => { if (d is PulseSourceDevice p) p.HighValue = v; }),
        new("LowValue", "Low Value", "V", -100.0, 100.0, 0.0, 0.1,
            d => d is PulseSourceDevice p ? p.LowValue : 0.0,
            (d, v) => { if (d is PulseSourceDevice p) p.LowValue = v; }),
        new("Delay", "Initial Delay", "s", 0.0, 100.0, 0.0, 0.01,
            d => d is PulseSourceDevice p ? p.Delay : 0.0,
            (d, v) => { if (d is PulseSourceDevice p) p.Delay = Math.Max(0.0, v); })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new PulseSourceDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is PulseSourceDevice p)
        {
            return $"Pulse({p.Period * 1000:G2}ms, {p.DutyCycle:G0}%)";
        }
        return "Pulse Gen";
    }
}
#endregion
