using System;
using System.Collections.Generic;
using System.Globalization;
using FryCircuit.Core.Control;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Continuous State-Space block representation of arbitrary-order dynamical system:
/// dx/dt = A * x + B * u
///     y = C * x + D * u
/// Supports custom matrix strings (e.g. A="0, 1; -2, -3", B="0; 1", C="1, 0", D="0").
/// Solves using 4th-Order Runge-Kutta (RK4) numerical integration.
/// </summary>
public class StateSpaceBlockDevice : Device, IDeviceSimulation
{
    private string _aString = "0, 1; -2, -3";
    private string _bString = "0; 1";
    private string _cString = "1, 0";
    private string _dString = "0";

    private StateSpace? _stateSpace;
    private bool _isDirty = true;

    public string AString
    {
        get => _aString;
        set
        {
            if (_aString != value)
            {
                _aString = value ?? "0";
                _isDirty = true;
            }
        }
    }

    public string BString
    {
        get => _bString;
        set
        {
            if (_bString != value)
            {
                _bString = value ?? "0";
                _isDirty = true;
            }
        }
    }

    public string CString
    {
        get => _cString;
        set
        {
            if (_cString != value)
            {
                _cString = value ?? "0";
                _isDirty = true;
            }
        }
    }

    public string DString
    {
        get => _dString;
        set
        {
            if (_dString != value)
            {
                _dString = value ?? "0";
                _isDirty = true;
            }
        }
    }

    public int Order => _stateSpace?.StateCount ?? 0;
    public double OutputVoltage { get; set; }
    public IReadOnlyList<double>? States => _stateSpace?.State;

    public void SetStates(ReadOnlySpan<double> states)
    {
        EnsureModel();
        _stateSpace?.SetStates(states);
    }

    public StateSpaceBlockDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "statespace", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, 0), // Input u
            new(1, 0, 2, 0),  // Output y
            new(2, 0, 0, 1)   // Common Ref
        };
        RebuildModel();
    }

    public void RebuildModel()
    {
        try
        {
            double[,] a = ParseMatrix(_aString);
            double[,] b = ParseMatrix(_bString);
            double[,] c = ParseMatrix(_cString);
            double[,] d = ParseMatrix(_dString);

            _stateSpace = new StateSpace(a, b, c, d);
            _isDirty = false;
        }
        catch
        {
            // Default 1st order lag fallback: x_dot = -x + u, y = x
            _stateSpace = new StateSpace(
                new double[,] { { -1.0 } },
                new double[,] { { 1.0 } },
                new double[,] { { 1.0 } },
                new double[,] { { 0.0 } }
            );
            _isDirty = false;
        }
    }

    private void EnsureModel()
    {
        if (_isDirty || _stateSpace == null)
        {
            RebuildModel();
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

        EnsureModel();

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

        double y = _stateSpace?.ComputeOutput(u) ?? u;
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
            EnsureModel();

            double vRef = Pins.Count > 2 ? ctx.GetNodeVoltage(Pins[2].NodeId) : 0.0;
            double u = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;

            _stateSpace?.Advance(u, ctx.Dt);

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }

    public static double[,] ParseMatrix(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new double[1, 1];

        // Clean brackets
        string clean = text.Trim().Replace("[", "").Replace("]", "");
        string[] rowTokens = clean.Split(';', StringSplitOptions.RemoveEmptyEntries);

        int rows = rowTokens.Length;
        var parsedRows = new List<double[]>();

        for (int r = 0; r < rows; r++)
        {
            string rowStr = rowTokens[r].Trim();
            // Split by comma or space
            string[] colTokens = rowStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var colVals = new double[colTokens.Length];
            for (int c = 0; c < colTokens.Length; c++)
            {
                if (double.TryParse(colTokens[c], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    colVals[c] = val;
                }
            }
            parsedRows.Add(colVals);
        }

        if (parsedRows.Count == 0) return new double[1, 1];

        int cols = parsedRows[0].Length;
        double[,] mat = new double[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < Math.Min(cols, parsedRows[r].Length); c++)
            {
                mat[r, c] = parsedRows[r][c];
            }
        }

        return mat;
    }
}

public class StateSpaceBlockDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "statespace";
    public override string DisplayName => "State-Space [A,B,C,D]";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input u", -2, 0, PinDirection.Left),
        new(1, "Output y", 2, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (4.0, 2.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new StateSpaceBlockDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is StateSpaceBlockDevice ss)
        {
            return $"State-Space (n={ss.Order})";
        }
        return "dx/dt=Ax+Bu";
    }
}
