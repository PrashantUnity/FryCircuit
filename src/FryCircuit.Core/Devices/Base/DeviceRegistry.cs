using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Devices.Capacitor;
using FryCircuit.Core.Devices.CurrentSource;
using FryCircuit.Core.Devices.Diode;
using FryCircuit.Core.Devices.Display;
using FryCircuit.Core.Devices.Fuse;
using FryCircuit.Core.Devices.Ground;
using FryCircuit.Core.Devices.Inductor;
using FryCircuit.Core.Devices.Lamp;
using FryCircuit.Core.Devices.Logic;
using FryCircuit.Core.Devices.Meters;
using FryCircuit.Core.Devices.NetLabel;
using FryCircuit.Core.Devices.OpAmp;
using FryCircuit.Core.Devices.Potentiometer;
using FryCircuit.Core.Devices.Quantum;
using FryCircuit.Core.Devices.Resistor;
using FryCircuit.Core.Devices.Sensors;
using FryCircuit.Core.Devices.Switch;
using FryCircuit.Core.Devices.Timer;
using FryCircuit.Core.Devices.Transistor;
using FryCircuit.Core.Devices.TransmissionLine;
using FryCircuit.Core.Devices.VoltageSource;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Base;

public static class DeviceRegistry
{
    private static readonly Dictionary<string, IDeviceDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized = false;
    private static readonly object _lock = new();

    public static void Register(IDeviceDescriptor descriptor, params string[] aliases)
    {
        _descriptors[descriptor.TypeName] = descriptor;
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                _descriptors[alias.Trim()] = descriptor;
            }
        }
    }

    public static IDeviceDescriptor? TryGet(string typeName)
    {
        EnsureInitialized();
        string norm = (typeName ?? string.Empty).Trim().ToLowerInvariant();
        return _descriptors.TryGetValue(norm, out var desc) ? desc : null;
    }

    public static bool TryGet(string typeName, out IDeviceDescriptor? descriptor)
    {
        descriptor = TryGet(typeName);
        return descriptor != null;
    }

    public static IDeviceDescriptor Get(string typeName)
    {
        EnsureInitialized();
        var desc = TryGet(typeName);
        if (desc != null) return desc;

        throw new KeyNotFoundException($"No descriptor registered for device type '{typeName}'.");
    }

    public static IDeviceDescriptor? GetDescriptor(Device device)
    {
        return TryGet(device.Type);
    }

    public static Device CreateDevice(string typeName, int id = 0, double x = 0, double y = 0)
    {
        EnsureInitialized();
        string norm = (typeName ?? string.Empty).Trim().ToLowerInvariant();

        if (norm == "vsrcsine")
        {
            var vs = new VoltageSourceDevice(id, x, y, 5.0) { WaveformType = VoltageWaveformType.Sine };
            vs.SetupDefaultPins();
            return vs;
        }

        var desc = TryGet(norm);
        if (desc != null)
        {
            return desc.CreateInstance(id, x, y);
        }

        return new Device(id, norm, x, y);
    }

    public static string FormatLabel(Device device)
    {
        var desc = GetDescriptor(device);
        return desc != null ? desc.FormatLabel(device) : string.Empty;
    }

    public static DeviceParameterSpec? GetPrimaryParameter(Device device)
    {
        var desc = GetDescriptor(device);
        return desc?.PrimaryParameter;
    }

    public static IReadOnlyList<DeviceParameterSpec> GetParameters(Device device)
    {
        var desc = GetDescriptor(device);
        return desc != null ? desc.Parameters : Array.Empty<DeviceParameterSpec>();
    }

    public static bool HandleClick(Device device)
    {
        var desc = GetDescriptor(device);
        return desc != null && desc.HandleClick(device);
    }

    public static IReadOnlyCollection<IDeviceDescriptor> GetAllDescriptors()
    {
        EnsureInitialized();
        return _descriptors.Values.Distinct().ToList();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;
            RegisterDefaultDescriptors();
            _initialized = true;
        }
    }

    private static void RegisterDefaultDescriptors()
    {
        Register(new ResistorDescriptor(), "resistor", "r");
        Register(new CapacitorDescriptor(), "capacitor", "c");
        Register(new InductorDescriptor(), "inductor", "l");
        Register(new GroundDescriptor(), "ground", "gnd");
        Register(new VoltageSourceDescriptor(), "vsource", "vsrc", "vcc", "vdc");
        Register(new CurrentSourceDescriptor(), "csource", "isrc");
        Register(new DiodeDescriptor(), "diode", "d");
        Register(new ZenerDiodeDescriptor(), "zener");
        Register(new LedDescriptor(), "led");
        Register(new PotentiometerDescriptor(), "potentiometer", "pot");
        Register(new SwitchDescriptor(), "spst", "switch", "spstauto");
        Register(new SpdtSwitchDescriptor(), "spdt", "spdtauto");

        Register(new BjtDescriptor(), "npn");
        Register(new PnpBjtDescriptor(), "pnp");
        Register(new MosfetDescriptor(), "nmos");
        Register(new PmosDescriptor(), "pmos");

        Register(new OpAmpDescriptor(), "opamp");

        Register(new LogicGateDescriptor("and"), "and");
        Register(new LogicGateDescriptor("or"), "or");
        Register(new LogicGateDescriptor("not"), "not");
        Register(new LogicGateDescriptor("nand"), "nand");
        Register(new LogicGateDescriptor("nor"), "nor");
        Register(new LogicGateDescriptor("xor"), "xor");
        Register(new LogicGateDescriptor("xnor"), "xnor");

        Register(new Timer555Descriptor(), "timer555", "555");
        Register(new JosephsonJunctionDescriptor(), "josephson", "jj", "qubit");
        Register(new SquidDescriptor(), "squid");
        Register(new TunnelDiodeDescriptor(), "tunneldiode", "esaki");
        Register(new TransmissionLineDescriptor(), "tline", "transmissionline");
        Register(new CosmicIonPulseDescriptor(), "cosmicipulse", "ionpulse", "seu");
        Register(new HallEffectSensorDescriptor(), "hallsensor", "hall");
        Register(new BatteryCellDescriptor(), "battery", "cell", "lipo");
        Register(new NetLabelDescriptor(), "netlabel");
        Register(new VoltmeterDescriptor(), "voltmeter");
        Register(new AmmeterDescriptor(), "amperemeter", "ammeter");
        Register(new OhmmeterDescriptor(), "ohmmeter", "ohm");
        Register(new LampDescriptor(), "lamp");
        Register(new FuseDescriptor(), "fuse");
        Register(new SevenSegmentDisplayDescriptor(), "seg7", "7seg", "sevensegment", "display");
        Register(new BcdTo7SegmentDescriptor(), "bcd7seg", "7448", "7447");
        Register(new BinaryCounterDescriptor(), "counter4", "74193", "counter");

        // Drone & Aerospace Components
        Register(new FanDescriptor(), "fan", "dcfan", "cooler", "blower");
        Register(new BldcMotorDescriptor(), "bldc", "bldcmotor", "motor");
        Register(new EscDescriptor(), "esc", "bldcesc", "speedcontroller");
        Register(new BecDescriptor(), "bec", "buck", "dcdc");
        Register(new PiezoBuzzerDescriptor(), "buzzer", "piezo", "beeper");
        Register(new TvsDiodeDescriptor(), "tvs", "tvsdiode");

        // Control Systems & Feedback Engineering (Astrom & Murray CDS 101)
        Register(new FryCircuit.Core.Devices.Control.TransferFunctionDescriptor(), "tf", "transferfunction");
        Register(new FryCircuit.Core.Devices.Control.SummingJunctionDescriptor(), "sum", "comparator", "summingjunction");
        Register(new FryCircuit.Core.Devices.Control.PidControllerDescriptor(), "pid", "pidcontroller");
        Register(new FryCircuit.Core.Devices.Control.GainBlockDescriptor(), "gain", "multiplier");
        Register(new FryCircuit.Core.Devices.Control.StepSourceDescriptor(), "step", "stepsource", "stepgen");
        Register(new FryCircuit.Core.Devices.Control.IntegratorDescriptor(), "integrator", "integ");
        Register(new FryCircuit.Core.Devices.Control.DerivativeDescriptor(), "derivative", "deriv");
        Register(new FryCircuit.Core.Devices.Control.SaturationDescriptor(), "saturation", "limiter");
        Register(new FryCircuit.Core.Devices.Control.TimeDelayDescriptor(), "delay", "timedelay");

        // Universal Control & System Dynamics Extensions
        Register(new FryCircuit.Core.Devices.Control.RampSourceDescriptor(), "ramp", "rampsource", "rampgen");
        Register(new FryCircuit.Core.Devices.Control.ConstantSourceDescriptor(), "const", "constant", "setpoint");
        Register(new FryCircuit.Core.Devices.Control.NoiseSourceDescriptor(), "noise", "whitenoise", "noisegen");
        Register(new FryCircuit.Core.Devices.Control.PulseSourceDescriptor(), "pulse", "pulsesource", "pulsegen");
        Register(new FryCircuit.Core.Devices.Control.MultiplierDescriptor(), "mult", "prod", "product");
        Register(new FryCircuit.Core.Devices.Control.DividerDescriptor(), "div", "divider", "quotient");
        Register(new FryCircuit.Core.Devices.Control.MathExpressionDescriptor(), "mathexpr", "function", "mathfunc");
        Register(new FryCircuit.Core.Devices.Control.DeadZoneDescriptor(), "deadzone", "deadband");
        Register(new FryCircuit.Core.Devices.Control.RelayDescriptor(), "relay", "hysteresis");
        Register(new FryCircuit.Core.Devices.Control.RateLimiterDescriptor(), "ratelimiter", "slew", "slewrate");
        Register(new FryCircuit.Core.Devices.Control.StateSpaceBlockDescriptor(), "statespace", "ss", "statespacemodel");
        Register(new FryCircuit.Core.Devices.Control.DcMotorPlantDescriptor(), "dcmotor", "motorplant", "dcmotorplant");
        Register(new FryCircuit.Core.Devices.Control.ZeroOrderHoldDescriptor(), "zoh", "samplehold");
        Register(new FryCircuit.Core.Devices.Control.UnitDelayDescriptor(), "unitdelay", "zdelay", "zinv");
        Register(new FryCircuit.Core.Devices.Control.DiscreteTransferFunctionDescriptor(), "discretetf", "dtf", "ztrans");
    }
}
