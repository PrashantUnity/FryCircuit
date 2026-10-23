using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices.Control;

#region 1. Zero-Order Hold (ZOH / Sample & Hold)
/// <summary>
/// Zero-Order Hold (ZOH) block:
/// Samples continuous input u(t) at discrete intervals Ts and holds it constant:
/// y(t) = u(k * Ts) for k * Ts &lt;= t &lt; (k+1) * Ts.
/// Simulates digital DAC/ADC converters and sampled-data control interfaces.
/// </summary>
public class ZeroOrderHoldDevice : Device, IDeviceSimulation
{
    private double _heldValue = 0.0;
    private double _lastSampleTime = -1.0;

    public double SampleTime
    {
        get => GetParameter("sample_time", 0.01);
        set => SetParameter("sample_time", Math.Max(1e-6, value));
    }

    public double OutputVoltage { get; set; }

    public ZeroOrderHoldDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "zoh", x, y)
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
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        int outNode = Pins[1].NodeId;
        int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        // Sample on clock edge
        if (_lastSampleTime < 0.0 || (ctx.CurrentTime - _lastSampleTime) >= SampleTime - 1e-9)
        {
            _heldValue = u;
            _lastSampleTime = ctx.CurrentTime;
        }

        OutputVoltage = _heldValue;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, _heldValue * gOut);
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

public class ZeroOrderHoldDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "zoh";
    public override string DisplayName => "Zero-Order Hold (ZOH)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input u(t)", -1, 0, PinDirection.Left),
        new(1, "Output y[k]", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "SampleTime", "Sample Time Ts (s)", "s", 1e-5, 10.0, 0.01, 0.001,
        d => d is ZeroOrderHoldDevice z ? z.SampleTime : 0.01,
        (d, v) => { if (d is ZeroOrderHoldDevice z) z.SampleTime = Math.Max(1e-6, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new ZeroOrderHoldDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is ZeroOrderHoldDevice z)
        {
            return $"ZOH(Ts={z.SampleTime * 1000:G2}ms)";
        }
        return "ZOH";
    }
}
#endregion

#region 2. Discrete Unit Delay (z^-1)
/// <summary>
/// Unit delay block: y[k] = u[k-1]
/// Fundamental digital building block for difference equations, FIR filters, and state buffers.
/// </summary>
public class UnitDelayDevice : Device, IDeviceSimulation
{
    private double _storedValue = 0.0;
    private double _pendingValue = 0.0;
    private double _lastSampleTime = -1.0;

    public double SampleTime
    {
        get => GetParameter("sample_time", 0.01);
        set => SetParameter("sample_time", Math.Max(1e-6, value));
    }

    public double InitialCondition
    {
        get => GetParameter("init_cond", 0.0);
        set => SetParameter("init_cond", value);
    }

    public double OutputVoltage { get; set; }

    public UnitDelayDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "unitdelay", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input
            new(1, 0, 1, 0),  // Output
            new(2, 0, 0, 1)   // Common Ref
        };
        _storedValue = InitialCondition;
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

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        OutputVoltage = _storedValue;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, _storedValue * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        double vRef = Pins.Count > 2 ? ctx.GetNodeVoltage(Pins[2].NodeId) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        if (_lastSampleTime < 0.0 || (ctx.CurrentTime - _lastSampleTime) >= SampleTime - 1e-9)
        {
            _storedValue = _pendingValue;
            _pendingValue = u;
            _lastSampleTime = ctx.CurrentTime;
        }

        double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vOut;
        Current = OutputVoltage - vOut;
    }
}

public class UnitDelayDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "unitdelay";
    public override string DisplayName => "Unit Delay (z⁻¹)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input u[k]", -1, 0, PinDirection.Left),
        new(1, "Output y[k-1]", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "SampleTime", "Sample Time Ts (s)", "s", 1e-5, 10.0, 0.01, 0.001,
        d => d is UnitDelayDevice ud ? ud.SampleTime : 0.01,
        (d, v) => { if (d is UnitDelayDevice ud) ud.SampleTime = Math.Max(1e-6, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("InitialCondition", "Initial y[0]", "V", -100.0, 100.0, 0.0, 0.1,
            d => d is UnitDelayDevice ud ? ud.InitialCondition : 0.0,
            (d, v) => { if (d is UnitDelayDevice ud) ud.InitialCondition = v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new UnitDelayDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        return "z⁻¹";
    }
}
#endregion

#region 3. Discrete Transfer Function H(z)
/// <summary>
/// Pulse transfer function in the Z-domain:
/// H(z) = (b0 + b1*z^-1 + b2*z^-2 + ...) / (1 + a1*z^-1 + a2*z^-2 + ...)
/// Evaluated via difference equation recursion at each discrete sample period Ts.
/// </summary>
public class DiscreteTransferFunctionDevice : Device, IDeviceSimulation
{
    private string _numStr = "0.1, 0.05";
    private string _denStr = "1, -0.85";

    private double[] _numCoeffs = new[] { 0.1, 0.05 };
    private double[] _denCoeffs = new[] { 1.0, -0.85 };

    private readonly List<double> _inputHistory = new();
    private readonly List<double> _outputHistory = new();
    private double _lastSampleTime = -1.0;

    public string NumeratorString
    {
        get => _numStr;
        set
        {
            if (_numStr != value)
            {
                _numStr = value ?? "1";
                ParseCoeffs();
            }
        }
    }

    public string DenominatorString
    {
        get => _denStr;
        set
        {
            if (_denStr != value)
            {
                _denStr = value ?? "1";
                ParseCoeffs();
            }
        }
    }

    public double SampleTime
    {
        get => GetParameter("sample_time", 0.01);
        set => SetParameter("sample_time", Math.Max(1e-6, value));
    }

    public double OutputVoltage { get; set; }

    public DiscreteTransferFunctionDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "discretetf", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, 0), // Input u
            new(1, 0, 2, 0),  // Output y
            new(2, 0, 0, 1)   // Common Ref
        };
        ParseCoeffs();
    }

    private void ParseCoeffs()
    {
        try
        {
            _numCoeffs = ParseArray(_numStr);
            _denCoeffs = ParseArray(_denStr);
            if (_denCoeffs.Length == 0 || Math.Abs(_denCoeffs[0]) < 1e-12)
            {
                _denCoeffs = new[] { 1.0 };
            }
            // Normalize so a0 = 1.0
            double a0 = _denCoeffs[0];
            for (int i = 0; i < _numCoeffs.Length; i++) _numCoeffs[i] /= a0;
            for (int i = 0; i < _denCoeffs.Length; i++) _denCoeffs[i] /= a0;

            _inputHistory.Clear();
            _outputHistory.Clear();
        }
        catch
        {
            _numCoeffs = new[] { 1.0 };
            _denCoeffs = new[] { 1.0 };
        }
    }

    private static double[] ParseArray(string text)
    {
        string[] parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<double>();
        foreach (var p in parts)
        {
            if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                list.Add(v);
            }
        }
        return list.Count > 0 ? list.ToArray() : new[] { 1.0 };
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

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        // Instantaneous direct feedthrough: y = b0 * u + (history terms)
        double direct = _numCoeffs.Length > 0 ? _numCoeffs[0] * u : 0.0;
        double histTerms = 0.0;

        for (int j = 1; j < _numCoeffs.Length; j++)
        {
            int histIdx = _inputHistory.Count - j;
            if (histIdx >= 0) histTerms += _numCoeffs[j] * _inputHistory[histIdx];
        }
        for (int i = 1; i < _denCoeffs.Length; i++)
        {
            int histIdx = _outputHistory.Count - i;
            if (histIdx >= 0) histTerms -= _denCoeffs[i] * _outputHistory[histIdx];
        }

        double y = direct + histTerms;
        OutputVoltage = y;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, y * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;
        double vRef = Pins.Count > 2 ? ctx.GetNodeVoltage(Pins[2].NodeId) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        if (_lastSampleTime < 0.0 || (ctx.CurrentTime - _lastSampleTime) >= SampleTime - 1e-9)
        {
            _inputHistory.Add(u);
            _outputHistory.Add(OutputVoltage);

            // Cap history to order size
            int maxHist = Math.Max(_numCoeffs.Length, _denCoeffs.Length) + 5;
            if (_inputHistory.Count > maxHist) _inputHistory.RemoveAt(0);
            if (_outputHistory.Count > maxHist) _outputHistory.RemoveAt(0);

            _lastSampleTime = ctx.CurrentTime;
        }

        double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vOut;
        Current = OutputVoltage - vOut;
    }
}

public class DiscreteTransferFunctionDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "discretetf";
    public override string DisplayName => "Discrete TF H(z)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input u[k]", -2, 0, PinDirection.Left),
        new(1, "Output y[k]", 2, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (3.8, 2.0);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "SampleTime", "Sample Time Ts (s)", "s", 1e-5, 10.0, 0.01, 0.001,
        d => d is DiscreteTransferFunctionDevice dtf ? dtf.SampleTime : 0.01,
        (d, v) => { if (d is DiscreteTransferFunctionDevice dtf) dtf.SampleTime = Math.Max(1e-6, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new DiscreteTransferFunctionDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is DiscreteTransferFunctionDevice dtf)
        {
            return $"H(z) [Ts={dtf.SampleTime * 1000:G2}ms]";
        }
        return "H(z)";
    }
}
#endregion
