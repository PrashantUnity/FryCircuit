using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Summing Junction / Comparator Block:
/// Computes y = s0 * V_A + s1 * V_B + s2 * V_C
/// Configurable signs parameter, e.g. "+-" computes A - B (standard error comparator).
/// Pin 0: Input A (Left, -1, 0)
/// Pin 1: Input B (Bottom, 0, 1)
/// Pin 2: Input C (Top, 0, -1)
/// Pin 3: Output (Right, 1, 0)
/// Pin 4: Ref/Ground (Left-Down, -1, 1)
/// </summary>
public class SummingJunctionDevice : Device, IDeviceSimulation
{
    private string _signs = "+-";

    public string Signs
    {
        get => _signs;
        set => _signs = value ?? "+-";
    }

    public double OutputVoltage { get; set; }

    public SummingJunctionDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "sum", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0),  // Pin 0: Input A (Left)
            new(1, 0, 0, 1),   // Pin 1: Input B (Bottom)
            new(2, 0, 0, -1),  // Pin 2: Input C (Top)
            new(3, 0, 1, 0),   // Pin 3: Output (Right)
            new(4, 0, -1, 1)   // Pin 4: Ref (Common)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 4) return;
        int outNode = Pins[3].NodeId;
        int refNode = Pins.Count > 4 ? Pins[4].NodeId : -1;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        ctx.StampConductance(outNode, refNode, gOut);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 4) return;
        int outNode = Pins[3].NodeId;
        int refNode = Pins.Count > 4 ? Pins[4].NodeId : -1;

        double vRef = refNode >= 0 ? ctx.GetNodeVoltage(refNode) : 0.0;
        string signs = Signs;

        double sum = 0.0;
        // Pin 0 (Input A)
        double sA = (signs.Length > 0 && signs[0] == '-') ? -1.0 : 1.0;
        sum += sA * (ctx.GetNodeVoltage(Pins[0].NodeId) - vRef);

        // Pin 1 (Input B)
        if (signs.Length > 1)
        {
            double sB = (signs[1] == '-') ? -1.0 : 1.0;
            sum += sB * (ctx.GetNodeVoltage(Pins[1].NodeId) - vRef);
        }

        // Pin 2 (Input C)
        if (signs.Length > 2)
        {
            double sC = (signs[2] == '-') ? -1.0 : 1.0;
            sum += sC * (ctx.GetNodeVoltage(Pins[2].NodeId) - vRef);
        }

        OutputVoltage = sum;

        double rOut = 1.0;
        double gOut = 1.0 / rOut;
        double iNorton = sum * gOut;
        ctx.StampCurrentSource(refNode, outNode, iNorton);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 4)
        {
            double vOut = ctx.GetNodeVoltage(Pins[3].NodeId);
            VoltageDrop = vOut;
            Current = OutputVoltage - vOut;
        }
    }
}

public class SummingJunctionDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "sum";
    public override string DisplayName => "Summing Junction";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Input A", -1, 0, PinDirection.Left),
        new(1, "Input B", 0, 1, PinDirection.Down),
        new(2, "Input C", 0, -1, PinDirection.Up),
        new(3, "Output", 1, 0, PinDirection.Right),
        new(4, "Ref", -1, 1, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var sum = new SummingJunctionDevice(id, x, y);
        sum.SetupDefaultPins();
        return sum;
    }

    public override string FormatLabel(Device device)
    {
        return (device is SummingJunctionDevice sum) ? $"Σ ({sum.Signs})" : "Σ";
    }
}
