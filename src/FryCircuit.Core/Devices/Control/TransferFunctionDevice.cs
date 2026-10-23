using System;
using System.Collections.Generic;
using FryCircuit.Core.Control;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Schematic Transfer Function Block:
/// G(s) = N(s) / D(s)
/// Pin 0: Signal Input (-2, 0)
/// Pin 1: Signal Output (+2, 0)
/// Pin 2: Ground / Common Reference (0, 1)
/// Realized via real-time discrete state-space controllable canonical form.
/// </summary>
public class TransferFunctionDevice : Device, IDeviceSimulation
{
    private StateSpace? _stateSpace;
    private string _numStr = "1";
    private string _denStr = "1, 1";
    private string _cachedNumStr = "";
    private string _cachedDenStr = "";
    private TransferFunction? _cachedTf;

    public string NumeratorString
    {
        get => _numStr;
        set
        {
            _numStr = value ?? "1";
            InvalidateStateSpace();
        }
    }

    public string DenominatorString
    {
        get => _denStr;
        set
        {
            _denStr = value ?? "1, 1"; // Default: 1 / (s + 1)
            InvalidateStateSpace();
        }
    }

    public double OutputVoltage { get; set; }

    public TransferFunction CurrentTransferFunction
    {
        get
        {
            EnsureStateSpace();
            return _cachedTf ?? TransferFunction.One;
        }
    }

    public TransferFunctionDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "tf", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, 0),
            new(1, 0, 2, 0),
            new(2, 0, 0, 1)
        };
    }

    private void InvalidateStateSpace()
    {
        _stateSpace = null;
        _cachedTf = null;
    }

    private void EnsureStateSpace()
    {
        string num = NumeratorString;
        string den = DenominatorString;

        if (_stateSpace != null && num == _cachedNumStr && den == _cachedDenStr)
            return;

        try
        {
            _cachedTf = TransferFunction.Parse(num, den);
            _stateSpace = _cachedTf.ToStateSpace();
            _cachedNumStr = num;
            _cachedDenStr = den;
        }
        catch
        {
            _cachedTf = TransferFunction.One;
            _stateSpace = _cachedTf.ToStateSpace();
        }
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

        EnsureStateSpace();

        double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = vIn - vRef;

        // Compute output using current state without advancing
        double y = _stateSpace?.ComputeOutput(u) ?? u;
        OutputVoltage = y;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        double iNorton = y * gOut;

        ctx.StampCurrentSource(refNode, outNode, iNorton);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            EnsureStateSpace();

            int refNode = Pins.Count > 2 ? Pins[2].NodeId : -1;
            double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
            double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
            double u = vIn - vRef;
            double dt = ctx.Dt > 0 ? ctx.Dt : 1e-4;

            // Advance state once per simulation time-step
            _stateSpace?.Advance(u, dt);

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class TransferFunctionDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "tf";
    public override string DisplayName => "Transfer Function";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -2, 0, PinDirection.Left),
        new(1, "Output", 2, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (4.0, 2.0);

    public override Device CreateInstance(int id, double x, double y)
    {
        var tf = new TransferFunctionDevice(id, x, y);
        tf.SetupDefaultPins();
        return tf;
    }

    public override string FormatLabel(Device device)
    {
        if (device is TransferFunctionDevice tf)
        {
            return $"G(s) = ({tf.NumeratorString}) / ({tf.DenominatorString})";
        }
        return "G(s)";
    }
}
