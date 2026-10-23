using System;
using System.Collections.Generic;
using System.Globalization;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices.Control;

#region 1. Signal Multiplier (Product Block)
/// <summary>
/// Analog/signal multiplier: Output y = Scale * (uA * uB)
/// Enables power calculation (P = V*I), frequency mixing, AM modulation,
/// and bilinear control / state-dependent gain loops.
/// </summary>
public class MultiplierDevice : Device, IDeviceSimulation
{
    public double Scale
    {
        get => GetParameter("scale", 1.0);
        set => SetParameter("scale", value);
    }

    public double OutputVoltage { get; set; }

    public MultiplierDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "mult", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -0.5), // Input A
            new(1, 0, -1, 0.5),  // Input B
            new(2, 0, 1, 0),     // Output
            new(3, 0, 0, 1)      // Common Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 3) return;
        int outNode = Pins[2].NodeId;
        int refNode = Pins.Count > 3 ? Pins[3].NodeId : -1;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 3) return;
        int outNode = Pins[2].NodeId;
        int refNode = Pins.Count > 3 ? Pins[3].NodeId : -1;

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double uA = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;
        double uB = ctx.GetNodeVoltage(Pins[1].NodeId) - vRef;

        double y = Scale * uA * uB;
        OutputVoltage = y;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, y * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 3)
        {
            double vOut = ctx.GetNodeVoltage(Pins[2].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class MultiplierDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "mult";
    public override string DisplayName => "Multiplier (A × B)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input A", -1, -0.5, PinDirection.Left),
        new(1, "Input B", -1, 0.5, PinDirection.Left),
        new(2, "Output", 1, 0, PinDirection.Right),
        new(3, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.2);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Scale", "Scale Factor", "×", -100.0, 100.0, 1.0, 0.1,
        d => d is MultiplierDevice m ? m.Scale : 1.0,
        (d, v) => { if (d is MultiplierDevice m) m.Scale = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new MultiplierDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is MultiplierDevice m && Math.Abs(m.Scale - 1.0) > 1e-4)
        {
            return $"× ({m.Scale:G2})";
        }
        return "A × B";
    }
}
#endregion

#region 2. Signal Divider (Quotient Block)
/// <summary>
/// Analog/signal divider: Output y = Scale * (uNum / (uDen + ε))
/// Includes configurable epsilon singularity avoidance.
/// </summary>
public class DividerDevice : Device, IDeviceSimulation
{
    public double Scale
    {
        get => GetParameter("scale", 1.0);
        set => SetParameter("scale", value);
    }

    public double Epsilon
    {
        get => GetParameter("epsilon", 1e-4);
        set => SetParameter("epsilon", Math.Max(1e-9, value));
    }

    public double OutputVoltage { get; set; }

    public DividerDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "div", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -0.5), // Numerator
            new(1, 0, -1, 0.5),  // Denominator
            new(2, 0, 1, 0),     // Output
            new(3, 0, 0, 1)      // Common Ref
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 3) return;
        int outNode = Pins[2].NodeId;
        int refNode = Pins.Count > 3 ? Pins[3].NodeId : -1;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 3) return;
        int outNode = Pins[2].NodeId;
        int refNode = Pins.Count > 3 ? Pins[3].NodeId : -1;

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        double uNum = ctx.GetNodeVoltage(Pins[0].NodeId) - vRef;
        double uDen = ctx.GetNodeVoltage(Pins[1].NodeId) - vRef;

        double denomSafe = Math.Abs(uDen) < Epsilon
            ? (uDen >= 0 ? Epsilon : -Epsilon)
            : uDen;

        double y = Scale * (uNum / denomSafe);
        OutputVoltage = y;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampCurrentSource(refNode, outNode, y * gOut);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 3)
        {
            double vOut = ctx.GetNodeVoltage(Pins[2].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class DividerDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "div";
    public override string DisplayName => "Divider (A ÷ B)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Numerator", -1, -0.5, PinDirection.Left),
        new(1, "Denominator", -1, 0.5, PinDirection.Left),
        new(2, "Output", 1, 0, PinDirection.Right),
        new(3, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.2);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Scale", "Scale Factor", "×", -100.0, 100.0, 1.0, 0.1,
        d => d is DividerDevice dv ? dv.Scale : 1.0,
        (d, v) => { if (d is DividerDevice dv) dv.Scale = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("Epsilon", "Zero Guard ε", "V", 1e-6, 0.1, 1e-4, 1e-4,
            d => d is DividerDevice dv ? dv.Epsilon : 1e-4,
            (d, v) => { if (d is DividerDevice dv) dv.Epsilon = Math.Max(1e-9, v); })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new DividerDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        return "A ÷ B";
    }
}
#endregion

#region 3. Math Function Expression Block f(u)
/// <summary>
/// Arbitrary user-defined mathematical expression block: Output y = f(u).
/// Supports trigonometric, exponential, square root, power, and algebraic expressions.
/// </summary>
public class MathExpressionDevice : Device, IDeviceSimulation
{
    private string _expression = "sin(u)";

    public string Expression
    {
        get => _expression;
        set => _expression = string.IsNullOrWhiteSpace(value) ? "u" : value.Trim();
    }

    public double OutputVoltage { get; set; }

    public MathExpressionDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "mathexpr", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Input u
            new(1, 0, 1, 0),  // Output y = f(u)
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

        double y = EvaluateExpression(_expression, u);
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

    /// <summary>
    /// Robust, fast expression evaluator for standard control functions f(u).
    /// </summary>
    public static double EvaluateExpression(string expr, double u)
    {
        if (string.IsNullOrWhiteSpace(expr)) return u;
        string e = expr.Trim().ToLowerInvariant().Replace(" ", "");

        try
        {
            if (e is "u") return u;
            if (e is "-u") return -u;

            // Strip outer matching parentheses if whole expression is enclosed: ( ... )
            if (e.StartsWith('(') && e.EndsWith(')'))
            {
                int depth = 0;
                bool matchedAll = true;
                for (int i = 0; i < e.Length - 1; i++)
                {
                    if (e[i] == '(') depth++;
                    else if (e[i] == ')') depth--;
                    if (depth == 0) { matchedAll = false; break; }
                }
                if (matchedAll)
                {
                    return EvaluateExpression(e[1..^1], u);
                }
            }

            // 1. Addition / Subtraction (lowest precedence - split first!)
            int addIdx = FindLowestPrecedenceOperator(e, '+', '-');
            if (addIdx > 0)
            {
                char op = e[addIdx];
                double left = EvaluateExpression(e[..addIdx], u);
                double right = EvaluateExpression(e[(addIdx + 1)..], u);
                return op == '+' ? left + right : left - right;
            }

            // 2. Multiplication / Division
            int multIdx = FindLowestPrecedenceOperator(e, '*', '/');
            if (multIdx > 0)
            {
                char op = e[multIdx];
                double left = EvaluateExpression(e[..multIdx], u);
                double right = EvaluateExpression(e[(multIdx + 1)..], u);
                return op == '*' ? left * right : (Math.Abs(right) < 1e-9 ? 0.0 : left / right);
            }

            // 3. Power: u^2, u^3, etc.
            int caretIdx = FindLowestPrecedenceOperator(e, '^', '^');
            if (caretIdx > 0)
            {
                double baseVal = EvaluateExpression(e[..caretIdx], u);
                double expVal = EvaluateExpression(e[(caretIdx + 1)..], u);
                return Math.Pow(baseVal, expVal);
            }

            // Direct standard functions
            if (e.StartsWith("sin(") && e.EndsWith(")")) return Math.Sin(EvaluateExpression(e[4..^1], u));
            if (e.StartsWith("cos(") && e.EndsWith(")")) return Math.Cos(EvaluateExpression(e[4..^1], u));
            if (e.StartsWith("tan(") && e.EndsWith(")")) return Math.Tan(EvaluateExpression(e[4..^1], u));
            if (e.StartsWith("exp(") && e.EndsWith(")")) return Math.Exp(Math.Clamp(EvaluateExpression(e[4..^1], u), -50.0, 50.0));
            if (e.StartsWith("log(") && e.EndsWith(")") || e.StartsWith("ln(") && e.EndsWith(")"))
            {
                int prefixLen = e.StartsWith("log(") ? 4 : 3;
                double val = EvaluateExpression(e[prefixLen..^1], u);
                return Math.Log(Math.Max(1e-9, val));
            }
            if (e.StartsWith("sqrt(") && e.EndsWith(")")) return Math.Sqrt(Math.Max(0.0, EvaluateExpression(e[5..^1], u)));
            if (e.StartsWith("abs(") && e.EndsWith(")")) return Math.Abs(EvaluateExpression(e[4..^1], u));
            if (e.StartsWith("tanh(") && e.EndsWith(")")) return Math.Tanh(EvaluateExpression(e[5..^1], u));
            if (e.StartsWith("sinh(") && e.EndsWith(")")) return Math.Sinh(Math.Clamp(EvaluateExpression(e[5..^1], u), -20.0, 20.0));
            if (e.StartsWith("cosh(") && e.EndsWith(")")) return Math.Cosh(Math.Clamp(EvaluateExpression(e[5..^1], u), -20.0, 20.0));
            if (e.StartsWith("sgn(") && e.EndsWith(")") || e.StartsWith("sign(") && e.EndsWith(")"))
            {
                int prefixLen = e.StartsWith("sgn(") ? 4 : 5;
                return Math.Sign(EvaluateExpression(e[prefixLen..^1], u));
            }

            if (double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }

            return u;
        }
        catch
        {
            return u;
        }
    }

    private static int FindLowestPrecedenceOperator(string s, char op1, char op2)
    {
        int parenDepth = 0;
        for (int i = s.Length - 1; i > 0; i--)
        {
            char c = s[i];
            if (c == ')') parenDepth++;
            else if (c == '(') parenDepth--;
            else if (parenDepth == 0 && (c == op1 || c == op2))
            {
                // Ensure not a leading unary minus
                if (c == '-' && (s[i - 1] == '*' || s[i - 1] == '/' || s[i - 1] == '+' || s[i - 1] == '('))
                    continue;
                return i;
            }
        }
        return -1;
    }
}

public class MathExpressionDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "mathexpr";
    public override string DisplayName => "Math Function f(u)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input u", -1, 0, PinDirection.Left),
        new(1, "Output y", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.4, 1.8);

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new MathExpressionDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is MathExpressionDevice me)
        {
            return $"f(u) = {me.Expression}";
        }
        return "f(u)";
    }
}
#endregion

#region 4. Dead-Zone Actuator Nonlinearity
/// <summary>
/// Dead-Zone block: Output is zero within threshold band [-W, +W],
/// and linear slope outside the dead-zone band.
/// Models mechanical gear backlash, valve stiction, and motor deadbands.
/// </summary>
public class DeadZoneDevice : Device, IDeviceSimulation
{
    public double Threshold
    {
        get => GetParameter("threshold", 0.5);
        set => SetParameter("threshold", Math.Max(0.0, value));
    }

    public double OutputVoltage { get; set; }

    public DeadZoneDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "deadzone", x, y)
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

        double y;
        if (u > Threshold) y = u - Threshold;
        else if (u < -Threshold) y = u + Threshold;
        else y = 0.0;

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

public class DeadZoneDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "deadzone";
    public override string DisplayName => "Dead-Zone";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Threshold", "Deadband ±W", "V", 0.0, 50.0, 0.5, 0.05,
        d => d is DeadZoneDevice dz ? dz.Threshold : 0.5,
        (d, v) => { if (d is DeadZoneDevice dz) dz.Threshold = Math.Max(0.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new DeadZoneDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is DeadZoneDevice dz)
        {
            return $"DeadZone(±{dz.Threshold:G2}V)";
        }
        return "DeadZone";
    }
}
#endregion

#region 5. Relay with Hysteresis Block
/// <summary>
/// Bang-bang relay with dual switch thresholds (hysteresis band).
/// Essential for thermostat bang-bang control, Schmitt triggers, and limit cycle analysis.
/// </summary>
public class RelayDevice : Device, IDeviceSimulation
{
    private bool _state = false;

    public double OnThreshold
    {
        get => GetParameter("on_thresh", 1.0);
        set => SetParameter("on_thresh", value);
    }

    public double OffThreshold
    {
        get => GetParameter("off_thresh", -1.0);
        set => SetParameter("off_thresh", value);
    }

    public double HighOutput
    {
        get => GetParameter("high_val", 5.0);
        set => SetParameter("high_val", value);
    }

    public double LowOutput
    {
        get => GetParameter("low_val", 0.0);
        set => SetParameter("low_val", value);
    }

    public double OutputVoltage { get; set; }

    public RelayDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "relay", x, y)
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

        double rOut = 0.5;
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

        if (u >= OnThreshold) _state = true;
        else if (u <= OffThreshold) _state = false;

        double y = _state ? HighOutput : LowOutput;
        OutputVoltage = y;

        double rOut = 0.5;
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
            Current = (OutputVoltage - vOut) / 0.5;
        }
    }
}

public class RelayDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "relay";
    public override string DisplayName => "Relay (Hysteresis)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "OnThreshold", "Switch On", "V", -50.0, 50.0, 1.0, 0.1,
        d => d is RelayDevice r ? r.OnThreshold : 1.0,
        (d, v) => { if (d is RelayDevice r) r.OnThreshold = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("OffThreshold", "Switch Off", "V", -50.0, 50.0, -1.0, 0.1,
            d => d is RelayDevice r ? r.OffThreshold : -1.0,
            (d, v) => { if (d is RelayDevice r) r.OffThreshold = v; }),
        new("HighOutput", "High Output", "V", -100.0, 100.0, 5.0, 0.5,
            d => d is RelayDevice r ? r.HighOutput : 5.0,
            (d, v) => { if (d is RelayDevice r) r.HighOutput = v; }),
        new("LowOutput", "Low Output", "V", -100.0, 100.0, 0.0, 0.5,
            d => d is RelayDevice r ? r.LowOutput : 0.0,
            (d, v) => { if (d is RelayDevice r) r.LowOutput = v; })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new RelayDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is RelayDevice r)
        {
            return $"Relay[{r.OffThreshold:G1}V → {r.OnThreshold:G1}V]";
        }
        return "Relay";
    }
}
#endregion

#region 6. Rate Limiter (Slew-Rate Limiter)
/// <summary>
/// Rate Limiter: Enforces maximum rising and falling derivative bounds |du/dt| &lt;= R.
/// Models hydraulic slew limits, motor speed ramps, and servo velocity limits.
/// </summary>
public class RateLimiterDevice : Device, IDeviceSimulation
{
    private double _lastOutput = 0.0;
    private double _lastTime = -1.0;

    public double RisingSlewRate
    {
        get => GetParameter("rising_rate", 10.0);
        set => SetParameter("rising_rate", Math.Max(0.001, value));
    }

    public double FallingSlewRate
    {
        get => GetParameter("falling_rate", -10.0);
        set => SetParameter("falling_rate", Math.Min(-0.001, value));
    }

    public double OutputVoltage { get; set; }

    public RateLimiterDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "ratelimiter", x, y)
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

        double dt = (_lastTime >= 0.0) ? Math.Max(1e-7, ctx.CurrentTime - _lastTime) : ctx.Dt;
        double maxDeltaRise = RisingSlewRate * dt;
        double maxDeltaFall = FallingSlewRate * dt; // negative

        double delta = u - _lastOutput;
        double y;
        if (delta > maxDeltaRise) y = _lastOutput + maxDeltaRise;
        else if (delta < maxDeltaFall) y = _lastOutput + maxDeltaFall;
        else y = u;

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
            _lastOutput = OutputVoltage;
            _lastTime = ctx.CurrentTime;

            double vOut = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class RateLimiterDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "ratelimiter";
    public override string DisplayName => "Rate Limiter (Slew)";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input", -1, 0, PinDirection.Left),
        new(1, "Output", 1, 0, PinDirection.Right),
        new(2, "Ref", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.0, 1.8);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "RisingSlewRate", "Rising Slew (V/s)", "V/s", 0.01, 1000.0, 10.0, 1.0,
        d => d is RateLimiterDevice rl ? rl.RisingSlewRate : 10.0,
        (d, v) => { if (d is RateLimiterDevice rl) rl.RisingSlewRate = Math.Max(0.001, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("FallingSlewRate", "Falling Slew (V/s)", "V/s", -1000.0, -0.01, -10.0, 1.0,
            d => d is RateLimiterDevice rl ? rl.FallingSlewRate : -10.0,
            (d, v) => { if (d is RateLimiterDevice rl) rl.FallingSlewRate = Math.Min(-0.001, v); })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new RateLimiterDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is RateLimiterDevice rl)
        {
            return $"Slew(±{rl.RisingSlewRate:G2}V/s)";
        }
        return "Rate Limiter";
    }
}
#endregion
