using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// Electro-Mechanical DC Brushless Cooling Fan & Propeller Blower model.
/// Simulates motor coil resistance, back-EMF voltage generator, rotor inertia,
/// aerodynamic quadratic air-resistance drag torque, live dynamic RPM acceleration,
/// airflow delivery in CFM, and optical/Hall tachometer pulse feedback.
/// </summary>
public class FanDevice : Device, IDeviceSimulation
{
    /// <summary>Nominal design operating voltage (e.g. 5V, 12V, or 24V DC)</summary>
    public double RatedVoltage { get; set; } = 12.0;

    /// <summary>Nominal operating current at rated voltage and steady-state speed</summary>
    public double RatedCurrent { get; set; } = 0.25; // 250 mA

    /// <summary>Maximum aerodynamic impeller rotational speed at rated voltage</summary>
    public double MaxRpm { get; set; } = 5000.0;

    /// <summary>Minimum starting voltage required to overcome static cogging bearing friction</summary>
    public double StartVoltage { get; set; } = 4.0;

    /// <summary>Maximum rated airflow volume at max RPM in Cubic Feet per Minute (CFM)</summary>
    public double AirflowRatingCfm { get; set; } = 35.0;

    /// <summary>Armature coil copper series resistance in Ohms</summary>
    public double CoilResistance { get; set; } = 12.0; // 12 Ω

    /// <summary>Effective rotor + impeller moment of inertia in kg*m^2</summary>
    public double RotorInertia { get; set; } = 1.2e-6;

    /// <summary>Aerodynamic drag damping coefficient</summary>
    public double AerodynamicDragCoeff { get; set; } = 1.8e-8;

    // --- Dynamic Simulation State ---
    /// <summary>Current instantaneous rotor angular velocity in radians per second</summary>
    public double AngularVelocityRadS { get; set; } = 0.0;

    /// <summary>Current mechanical rotation speed in Revolutions Per Minute (RPM)</summary>
    public double CurrentRpm => AngularVelocityRadS * (60.0 / (2.0 * Math.PI));

    /// <summary>Instantaneous airflow delivery in CFM based on current speed</summary>
    public double CurrentAirflowCfm => Math.Max(0.0, (CurrentRpm / Math.Max(1.0, MaxRpm)) * AirflowRatingCfm);

    /// <summary>Instantaneous rotor mechanical angle in radians (for continuous UI spinning animation)</summary>
    public double RotorAngleRadians { get; set; } = 0.0;

    /// <summary>Back-EMF constant Ke = Kt in V/(rad/s)</summary>
    public double BackEmfConstant
    {
        get
        {
            double maxOmega = MaxRpm * (2.0 * Math.PI / 60.0);
            return maxOmega > 0 ? (RatedVoltage - RatedCurrent * CoilResistance) / maxOmega : 0.01;
        }
    }

    /// <summary>Generated internal back-EMF voltage</summary>
    public double BackEmfVoltage => Math.Max(0.0, BackEmfConstant * AngularVelocityRadS);

    public FanDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "fan", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Pin 0: Positive terminal (+) [Left]
            new(1, 0, 1, 0),  // Pin 1: Negative terminal (-) [Right]
            new(2, 0, 0, 1)   // Pin 2: Tachometer pulse (TACH) [Bottom]
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        double g = 1.0 / Math.Max(0.1, CoilResistance);
        // Stamp stator winding resistance between Pin 0 (+) and Pin 1 (-)
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, g);

        // Tachometer pin has internal pull-up / logic output impedance to Pin 1
        if (Pins.Count > 2)
        {
            ctx.StampConductance(Pins[2].NodeId, Pins[1].NodeId, 1e-4); // 10 kΩ output impedance
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        double r = Math.Max(0.1, CoilResistance);
        double eEmf = BackEmfVoltage;

        // Norton equivalent for Back-EMF: opposes forward current (flows from negative to positive)
        double iNorton = eEmf / r;
        ctx.StampCurrentSource(Pins[1].NodeId, Pins[0].NodeId, iNorton);

        // Tachometer pulse generator: 2 pulses per revolution
        if (Pins.Count > 2 && CurrentRpm > 50)
        {
            // Square wave based on rotor angle (2 electrical cycles per mechanical rev)
            bool tachHigh = (Math.Sin(2.0 * RotorAngleRadians) > 0);
            double vTach = tachHigh ? 5.0 : 0.0;
            double gTach = 1e-3; // 1 kΩ equivalent driver
            ctx.StampCurrentSource(Pins[1].NodeId, Pins[2].NodeId, vTach * gTach);
        }
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vPos = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vNeg = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vPos - vNeg;

        double r = Math.Max(0.1, CoilResistance);
        double eEmf = BackEmfVoltage;

        // Armature current
        double iArmature = (VoltageDrop - eEmf) / r;
        Current = Math.Max(0.0, iArmature);

        double dt = ctx.Dt;
        if (dt <= 0) return;

        // Electromagnetic torque
        double kt = BackEmfConstant;
        double torqueElectro = (VoltageDrop >= StartVoltage || AngularVelocityRadS > 10.0)
            ? kt * Current
            : 0.0;

        // Mechanical drag & friction torque
        double omega = AngularVelocityRadS;
        double torqueDrag = AerodynamicDragCoeff * (omega * omega);
        double torqueFriction = 1.5e-6 * omega;

        // Net acceleration: d(omega)/dt = (Te - Tdrag - Tfriction) / J
        double torqueNet = torqueElectro - torqueDrag - torqueFriction;
        double alpha = torqueNet / Math.Max(1e-9, RotorInertia);

        // Integrate angular speed with damping
        omega = Math.Max(0.0, omega + alpha * dt);

        // Update rotor angle
        RotorAngleRadians = (RotorAngleRadians + omega * dt) % (2.0 * Math.PI);
        AngularVelocityRadS = omega;
    }
}
