using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// 3-Phase Permanent Magnet Brushless DC (BLDC) Drone Propulsion Motor.
/// Features 3-phase Wye/Star stator winding model, rotor electrical angle tracking,
/// 3-phase sinusoidal/trapezoidal back-EMF generation, dynamic torque acceleration,
/// aerodynamic propeller drag, and real-time thrust calculation (grams / Newtons).
/// </summary>
public class BldcMotorDevice : Device, IDeviceSimulation
{
    /// <summary>Motor velocity constant Kv in RPM/Volt (e.g. 2300 Kv for 5" FPV drone)</summary>
    public double Kv { get; set; } = 2300.0;

    /// <summary>Phase-to-neutral stator winding resistance in Ohms (e.g. 0.065 Ω)</summary>
    public double PhaseResistance { get; set; } = 0.065;

    /// <summary>Number of rotor pole pairs (typically 7 pairs for 14-pole drone outrunner)</summary>
    public int PolePairs { get; set; } = 7;

    /// <summary>Propeller diameter in inches (e.g. 5.0" for standard quadcopter)</summary>
    public double PropellerDiameterInches { get; set; } = 5.0;

    /// <summary>Propeller aerodynamic pitch in inches (e.g. 4.3")</summary>
    public double PropellerPitchInches { get; set; } = 4.3;

    /// <summary>Total rotor bell + propeller moment of inertia in kg*m^2</summary>
    public double RotorInertia { get; set; } = 3.5e-6;

    /// <summary>Propeller aerodynamic drag torque constant</summary>
    public double AerodynamicDragCoeff { get; set; } = 4.2e-8;

    // --- Dynamic Simulation State ---
    /// <summary>Rotor mechanical angular velocity in radians per second</summary>
    public double AngularVelocityRadS { get; set; } = 0.0;

    /// <summary>Rotor rotational speed in Revolutions Per Minute (RPM)</summary>
    public double CurrentRpm => AngularVelocityRadS * (60.0 / (2.0 * Math.PI));

    /// <summary>Instantaneous rotor electrical angle in radians</summary>
    public double ElectricalAngleRadians { get; set; } = 0.0;

    /// <summary>Instantaneous rotor mechanical angle in radians (for UI propeller spinning animation)</summary>
    public double MechanicalAngleRadians { get; set; } = 0.0;

    /// <summary>Dynamic aerodynamic thrust output in grams (1 gf = 0.00980665 N)</summary>
    public double ThrustGrams { get; set; } = 0.0;

    /// <summary>Back-EMF peak line-to-neutral constant in V/(rad/s)</summary>
    public double BackEmfConstant => (Kv > 0) ? (60.0 / (2.0 * Math.PI * Kv)) * Math.Sqrt(2.0 / 3.0) : 0.005;

    public BldcMotorDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "bldc", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -1), // Phase U [Top Left]
            new(1, 0, -1, 0),  // Phase V [Middle Left]
            new(2, 0, -1, 1)   // Phase W [Bottom Left]
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        // 3-Phase Wye to Delta equivalent: R_delta = 3 * R_phase
        double rDelta = Math.Max(0.005, 3.0 * PhaseResistance);
        double gDelta = 1.0 / rDelta;

        int u = Pins[0].NodeId;
        int v = Pins[1].NodeId;
        int w = Pins[2].NodeId;

        ctx.StampConductance(u, v, gDelta);
        ctx.StampConductance(v, w, gDelta);
        ctx.StampConductance(w, u, gDelta);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (AngularVelocityRadS <= 0.1) return;

        double ke = BackEmfConstant;
        double th = ElectricalAngleRadians;

        // Line-to-neutral back-EMFs
        double eu = ke * AngularVelocityRadS * Math.Sin(th);
        double ev = ke * AngularVelocityRadS * Math.Sin(th - 2.0 * Math.PI / 3.0);
        double ew = ke * AngularVelocityRadS * Math.Sin(th + 2.0 * Math.PI / 3.0);

        // Line-to-line back-EMFs
        double euv = eu - ev;
        double evw = ev - ew;
        double ewu = ew - eu;

        double rDelta = Math.Max(0.005, 3.0 * PhaseResistance);

        int u = Pins[0].NodeId;
        int v = Pins[1].NodeId;
        int w = Pins[2].NodeId;

        // Norton equivalent currents: I_eq = E_line / R_delta
        ctx.StampCurrentSource(v, u, euv / rDelta);
        ctx.StampCurrentSource(w, v, evw / rDelta);
        ctx.StampCurrentSource(u, w, ewu / rDelta);
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vu = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vv = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vw = ctx.GetNodeVoltage(Pins[2].NodeId);

        // Terminal line-to-line voltages
        double vuv = vu - vv;
        double vvw = vv - vw;
        double vwu = vw - vu;

        VoltageDrop = Math.Max(Math.Abs(vuv), Math.Max(Math.Abs(vvw), Math.Abs(vwu)));

        double rDelta = Math.Max(0.005, 3.0 * PhaseResistance);

        // Compute phase branch currents
        double ke = BackEmfConstant;
        double th = ElectricalAngleRadians;
        double euv = ke * AngularVelocityRadS * (Math.Sin(th) - Math.Sin(th - 2.0 * Math.PI / 3.0));
        double evw = ke * AngularVelocityRadS * (Math.Sin(th - 2.0 * Math.PI / 3.0) - Math.Sin(th + 2.0 * Math.PI / 3.0));
        double ewu = ke * AngularVelocityRadS * (Math.Sin(th + 2.0 * Math.PI / 3.0) - Math.Sin(th));

        double iuv = (vuv - euv) / rDelta;
        double ivw = (vvw - evw) / rDelta;
        double iwu = (vwu - ewu) / rDelta;

        // Total motor electrical input current RMS
        Current = Math.Sqrt((iuv * iuv + ivw * ivw + iwu * iwu) / 3.0);

        double dt = ctx.Dt;
        if (dt <= 0) return;

        // Total electromechanical power
        double pe = euv * iuv + evw * ivw + ewu * iwu;
        double omega = AngularVelocityRadS;

        double torqueElectro = 0.0;
        if (omega > 0.5)
        {
            torqueElectro = Math.Max(0.0, pe / omega);
        }
        else if (Current > 0.1)
        {
            // Starting electromagnetic torque
            torqueElectro = ke * Current * 1.5;
        }

        // Aerodynamic drag and mechanical friction
        double torqueDrag = AerodynamicDragCoeff * (omega * omega);
        double torqueFriction = 0.0003 * omega;

        double alpha = (torqueElectro - torqueDrag - torqueFriction) / Math.Max(1e-9, RotorInertia);
        omega = Math.Max(0.0, omega + alpha * dt);

        // Integrate mechanical and electrical angles
        MechanicalAngleRadians = (MechanicalAngleRadians + omega * dt) % (2.0 * Math.PI);
        ElectricalAngleRadians = (ElectricalAngleRadians + PolePairs * omega * dt) % (2.0 * Math.PI);
        AngularVelocityRadS = omega;

        // Calculate static aerodynamic propeller thrust:
        // Thrust = Ct * rho * n^2 * D^4
        // With rho = 1.225 kg/m^3 (sea level air), D in meters, n in rev/sec
        double dMeters = PropellerDiameterInches * 0.0254;
        double nRevPerSec = omega / (2.0 * Math.PI);
        double ct = 0.108; // Typical high-performance multi-rotor airfoil coefficient
        double thrustNewtons = ct * 1.225 * Math.Pow(nRevPerSec, 2.0) * Math.Pow(dMeters, 4.0);
        ThrustGrams = Math.Max(0.0, thrustNewtons * (1000.0 / 9.80665));
    }
}
