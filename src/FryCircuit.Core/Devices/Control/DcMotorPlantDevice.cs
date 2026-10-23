using System;
using System.Collections.Generic;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices.Control;

/// <summary>
/// Physical Electromechanical DC Motor Plant model:
/// Electrical: La * d(ia)/dt + Ra * ia = Va - Ke * omega
/// Mechanical: J * d(omega)/dt + B * omega = Kt * ia - T_load
/// Kinematic:  d(theta)/dt = omega
/// Bridges electrical circuits (batteries, ESC, H-Bridge) and control systems (speed/position loops).
/// </summary>
public class DcMotorPlantDevice : Device, IDeviceSimulation
{
    private double _ia = 0.0;
    private double _omega = 0.0;
    private double _theta = 0.0;

    public double ResistanceRa
    {
        get => GetParameter("ra", 1.0);
        set => SetParameter("ra", Math.Max(0.01, value));
    }

    public double InductanceLa
    {
        get => GetParameter("la", 0.005);
        set => SetParameter("la", Math.Max(1e-6, value));
    }

    public double TorqueConstantKt
    {
        get => GetParameter("kt", 0.05);
        set => SetParameter("kt", Math.Max(1e-4, value));
    }

    public double BackEmfConstantKe
    {
        get => GetParameter("ke", 0.05);
        set => SetParameter("ke", Math.Max(1e-4, value));
    }

    public double RotorInertiaJ
    {
        get => GetParameter("j", 0.001);
        set => SetParameter("j", Math.Max(1e-6, value));
    }

    public double ViscousDampingB
    {
        get => GetParameter("b", 0.002);
        set => SetParameter("b", Math.Max(0.0, value));
    }

    // Telemetry outputs
    public double ArmatureCurrent => _ia;
    public double AngularVelocity => _omega;
    public double ShaftAngle => _theta;
    public double SpeedRpm => _omega * (60.0 / (2.0 * Math.PI));

    public DcMotorPlantDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "dcmotor", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -2, -0.6), // Armature Pos (+)
            new(1, 0, -2, 0.6),  // Armature Neg (-)
            new(2, 0, 0, 1.2),   // Load Torque T_L input
            new(3, 0, 2, -0.6),  // Speed Output omega (rad/s)
            new(4, 0, 2, 0.6),   // Position Output theta (rad)
            new(5, 0, 1, 1.2)    // Mechanical Ref / Ground
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count < 5) return;
        int nArmPos = Pins[0].NodeId;
        int nArmNeg = Pins[1].NodeId;
        int nSpeed = Pins[3].NodeId;
        int nPos = Pins[4].NodeId;
        int nRef = Pins.Count > 5 ? Pins[5].NodeId : -1;

        // Armature Norton conductance
        double rArmEquiv = ResistanceRa + (InductanceLa / Math.Max(1e-6, ctx.Dt));
        double gArm = 1.0 / rArmEquiv;
        ctx.StampConductance(nArmPos, nArmNeg, gArm);

        // Mechanical outputs Norton conductance
        double gMech = 1.0;
        ctx.StampConductance(nSpeed, nRef, gMech);
        ctx.StampConductance(nPos, nRef, gMech);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count < 5) return;
        int nArmPos = Pins[0].NodeId;
        int nArmNeg = Pins[1].NodeId;
        int nSpeed = Pins[3].NodeId;
        int nPos = Pins[4].NodeId;
        int nRef = Pins.Count > 5 ? Pins[5].NodeId : -1;

        double dt = Math.Max(1e-6, ctx.Dt);
        double rArmEquiv = ResistanceRa + (InductanceLa / dt);
        double gArm = 1.0 / rArmEquiv;

        // Back-EMF opposing voltage + inductor history voltage
        double backEmf = BackEmfConstantKe * _omega;
        double vHist = (InductanceLa / dt) * _ia;
        double iArmNorton = (backEmf - vHist) * gArm;

        ctx.StampCurrentSource(nArmNeg, nArmPos, iArmNorton);

        // Stamp Speed & Position Norton currents
        double gMech = 1.0;
        ctx.StampCurrentSource(nRef, nSpeed, _omega * gMech);
        ctx.StampCurrentSource(nRef, nPos, _theta * gMech);
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 5) return;
        double dt = Math.Max(1e-6, ctx.Dt);

        double va = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        double tLoad = ctx.GetNodeVoltage(Pins[2].NodeId); // Load torque input signal

        // State update using RK2 / Heun integration for coupled mechatronic dynamics
        double dI1 = (va - ResistanceRa * _ia - BackEmfConstantKe * _omega) / InductanceLa;
        double dW1 = (TorqueConstantKt * _ia - ViscousDampingB * _omega - tLoad) / RotorInertiaJ;

        double iaMid = _ia + dI1 * dt;
        double wMid = _omega + dW1 * dt;

        double dI2 = (va - ResistanceRa * iaMid - BackEmfConstantKe * wMid) / InductanceLa;
        double dW2 = (TorqueConstantKt * iaMid - ViscousDampingB * wMid - tLoad) / RotorInertiaJ;

        _ia += 0.5 * dt * (dI1 + dI2);
        _omega += 0.5 * dt * (dW1 + dW2);
        _theta += _omega * dt;

        VoltageDrop = va;
        Current = _ia;
    }
}

public class DcMotorPlantDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "dcmotor";
    public override string DisplayName => "DC Motor Plant";
    public override string Category => "Control Systems";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Armature +", -2, -0.6, PinDirection.Left),
        new(1, "Armature -", -2, 0.6, PinDirection.Left),
        new(2, "Torque In TL", 0, 1.2, PinDirection.Down),
        new(3, "Speed Out ω", 2, -0.6, PinDirection.Right),
        new(4, "Angle Out θ", 2, 0.6, PinDirection.Right),
        new(5, "Mech Ref", 1, 1.2, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (4.0, 2.6);

    public override DeviceParameterSpec? PrimaryParameter => new(
        "TorqueConstantKt", "Torque Const Kt (N·m/A)", "N·m/A", 0.001, 10.0, 0.05, 0.005,
        d => d is DcMotorPlantDevice m ? m.TorqueConstantKt : 0.05,
        (d, v) => { if (d is DcMotorPlantDevice m) { m.TorqueConstantKt = v; m.BackEmfConstantKe = v; } }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new("ResistanceRa", "Armature Ra", "Ω", 0.01, 100.0, 1.0, 0.1,
            d => d is DcMotorPlantDevice m ? m.ResistanceRa : 1.0,
            (d, v) => { if (d is DcMotorPlantDevice m) m.ResistanceRa = Math.Max(0.01, v); }),
        new("InductanceLa", "Armature La", "mH", 0.0001, 1.0, 0.005, 0.001,
            d => d is DcMotorPlantDevice m ? m.InductanceLa * 1000.0 : 5.0,
            (d, v) => { if (d is DcMotorPlantDevice m) m.InductanceLa = Math.Max(1e-6, v / 1000.0); }),
        new("RotorInertiaJ", "Inertia J", "kg·m²", 1e-6, 1.0, 0.001, 0.0001,
            d => d is DcMotorPlantDevice m ? m.RotorInertiaJ : 0.001,
            (d, v) => { if (d is DcMotorPlantDevice m) m.RotorInertiaJ = Math.Max(1e-6, v); }),
        new("ViscousDampingB", "Damping B", "N·m·s", 0.0, 1.0, 0.002, 0.0005,
            d => d is DcMotorPlantDevice m ? m.ViscousDampingB : 0.002,
            (d, v) => { if (d is DcMotorPlantDevice m) m.ViscousDampingB = Math.Max(0.0, v); })
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new DcMotorPlantDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        if (device is DcMotorPlantDevice m)
        {
            return $"DC Motor ({m.SpeedRpm:F0} RPM)";
        }
        return "DC Motor";
    }
}
