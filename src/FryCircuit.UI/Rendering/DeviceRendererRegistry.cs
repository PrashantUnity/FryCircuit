using FryCircuit.UI.Rendering.Devices.Aerospace;
using FryCircuit.UI.Rendering.Devices.Battery;
using FryCircuit.UI.Rendering.Devices.Capacitor;
using FryCircuit.UI.Rendering.Devices.CurrentSource;
using FryCircuit.UI.Rendering.Devices.Diode;
using FryCircuit.UI.Rendering.Devices.Display;
using FryCircuit.UI.Rendering.Devices.Fuse;
using FryCircuit.UI.Rendering.Devices.Ground;
using FryCircuit.UI.Rendering.Devices.Inductor;
using FryCircuit.UI.Rendering.Devices.Lamp;
using FryCircuit.UI.Rendering.Devices.Logic;
using FryCircuit.UI.Rendering.Devices.Meters;
using FryCircuit.UI.Rendering.Devices.NetLabel;
using FryCircuit.UI.Rendering.Devices.OpAmp;
using FryCircuit.UI.Rendering.Devices.Potentiometer;
using FryCircuit.UI.Rendering.Devices.Quantum;
using FryCircuit.UI.Rendering.Devices.Resistor;
using FryCircuit.UI.Rendering.Devices.Sensors;
using FryCircuit.UI.Rendering.Devices.Switch;
using FryCircuit.UI.Rendering.Devices.Timer;
using FryCircuit.UI.Rendering.Devices.TransmissionLine;
using FryCircuit.UI.Rendering.Devices.Transistor;
using FryCircuit.UI.Rendering.Devices.VoltageSource;

namespace FryCircuit.UI.Rendering;

public static class DeviceRendererRegistry
{
    private static readonly Dictionary<string, IDeviceRenderer> _renderers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["resistor"] = new ResistorRenderer(),
        ["capacitor"] = new CapacitorRenderer(),
        ["inductor"] = new InductorRenderer(),
        ["ground"] = new GroundRenderer(),
        ["vsource"] = new VoltageSourceRenderer(),
        ["vsrcsine"] = new VoltageSourceRenderer(),
        ["csource"] = new CurrentSourceRenderer(),
        ["isrc"] = new CurrentSourceRenderer(),
        ["diode"] = new DiodeRenderer(),
        ["zener"] = new DiodeRenderer(),
        ["led"] = new DiodeRenderer(),
        ["npn"] = new BjtRenderer(),
        ["pnp"] = new BjtRenderer(),
        ["nmos"] = new MosfetRenderer(),
        ["pmos"] = new MosfetRenderer(),
        ["opamp"] = new OpAmpRenderer(),
        ["potentiometer"] = new PotentiometerRenderer(),
        ["spst"] = new SwitchRenderer(),
        ["spdt"] = new SwitchRenderer(),
        ["switch"] = new SwitchRenderer(),
        ["and"] = new LogicGateRenderer(),
        ["or"] = new LogicGateRenderer(),
        ["not"] = new LogicGateRenderer(),
        ["nand"] = new LogicGateRenderer(),
        ["nor"] = new LogicGateRenderer(),
        ["xor"] = new LogicGateRenderer(),
        ["xnor"] = new LogicGateRenderer(),
        ["timer555"] = new Timer555Renderer(),
        ["555"] = new Timer555Renderer(),
        ["josephson"] = new JosephsonJunctionRenderer(),
        ["jj"] = new JosephsonJunctionRenderer(),
        ["squid"] = new SquidRenderer(),
        ["tunneldiode"] = new TunnelDiodeRenderer(),
        ["esaki"] = new TunnelDiodeRenderer(),
        ["tline"] = new TransmissionLineRenderer(),
        ["transmissionline"] = new TransmissionLineRenderer(),
        ["cosmicipulse"] = new CosmicIonPulseRenderer(),
        ["ionpulse"] = new CosmicIonPulseRenderer(),
        ["seu"] = new CosmicIonPulseRenderer(),
        ["hallsensor"] = new HallEffectSensorRenderer(),
        ["hall"] = new HallEffectSensorRenderer(),
        ["battery"] = new BatteryCellRenderer(),
        ["cell"] = new BatteryCellRenderer(),
        ["lipo"] = new BatteryCellRenderer(),
        ["netlabel"] = new NetLabelRenderer(),
        ["voltmeter"] = new MeterRenderer(),
        ["amperemeter"] = new MeterRenderer(),
        ["ammeter"] = new MeterRenderer(),
        ["ohmmeter"] = new MeterRenderer(),
        ["ohm"] = new MeterRenderer(),
        ["lamp"] = new LampRenderer(),
        ["fuse"] = new FuseRenderer(),
        ["seg7"] = new SevenSegmentDisplayRenderer(),
        ["7seg"] = new SevenSegmentDisplayRenderer(),
        ["sevensegment"] = new SevenSegmentDisplayRenderer(),
        ["display"] = new SevenSegmentDisplayRenderer(),
        ["bcd7seg"] = new BcdTo7SegmentRenderer(),
        ["7448"] = new BcdTo7SegmentRenderer(),
        ["7447"] = new BcdTo7SegmentRenderer(),
        ["counter4"] = new BinaryCounterRenderer(),
        ["74193"] = new BinaryCounterRenderer(),
        ["counter"] = new BinaryCounterRenderer(),
        ["fan"] = new FanRenderer(),
        ["dcfan"] = new FanRenderer(),
        ["cooler"] = new FanRenderer(),
        ["blower"] = new FanRenderer(),
        ["bldc"] = new BldcMotorRenderer(),
        ["bldcmotor"] = new BldcMotorRenderer(),
        ["motor"] = new BldcMotorRenderer(),
        ["esc"] = new EscRenderer(),
        ["bldcesc"] = new EscRenderer(),
        ["bec"] = new BecRenderer(),
        ["buck"] = new BecRenderer(),
        ["buzzer"] = new PiezoBuzzerRenderer(),
        ["piezo"] = new PiezoBuzzerRenderer(),
        ["tvs"] = new TvsDiodeRenderer(),
        ["tvsdiode"] = new TvsDiodeRenderer(),

        // Control Systems Renderers
        ["tf"] = new FryCircuit.UI.Rendering.Devices.Control.TransferFunctionRenderer(),
        ["transferfunction"] = new FryCircuit.UI.Rendering.Devices.Control.TransferFunctionRenderer(),
        ["sum"] = new FryCircuit.UI.Rendering.Devices.Control.SummingJunctionRenderer(),
        ["comparator"] = new FryCircuit.UI.Rendering.Devices.Control.SummingJunctionRenderer(),
        ["summingjunction"] = new FryCircuit.UI.Rendering.Devices.Control.SummingJunctionRenderer(),
        ["pid"] = new FryCircuit.UI.Rendering.Devices.Control.PidControllerRenderer(),
        ["pidcontroller"] = new FryCircuit.UI.Rendering.Devices.Control.PidControllerRenderer(),
        ["gain"] = new FryCircuit.UI.Rendering.Devices.Control.GainBlockRenderer(),
        ["multiplier"] = new FryCircuit.UI.Rendering.Devices.Control.GainBlockRenderer(),
        ["step"] = new FryCircuit.UI.Rendering.Devices.Control.StepSourceRenderer(),
        ["stepsource"] = new FryCircuit.UI.Rendering.Devices.Control.StepSourceRenderer(),
        ["stepgen"] = new FryCircuit.UI.Rendering.Devices.Control.StepSourceRenderer(),
        ["integrator"] = new FryCircuit.UI.Rendering.Devices.Control.IntegratorRenderer(),
        ["integ"] = new FryCircuit.UI.Rendering.Devices.Control.IntegratorRenderer(),
        ["derivative"] = new FryCircuit.UI.Rendering.Devices.Control.DerivativeRenderer(),
        ["deriv"] = new FryCircuit.UI.Rendering.Devices.Control.DerivativeRenderer(),
        ["saturation"] = new FryCircuit.UI.Rendering.Devices.Control.SaturationRenderer(),
        ["limiter"] = new FryCircuit.UI.Rendering.Devices.Control.SaturationRenderer(),
        ["delay"] = new FryCircuit.UI.Rendering.Devices.Control.TimeDelayRenderer(),
        ["timedelay"] = new FryCircuit.UI.Rendering.Devices.Control.TimeDelayRenderer(),

        // Universal Control & Systems Dynamics Renderers
        ["ramp"] = new FryCircuit.UI.Rendering.Devices.Control.RampSourceRenderer(),
        ["rampsource"] = new FryCircuit.UI.Rendering.Devices.Control.RampSourceRenderer(),
        ["rampgen"] = new FryCircuit.UI.Rendering.Devices.Control.RampSourceRenderer(),
        ["const"] = new FryCircuit.UI.Rendering.Devices.Control.ConstantSourceRenderer(),
        ["constant"] = new FryCircuit.UI.Rendering.Devices.Control.ConstantSourceRenderer(),
        ["setpoint"] = new FryCircuit.UI.Rendering.Devices.Control.ConstantSourceRenderer(),
        ["noise"] = new FryCircuit.UI.Rendering.Devices.Control.NoiseSourceRenderer(),
        ["whitenoise"] = new FryCircuit.UI.Rendering.Devices.Control.NoiseSourceRenderer(),
        ["pulse"] = new FryCircuit.UI.Rendering.Devices.Control.PulseSourceRenderer(),
        ["pulsesource"] = new FryCircuit.UI.Rendering.Devices.Control.PulseSourceRenderer(),
        ["mult"] = new FryCircuit.UI.Rendering.Devices.Control.MultiplierRenderer(),
        ["prod"] = new FryCircuit.UI.Rendering.Devices.Control.MultiplierRenderer(),
        ["product"] = new FryCircuit.UI.Rendering.Devices.Control.MultiplierRenderer(),
        ["div"] = new FryCircuit.UI.Rendering.Devices.Control.DividerRenderer(),
        ["divider"] = new FryCircuit.UI.Rendering.Devices.Control.DividerRenderer(),
        ["mathexpr"] = new FryCircuit.UI.Rendering.Devices.Control.MathExpressionRenderer(),
        ["function"] = new FryCircuit.UI.Rendering.Devices.Control.MathExpressionRenderer(),
        ["deadzone"] = new FryCircuit.UI.Rendering.Devices.Control.DeadZoneRenderer(),
        ["relay"] = new FryCircuit.UI.Rendering.Devices.Control.RelayRenderer(),
        ["hysteresis"] = new FryCircuit.UI.Rendering.Devices.Control.RelayRenderer(),
        ["ratelimiter"] = new FryCircuit.UI.Rendering.Devices.Control.RateLimiterRenderer(),
        ["slew"] = new FryCircuit.UI.Rendering.Devices.Control.RateLimiterRenderer(),
        ["statespace"] = new FryCircuit.UI.Rendering.Devices.Control.StateSpaceBlockRenderer(),
        ["ss"] = new FryCircuit.UI.Rendering.Devices.Control.StateSpaceBlockRenderer(),
        ["dcmotor"] = new FryCircuit.UI.Rendering.Devices.Control.DcMotorPlantRenderer(),
        ["motorplant"] = new FryCircuit.UI.Rendering.Devices.Control.DcMotorPlantRenderer(),
        ["zoh"] = new FryCircuit.UI.Rendering.Devices.Control.ZeroOrderHoldRenderer(),
        ["samplehold"] = new FryCircuit.UI.Rendering.Devices.Control.ZeroOrderHoldRenderer(),
        ["unitdelay"] = new FryCircuit.UI.Rendering.Devices.Control.UnitDelayRenderer(),
        ["zdelay"] = new FryCircuit.UI.Rendering.Devices.Control.UnitDelayRenderer(),
        ["discretetf"] = new FryCircuit.UI.Rendering.Devices.Control.DiscreteTransferFunctionRenderer(),
        ["dtf"] = new FryCircuit.UI.Rendering.Devices.Control.DiscreteTransferFunctionRenderer(),
    };

    private static readonly IDeviceRenderer _fallbackRenderer = new ResistorRenderer();

    public static IDeviceRenderer GetRenderer(string deviceType)
    {
        string norm = (deviceType ?? string.Empty).Trim().ToLowerInvariant();
        return _renderers.TryGetValue(norm, out var renderer) ? renderer : _fallbackRenderer;
    }
}
