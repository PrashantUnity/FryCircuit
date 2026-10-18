using FryCircuit.UI.Rendering.Devices.Aerospace;
using FryCircuit.UI.Rendering.Devices.Battery;
using FryCircuit.UI.Rendering.Devices.Capacitor;
using FryCircuit.UI.Rendering.Devices.CurrentSource;
using FryCircuit.UI.Rendering.Devices.Diode;
using FryCircuit.UI.Rendering.Devices.Display;
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
    };

    private static readonly IDeviceRenderer _fallbackRenderer = new ResistorRenderer();

    public static IDeviceRenderer GetRenderer(string deviceType)
    {
        string norm = (deviceType ?? string.Empty).Trim().ToLowerInvariant();
        return _renderers.TryGetValue(norm, out var renderer) ? renderer : _fallbackRenderer;
    }
}
