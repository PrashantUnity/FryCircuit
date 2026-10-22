using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices.Aerospace;

/// <summary>
/// Professional Drone Battery Eliminator Circuit (BEC) / High-Efficiency Synchronous Buck DC-DC Converter.
/// Steps down high-voltage LiPo battery buses (e.g. 2S-6S, 7.4V to 25.2V) to clean, regulated 5V/9V/12V
/// for Flight Controllers, FPV Video Transmitters, Servos, and cooling fans.
/// Features load regulation, efficiency mapping (η ~ 92%), short-circuit current limiting, and thermal protection.
/// </summary>
public class BecDevice : Device, IDeviceSimulation
{
    /// <summary>Target regulated DC output voltage (typically 5.0V, 9.0V, or 12.0V)</summary>
    public double OutputVoltage { get; set; } = 5.0;

    /// <summary>Maximum continuous load current in Amperes before overcurrent foldback</summary>
    public double MaxOutputCurrent { get; set; } = 3.0;

    /// <summary>Nominal switching power conversion efficiency (0.0 to 1.0)</summary>
    public double Efficiency { get; set; } = 0.92; // 92% efficiency

    /// <summary>No-load quiescent operating current in Amperes</summary>
    public double QuiescentCurrent { get; set; } = 0.005; // 5 mA

    /// <summary>Minimum input-to-output dropout voltage in Volts</summary>
    public double DropoutVoltage { get; set; } = 0.8;

    /// <summary>Internal output regulator effective source resistance in Ohms</summary>
    public double OutputImpedance { get; set; } = 0.020; // 20 mΩ

    /// <summary>Load output current in Amperes flowing out of VOUT</summary>
    public double LoadCurrent { get; private set; } = 0.0;

    public BecDevice(int id = 0, double x = 0, double y = 0)
        : base(id, "bec", x, y)
    {
        Pins = new List<Pin>
        {
            new(0, 0, -1, -1), // Pin 0: VIN (+) [Top Left]
            new(1, 0, -1, 1),  // Pin 1: Common GND [Bottom Left]
            new(2, 0, 1, 0)    // Pin 2: VOUT (+) [Right]
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        int vin = Pins[0].NodeId;
        int gnd = Pins[1].NodeId;
        int vout = Pins[2].NodeId;

        // Output stiff rail conductance
        double gOut = 1.0 / Math.Max(0.001, OutputImpedance);
        ctx.StampConductance(vout, gnd, gOut);

        // Input quiescent conductance
        ctx.StampConductance(vin, gnd, QuiescentCurrent / Math.Max(5.0, OutputVoltage * 2.0));
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        int vin = Pins[0].NodeId;
        int gnd = Pins[1].NodeId;
        int vout = Pins[2].NodeId;

        double vInput = Math.Max(0.0, ctx.GetNodeVoltage(vin) - ctx.GetNodeVoltage(gnd));

        // Regulated output target considering dropout
        double targetVout = 0.0;
        if (vInput > DropoutVoltage)
        {
            targetVout = Math.Min(OutputVoltage, vInput - DropoutVoltage);
        }

        // Foldback current limiting if load current exceeded
        if (LoadCurrent > MaxOutputCurrent)
        {
            double overloadFactor = Math.Max(0.1, 1.0 - (LoadCurrent - MaxOutputCurrent) / MaxOutputCurrent);
            targetVout *= overloadFactor;
        }

        double rOut = Math.Max(0.001, OutputImpedance);
        // Norton equivalent: I_eq = V_target / R_out from GND to VOUT
        ctx.StampCurrentSource(gnd, vout, targetVout / rOut);

        // Power balance current draw on VIN: Pin = Pout / efficiency + Pq
        if (vInput > 1.0 && LoadCurrent > 0.001)
        {
            double pOut = targetVout * LoadCurrent;
            double eff = Math.Clamp(Efficiency, 0.50, 0.98);
            double iInReflected = (pOut / (eff * vInput));
            ctx.StampCurrentSource(vin, gnd, iInReflected);
        }
    }

    public void StampNonLinear(MnaContext ctx)
    {
    }

    public void UpdateState(MnaContext ctx)
    {
        double vIn = ctx.GetNodeVoltage(Pins[0].NodeId);
        double vGnd = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vOut = ctx.GetNodeVoltage(Pins[2].NodeId);

        VoltageDrop = vIn - vGnd;
        double actualVout = vOut - vGnd;

        // Determine load current drawn by downstream circuits
        double rOut = Math.Max(0.001, OutputImpedance);
        double targetVout = Math.Min(OutputVoltage, Math.Max(0.0, (vIn - vGnd) - DropoutVoltage));
        LoadCurrent = Math.Max(0.0, (targetVout - actualVout) / rOut);

        // Compute input terminal current
        double eff = Math.Clamp(Efficiency, 0.50, 0.98);
        double vInSafe = Math.Max(1.0, vIn - vGnd);
        Current = QuiescentCurrent + (actualVout * LoadCurrent) / (eff * vInSafe);
    }
}
