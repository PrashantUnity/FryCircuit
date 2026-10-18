using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Battery;

/// <summary>
/// Professional Aerospace and Drone Lithium-Ion / LiPo Electrochemical Battery Cell Model.
/// Features temperature-dependent internal ESR resistance (Arrhenius derating),
/// nonlinear open-circuit voltage Voc(SOC) curve, and dynamic Coulomb-counting charge depletion.
/// </summary>
public class BatteryCellDevice : Device, IDeviceSimulation
{
    /// <summary>Nominal cell storage capacity in Ampere-hours (e.g. 2.2 Ah for drone pack)</summary>
    public double CapacityAmpHours { get; set; } = 2.2;

    /// <summary>State of Charge (SOC) from 0.0 (fully depleted) to 1.0 (fully charged)</summary>
    public double StateOfCharge { get; set; } = 0.90; // 90% initial charge

    /// <summary>Internal series resistance (ESR) R_0 in Ohms at reference room temperature (25°C)</summary>
    public double NominalSeriesResistance { get; set; } = 0.018; // 18 mΩ

    /// <summary>Activation energy for ionic electrolyte transport in Joules</summary>
    public double ActivationEnergyJoules { get; set; } = 0.35 * PhysicalConstants.ElementaryCharge; // 0.35 eV

    /// <summary>
    /// Open-circuit voltage computed from the electrochemical State of Charge (SOC).
    /// Typically 3.0V at 0% SOC to 4.20V at 100% SOC for drone LiPo chemistry.
    /// </summary>
    public double OpenCircuitVoltage
    {
        get
        {
            double soc = Math.Clamp(StateOfCharge, 0.0, 1.0);
            return 3.0 + 0.85 * soc + 0.35 * Math.Pow(soc, 3) - 0.2 * Math.Pow(1.0 - soc, 4);
        }
    }

    public BatteryCellDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "battery", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, 0), // Pin 0: Positive terminal (+)
            new(1, 0, 1, 0)   // Pin 1: Negative terminal (-)
        };
    }

    /// <summary>
    /// Computes internal series resistance at the operating temperature using Arrhenius relation.
    /// In cold space/aerospace temperatures (-55°C), ESR surges exponentially.
    /// </summary>
    public double GetSeriesResistanceAtTemperature(double tempKelvin)
    {
        double tRef = 298.15; // 25°C
        double tSafe = Math.Max(20.0, tempKelvin);
        double exponent = (ActivationEnergyJoules / PhysicalConstants.BoltzmannConstant) * (1.0 / tSafe - 1.0 / tRef);
        double derating = Math.Exp(Math.Clamp(exponent, -5.0, 10.0));
        return Math.Max(0.001, NominalSeriesResistance * derating);
    }

    public void StampConductance(MnaContext ctx)
    {
        double r0 = GetSeriesResistanceAtTemperature(ctx.TemperatureKelvin);
        double g0 = 1.0 / r0;
        ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, g0);
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        double r0 = GetSeriesResistanceAtTemperature(ctx.TemperatureKelvin);
        double voc = OpenCircuitVoltage;

        // Norton equivalent: I_eq = V_oc / R_0
        // Flows from negative terminal (Pin 1) into positive terminal (Pin 0)
        double iEq = voc / r0;
        ctx.StampCurrentSource(Pins[1].NodeId, Pins[0].NodeId, iEq);
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vPos = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vNeg = ctx.GetNodeVoltage(Pins[1].NodeId);
        VoltageDrop = vPos - vNeg;

        double r0 = GetSeriesResistanceAtTemperature(ctx.TemperatureKelvin);
        double voc = OpenCircuitVoltage;

        // Actual terminal discharge current flowing out of positive terminal
        double iDischarge = (voc - VoltageDrop) / r0;
        Current = iDischarge;

        // Dynamic Coulomb Counting: update State of Charge (SOC)
        if (CapacityAmpHours > 0 && ctx.Dt > 0)
        {
            double totalCapacityCoulombs = CapacityAmpHours * 3600.0;
            double deltaCharge = iDischarge * ctx.Dt;
            StateOfCharge = Math.Clamp(StateOfCharge - (deltaCharge / totalCapacityCoulombs), 0.0, 1.0);
        }
    }
}
