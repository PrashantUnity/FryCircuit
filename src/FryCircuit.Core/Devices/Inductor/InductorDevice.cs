using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices;

public class InductorDevice : Device, IDeviceSimulation
{
    public double Inductance
    {
        get => GetParameter("inductance", 1e-3);
        set => SetParameter("inductance", Math.Max(1e-12, value));
    }

    public double SeriesResistance
    {
        get => GetParameter("esr", 0.05); // 50 mΩ default winding ESR
        set => SetParameter("esr", Math.Max(1e-6, value));
    }

    // Companion model state variables
    public double StateCurrent { get; set; }
    public double StateVoltage { get; set; }
    public double EquivCurrent { get; set; }

    public InductorDevice(int id = 0, double x = 0, double y = 0, double inductance = 1e-3)
        : base(id, "inductor", x, y)
    {
        Inductance = inductance;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double zL = (ctx.Method == IntegrationMethod.Trapezoidal)
                ? (2.0 * Inductance / ctx.Dt)
                : (Inductance / ctx.Dt);
            double gl = 1.0 / (SeriesResistance + zL);
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gl);
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double zL = (ctx.Method == IntegrationMethod.Trapezoidal)
                ? (2.0 * Inductance / ctx.Dt)
                : (Inductance / ctx.Dt);
            double gl = 1.0 / (SeriesResistance + zL);
            double iEq;
            if (ctx.Method == IntegrationMethod.Trapezoidal)
            {
                iEq = gl * StateVoltage + gl * (zL - SeriesResistance) * StateCurrent;
            }
            else
            {
                iEq = gl * zL * StateCurrent;
            }
            EquivCurrent = iEq;
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, iEq);
        }
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;

            double zL = (ctx.Method == IntegrationMethod.Trapezoidal)
                ? (2.0 * Inductance / ctx.Dt)
                : (Inductance / ctx.Dt);
            double gl = 1.0 / (SeriesResistance + zL);
            double iEq;
            if (ctx.Method == IntegrationMethod.Trapezoidal)
            {
                iEq = gl * StateVoltage + gl * (zL - SeriesResistance) * StateCurrent;
            }
            else
            {
                iEq = gl * zL * StateCurrent;
            }

            double iBranch = gl * VoltageDrop + iEq;
            StateCurrent = iBranch;
            StateVoltage = VoltageDrop;
            Current = iBranch;
        }
    }
}
