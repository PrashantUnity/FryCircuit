using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.VoltageSource;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.IO;

/// <summary>
/// High-performance serializer and deserializer for the human- and LLM-optimized (.fcircuit) circuit format.
/// Supports named nets, semantic pin aliases, engineering units (e.g. 10k, 4.7uF, 1kHz),
/// automatic markdown code fence extraction, and optional auto-layout.
/// </summary>
public static class LlmCircuitSerializer
{
    public const string DefaultFileExtension = ".fcircuit";
    public const string SchemaUrl = "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Serializes a Circuit into the clean, LLM-friendly .fcircuit JSON format.
    /// </summary>
    public static string Serialize(Circuit circuit)
    {
        var root = new JsonObject
        {
            ["$schema"] = SchemaUrl,
            ["version"] = 1,
            ["name"] = circuit.Name,
            ["description"] = $"Schematic exported from FryCircuit CAD Studio containing {circuit.Devices.Count} components."
        };

        var optionsObj = new JsonObject
        {
            ["speed"] = circuit.Options.Speed,
            ["logicHigh"] = circuit.Options.LogicHigh
        };
        root["options"] = optionsObj;

        var frameObj = new JsonObject
        {
            ["width"] = circuit.Frame.XMax - circuit.Frame.XMin,
            ["height"] = circuit.Frame.YMax - circuit.Frame.YMin
        };
        root["frame"] = frameObj;

        // Build net name mapping from nodes
        var nodeToNetName = new Dictionary<int, string>();
        foreach (var node in circuit.Nodes.Values)
        {
            if (node.IsGround)
            {
                nodeToNetName[node.Id] = "gnd";
            }
            else
            {
                nodeToNetName[node.Id] = $"n{node.Id}";
            }
        }

        // Check if any NetLabel devices provide semantic names for nodes
        foreach (var dev in circuit.Devices)
        {
            if (dev is NetLabelDevice nl && nl.Pins.Count > 0 && !string.IsNullOrWhiteSpace(nl.NetName))
            {
                int nodeId = nl.Pins[0].NodeId;
                if (nodeId > 0 && nodeToNetName.ContainsKey(nodeId))
                {
                    nodeToNetName[nodeId] = nl.NetName.Trim();
                }
            }
        }

        var componentsArray = new JsonArray();
        int devIndex = 1;

        foreach (var dev in circuit.Devices)
        {
            var compObj = new JsonObject
            {
                ["id"] = FormatComponentId(dev, devIndex++),
                ["type"] = dev.Type
            };

            // Values / parameters
            var primaryParam = DeviceRegistry.GetPrimaryParameter(dev);
            if (primaryParam != null)
            {
                double val = primaryParam.Getter(dev);
                compObj["value"] = FormatEngineeringValue(val, primaryParam.Unit);
            }

            // Additional parameters
            var paramSpecs = DeviceRegistry.GetParameters(dev);
            if (paramSpecs.Count > 1 || dev is VoltageSourceDevice)
            {
                var paramsObj = new JsonObject();
                if (dev is VoltageSourceDevice vs)
                {
                    paramsObj["waveform"] = vs.WaveformType.ToString().ToLowerInvariant();
                    paramsObj["amplitude"] = FormatEngineeringValue(vs.Amplitude, "V");
                    paramsObj["frequency"] = FormatEngineeringValue(vs.Frequency, "Hz");
                    if (Math.Abs(vs.Offset) > 1e-9)
                    {
                        paramsObj["offset"] = FormatEngineeringValue(vs.Offset, "V");
                    }
                }

                foreach (var spec in paramSpecs)
                {
                    if (spec.ParameterName == primaryParam?.ParameterName) continue;
                    double val = spec.Getter(dev);
                    if (Math.Abs(val - spec.DefaultValue) > 1e-9)
                    {
                        paramsObj[spec.ParameterName.ToLowerInvariant()] = FormatEngineeringValue(val, spec.Unit);
                    }
                }

                if (paramsObj.Count > 0)
                {
                    compObj["parameters"] = paramsObj;
                }
            }

            // Coordinates
            compObj["x"] = Math.Round(dev.LocationX, 2);
            compObj["y"] = Math.Round(dev.LocationY, 2);
            if (dev.Transform.Rotation != DeviceRotation.Up)
            {
                compObj["rotation"] = DeviceTransform.RotationToString(dev.Transform.Rotation);
            }
            if (dev.Transform.Flip)
            {
                compObj["flip"] = true;
            }

            // Pin connectivity
            var desc = DeviceRegistry.GetDescriptor(dev);
            if (dev.Pins.Count == 2 && desc != null && desc.Pins.Count == 2 &&
                (dev.Type == "resistor" || dev.Type == "capacitor" || dev.Type == "inductor"))
            {
                // Simple 2-terminal passives can use "nodes": ["n1", "n2"]
                var nodesArr = new JsonArray
                {
                    GetNetName(dev.Pins[0].NodeId, nodeToNetName),
                    GetNetName(dev.Pins[1].NodeId, nodeToNetName)
                };
                compObj["nodes"] = nodesArr;
            }
            else
            {
                // Semantic pin dictionary
                var pinsObj = new JsonObject();
                for (int i = 0; i < dev.Pins.Count; i++)
                {
                    int nId = dev.Pins[i].NodeId;
                    string pinName = GetSemanticPinName(desc, i);
                    pinsObj[pinName] = GetNetName(nId, nodeToNetName);
                }
                compObj["pins"] = pinsObj;
            }

            componentsArray.Add(compObj);
        }

        root["components"] = componentsArray;

        return root.ToJsonString(JsonOptions);
    }

    /// <summary>
    /// Deserializes a Circuit from JSON, Markdown code block, or .fcircuit text.
    /// Resiliently strips markdown fences and auto-layouts if coordinates are missing.
    /// </summary>
    public static Circuit Deserialize(string text, string defaultName = "LLM Generated Circuit")
    {
        string cleanJson = ExtractJsonContent(text);

        using var doc = JsonDocument.Parse(cleanJson);
        var root = doc.RootElement;

        var circuit = new Circuit(defaultName);

        if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            string n = nameProp.GetString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(n))
            {
                circuit.Name = n;
            }
        }

        if (root.TryGetProperty("options", out var optProp) && optProp.ValueKind == JsonValueKind.Object)
        {
            if (optProp.TryGetProperty("speed", out var spd) && spd.TryGetDouble(out double speedVal))
            {
                circuit.Options.Speed = Math.Clamp(speedVal, 0.1, 10.0);
            }
            if (optProp.TryGetProperty("logicHigh", out var lh) && lh.TryGetDouble(out double lhVal))
            {
                circuit.Options.LogicHigh = Math.Clamp(lhVal, 1.0, 15.0);
            }
        }

        var netNameToNodeId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = 0,
            ["gnd"] = 0,
            ["ground"] = 0,
            ["earth"] = 0,
            ["vss"] = 0
        };
        circuit.GetOrCreateNode(0, isGround: true);

        int nextNodeId = 1;
        int GetOrCreateNodeId(string netName)
        {
            netName = netName.Trim();
            if (string.IsNullOrEmpty(netName))
            {
                netName = $"n{nextNodeId}";
            }

            if (netNameToNodeId.TryGetValue(netName, out int existingId))
            {
                return existingId;
            }

            bool isGnd = IsGroundNetName(netName);
            int newId = isGnd ? 0 : nextNodeId++;
            if (!isGnd)
            {
                netNameToNodeId[netName] = newId;
            }
            var cNode = circuit.GetOrCreateNode(newId, isGround: isGnd);
            if (!string.IsNullOrEmpty(netName) && string.IsNullOrEmpty(cNode.Name))
            {
                cNode.Name = isGnd ? "GND" : netName;
            }
            return newId;
        }

        JsonElement compArrayElem = default;
        if (root.TryGetProperty("components", out var cElem) && cElem.ValueKind == JsonValueKind.Array)
        {
            compArrayElem = cElem;
        }
        else if (root.TryGetProperty("devices", out var dElem) && dElem.ValueKind == JsonValueKind.Array)
        {
            compArrayElem = dElem;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            compArrayElem = root;
        }

        if (compArrayElem.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Invalid .fcircuit JSON: Missing 'components' array.");
        }

        int devIdCounter = 1;
        bool hasExplicitCoordinates = false;

        foreach (var comp in compArrayElem.EnumerateArray())
        {
            string type = "resistor";
            if (comp.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                type = NormalizeDeviceType(typeProp.GetString() ?? "resistor");
            }

            int id = devIdCounter;
            string? explicitLabel = null;
            if (comp.TryGetProperty("id", out var idProp))
            {
                if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out int nid))
                {
                    id = nid;
                    explicitLabel = id.ToString();
                }
                else if (idProp.ValueKind == JsonValueKind.String)
                {
                    string idStr = idProp.GetString() ?? string.Empty;
                    explicitLabel = idStr;
                    // Extract numeric suffix if possible (e.g. "R12" -> 12)
                    string digits = new string(idStr.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out int parsedId) && parsedId > 0)
                    {
                        id = parsedId;
                    }
                }
            }
            if (comp.TryGetProperty("label", out var labelProp) && labelProp.ValueKind == JsonValueKind.String)
            {
                explicitLabel = labelProp.GetString();
            }
            if (id <= 0) id = devIdCounter;
            devIdCounter = Math.Max(devIdCounter + 1, id + 1);

            double x = 0.0;
            double y = 0.0;
            if (comp.TryGetProperty("x", out var xProp))
            {
                x = ParseNumber(xProp);
                hasExplicitCoordinates = true;
            }
            if (comp.TryGetProperty("y", out var yProp))
            {
                y = ParseNumber(yProp);
                hasExplicitCoordinates = true;
            }

            var device = DeviceRegistry.CreateDevice(type, id, x, y);
            if (!string.IsNullOrEmpty(explicitLabel))
            {
                device.Label = explicitLabel;
            }

            // Rotation & Flip
            if (comp.TryGetProperty("rotation", out var rotProp))
            {
                device.Transform.Rotation = ParseRotation(rotProp);
            }
            if (comp.TryGetProperty("flip", out var flipProp))
            {
                device.Transform.Flip = flipProp.ValueKind == JsonValueKind.True;
            }

            // Apply Primary Value if specified
            if (comp.TryGetProperty("value", out var valProp))
            {
                double parsedVal = ParseEngineeringValue(valProp);
                ApplyPrimaryValue(device, parsedVal);
            }

            // Apply parameters object or direct properties
            ApplyParameters(device, comp);

            // Connect Pins
            var desc = DeviceRegistry.GetDescriptor(device);
            device.SetupDefaultPins();

            if (comp.TryGetProperty("nodes", out var nodesProp) && nodesProp.ValueKind == JsonValueKind.Array)
            {
                // Positional nodes array: ["vin", "vout"]
                int pIndex = 0;
                foreach (var netElem in nodesProp.EnumerateArray())
                {
                    if (pIndex < device.Pins.Count)
                    {
                        string netName = netElem.GetString() ?? string.Empty;
                        device.Pins[pIndex].NodeId = GetOrCreateNodeId(netName);
                        pIndex++;
                    }
                }
            }
            else if (comp.TryGetProperty("pins", out var pinsProp) && pinsProp.ValueKind == JsonValueKind.Object)
            {
                // Pin dictionary: {"anode": "vin", "cathode": "gnd"} or {"in-": "inv", "in+": "gnd"}
                foreach (var pinKvp in pinsProp.EnumerateObject())
                {
                    string pinSpec = pinKvp.Name;
                    string netName = pinKvp.Value.GetString() ?? string.Empty;
                    int pinIndex = ResolvePinIndex(desc, device, pinSpec);
                    if (pinIndex >= 0 && pinIndex < device.Pins.Count)
                    {
                        device.Pins[pinIndex].NodeId = GetOrCreateNodeId(netName);
                    }
                }
            }

            circuit.AddDevice(device);
        }

        // Auto-layout if schematic coordinates were completely omitted
        if (!hasExplicitCoordinates)
        {
            CircuitAutoLayoutEngine.Layout(circuit);
        }

        // Ensure minimum bounding frame
        double minX = circuit.Devices.Count > 0 ? circuit.Devices.Min(d => d.LocationX) - 3 : 0;
        double minY = circuit.Devices.Count > 0 ? circuit.Devices.Min(d => d.LocationY) - 3 : 0;
        double maxX = circuit.Devices.Count > 0 ? circuit.Devices.Max(d => d.LocationX) + 3 : 20;
        double maxY = circuit.Devices.Count > 0 ? circuit.Devices.Max(d => d.LocationY) + 3 : 20;
        circuit.Frame = new CircuitFrame(Math.Min(0, minX), Math.Min(0, minY), Math.Max(20, maxX), Math.Max(20, maxY));

        // Synthesize orthogonal wires connecting the common nodes
        circuit.EnsureWiresSynthesized();

        return circuit;
    }

    /// <summary>
    /// Resiliently extracts the raw JSON text from an LLM response that might contain
    /// conversational text, markdown code blocks (```json ... ```), or whitespace.
    /// </summary>
    public static string ExtractJsonContent(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "{}";
        string text = input.Trim();

        // Search for markdown code fences ```json ... ``` or ``` ... ```
        int fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            int lineEnd = text.IndexOf('\n', fenceStart);
            if (lineEnd > fenceStart)
            {
                int fenceEnd = text.IndexOf("```", lineEnd, StringComparison.Ordinal);
                if (fenceEnd > lineEnd)
                {
                    return text[lineEnd..fenceEnd].Trim();
                }
            }
        }

        // Otherwise find outermost matching curly braces '{' ... '}'
        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        // Or square brackets '[' ... ']'
        int firstBracket = text.IndexOf('[');
        int lastBracket = text.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
        {
            return text.Substring(firstBracket, lastBracket - firstBracket + 1);
        }

        return text;
    }

    /// <summary>
    /// Parses engineering values such as "10k", "4.7uF", "100n", "1MHz", "5V", "2.5ms", or numeric tokens.
    /// </summary>
    public static double ParseEngineeringValue(string str, double defaultValue = 0.0)
    {
        if (string.IsNullOrWhiteSpace(str)) return defaultValue;
        str = str.Trim();

        // Check for scientific / plain double first
        if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
        {
            return plain;
        }

        string s = str.Trim();
        if (s.EndsWith('%'))
        {
            if (double.TryParse(s[..^1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                return pct * 0.01;
            }
        }

        // Strip trailing units: OHM, OHMS, HZ, SEC, V, F, H, A, S (case-insensitive)
        string[] unitSuffixes = ["OHMS", "OHM", "HZ", "SEC", "DEG", "V", "F", "H", "A", "S", "Ω"];
        foreach (var u in unitSuffixes)
        {
            if (s.EndsWith(u, StringComparison.OrdinalIgnoreCase) && s.Length > u.Length)
            {
                s = s[..^u.Length].Trim();
                break;
            }
        }

        // Metric engineering prefixes
        double multiplier = 1.0;
        int prefixLen = 0;

        if (s.EndsWith("MEG", StringComparison.OrdinalIgnoreCase) || s.EndsWith("MEGA", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1e6;
            prefixLen = s.EndsWith("MEGA", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
        }
        else if (s.EndsWith("PICO", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-12; prefixLen = 4; }
        else if (s.EndsWith("NANO", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-9; prefixLen = 4; }
        else if (s.EndsWith("MICRO", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-6; prefixLen = 5; }
        else if (s.EndsWith("MILLI", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-3; prefixLen = 5; }
        else if (s.EndsWith("KILO", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e3; prefixLen = 4; }
        else if (s.EndsWith("GIGA", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e9; prefixLen = 4; }
        else if (s.EndsWith("TERA", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e12; prefixLen = 4; }
        else if (s.EndsWith("p", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-12; prefixLen = 1; }
        else if (s.EndsWith("n", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e-9; prefixLen = 1; }
        else if (s.EndsWith("u", StringComparison.OrdinalIgnoreCase) || s.EndsWith("µ")) { multiplier = 1e-6; prefixLen = 1; }
        else if (s.EndsWith('m')) { multiplier = 1e-3; prefixLen = 1; }
        else if (s.EndsWith('M')) { multiplier = 1e6; prefixLen = 1; }
        else if (s.EndsWith("k", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e3; prefixLen = 1; }
        else if (s.EndsWith("g", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e9; prefixLen = 1; }
        else if (s.EndsWith("t", StringComparison.OrdinalIgnoreCase)) { multiplier = 1e12; prefixLen = 1; }

        string numPart = prefixLen > 0 ? s[..^prefixLen].Trim() : s;
        if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            return val * multiplier;
        }

        return defaultValue;
    }

    public static double ParseEngineeringValue(JsonElement elem, double defaultValue = 0.0)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out double num))
        {
            return num;
        }
        if (elem.ValueKind == JsonValueKind.String)
        {
            return ParseEngineeringValue(elem.GetString() ?? string.Empty, defaultValue);
        }
        return defaultValue;
    }

    /// <summary>
    /// Formats a value using compact engineering notation (e.g. 10k, 4.7u, 100n, 5).
    /// </summary>
    public static string FormatEngineeringValue(double val, string unit = "")
    {
        if (double.IsNaN(val) || double.IsInfinity(val)) return "0";
        double abs = Math.Abs(val);
        if (abs < 1e-15) return "0";

        string prefix = "";
        double scaled = val;

        if (abs >= 1e12) { scaled = val / 1e12; prefix = "T"; }
        else if (abs >= 1e9) { scaled = val / 1e9; prefix = "G"; }
        else if (abs >= 1e6) { scaled = val / 1e6; prefix = "M"; }
        else if (abs >= 1e3) { scaled = val / 1e3; prefix = "k"; }
        else if (abs >= 1.0) { scaled = val; prefix = ""; }
        else if (abs >= 1e-3) { scaled = val * 1e3; prefix = "m"; }
        else if (abs >= 1e-6) { scaled = val * 1e6; prefix = "u"; }
        else if (abs >= 1e-9) { scaled = val * 1e9; prefix = "n"; }
        else { scaled = val * 1e12; prefix = "p"; }

        string formatted = scaled.ToString("G4", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unit) ? $"{formatted}{prefix}" : $"{formatted}{prefix}{unit}";
    }

    /// <summary>
    /// Resolves semantic pin aliases (e.g. "in-", "anode", "collector", "+") to the correct 0-based pin index.
    /// </summary>
    public static int ResolvePinIndex(IDeviceDescriptor? desc, Device dev, string pinSpec)
    {
        pinSpec = pinSpec.Trim().ToLowerInvariant();

        // 1. Direct integer index
        if (int.TryParse(pinSpec, out int directIdx) && directIdx >= 0 && directIdx < dev.Pins.Count)
        {
            return directIdx;
        }

        // 2. Check descriptor pin names
        if (desc != null)
        {
            for (int i = 0; i < desc.Pins.Count; i++)
            {
                string pName = desc.Pins[i].Name.ToLowerInvariant();
                if (pName == pinSpec) return i;

                // Strip punctuation and check
                string cleanDescName = new string(pName.Where(char.IsLetterOrDigit).ToArray());
                string cleanSpec = new string(pinSpec.Where(char.IsLetterOrDigit).ToArray());
                if (!string.IsNullOrEmpty(cleanDescName) && cleanDescName == cleanSpec) return i;
            }
        }

        // 3. Domain-specific pin aliases
        string type = dev.Type.ToLowerInvariant();

        // Op-Amps
        if (type == "opamp")
        {
            if (pinSpec is "in-" or "inv" or "inverting" or "-" or "minus" or "neg" or "in1") return 0;
            if (pinSpec is "in+" or "noninv" or "noninverting" or "+" or "plus" or "pos" or "in2") return 1;
            if (pinSpec is "out" or "output" or "o" or "out1") return 2;
        }

        // Diodes / LEDs / Zeners
        if (type is "diode" or "led" or "zener")
        {
            if (pinSpec is "anode" or "a" or "+" or "pos" or "p" or "in") return 0;
            if (pinSpec is "cathode" or "k" or "cath" or "-" or "neg" or "n" or "out") return 1;
        }

        // BJT Transistors (NPN / PNP)
        if (type is "npn" or "pnp")
        {
            if (pinSpec is "base" or "b") return 0;
            if (pinSpec is "collector" or "c") return 1;
            if (pinSpec is "emitter" or "e") return 2;
        }

        // MOSFETs (NMOS / PMOS)
        if (type is "nmos" or "pmos")
        {
            if (pinSpec is "gate" or "g") return 0;
            if (pinSpec is "drain" or "d") return 1;
            if (pinSpec is "source" or "s") return 2;
            if (pinSpec is "bulk" or "body" or "substrate" or "b") return 3;
        }

        // Sources & Batteries
        if (type is "vsource" or "battery" or "csource")
        {
            if (pinSpec is "+" or "pos" or "positive" or "p" or "v+" or "in") return 0;
            if (pinSpec is "-" or "neg" or "negative" or "n" or "v-" or "gnd" or "out") return 1;
        }

        // Potentiometer
        if (type == "potentiometer")
        {
            if (pinSpec is "t1" or "terminal1" or "in" or "a") return 0;
            if (pinSpec is "wiper" or "w" or "out" or "tap") return 1;
            if (pinSpec is "t2" or "terminal2" or "gnd" or "b") return 2;
        }

        // 555 Timer
        if (type is "timer555" or "555")
        {
            if (pinSpec is "gnd" or "ground") return 0;
            if (pinSpec is "trig" or "trigger") return 1;
            if (pinSpec is "out" or "output") return 2;
            if (pinSpec is "reset" or "rst") return 3;
            if (pinSpec is "ctrl" or "control" or "cv") return 4;
            if (pinSpec is "thresh" or "threshold") return 5;
            if (pinSpec is "disch" or "discharge") return 6;
            if (pinSpec is "vcc" or "power") return 7;
        }

        // Logic Gates
        if (type is "and" or "or" or "nand" or "nor" or "xor" or "xnor")
        {
            if (pinSpec is "in1" or "a" or "in") return 0;
            if (pinSpec is "in2" or "b") return 1;
            if (pinSpec is "out" or "y" or "q") return 2;
        }

        // Inverter / NOT gate
        if (type == "not")
        {
            if (pinSpec is "in" or "a") return 0;
            if (pinSpec is "out" or "y" or "q") return 1;
        }

        // Default 2-terminal fallback
        if (dev.Pins.Count >= 2)
        {
            if (pinSpec is "+" or "in" or "p" or "1") return 0;
            if (pinSpec is "-" or "out" or "n" or "2") return 1;
        }

        return 0;
    }

    private static void ApplyPrimaryValue(Device device, double val)
    {
        switch (device)
        {
            case ResistorDevice r:
                r.Resistance = Math.Max(0.001, val);
                break;
            case CapacitorDevice c:
                c.Capacitance = Math.Max(1e-15, val);
                break;
            case InductorDevice l:
                l.Inductance = Math.Max(1e-12, val);
                break;
            case VoltageSourceDevice vs:
                vs.Voltage = val;
                vs.Amplitude = val;
                break;
            case CurrentSourceDevice cs:
                cs.Current = val;
                break;
            case ZenerDiodeDevice zd:
                zd.BreakdownVoltage = Math.Max(0.1, val);
                break;
            case PotentiometerDevice p:
                p.Resistance = Math.Max(1.0, val);
                break;
            default:
                var primary = DeviceRegistry.GetPrimaryParameter(device);
                primary?.Setter(device, val);
                break;
        }
    }

    private static void ApplyParameters(Device device, JsonElement comp)
    {
        // Check for "parameters" object or inline properties
        if (comp.TryGetProperty("parameters", out var paramsElem) && paramsElem.ValueKind == JsonValueKind.Object)
        {
            ApplyParameterObject(device, paramsElem);
        }

        // Also check inline properties like "resistance", "capacitance", "voltage", "waveform", "frequency", "amplitude", "offset"
        if (device is VoltageSourceDevice vs)
        {
            if (comp.TryGetProperty("waveform", out var wfProp))
            {
                string wf = wfProp.GetString()?.ToLowerInvariant() ?? "";
                vs.WaveformType = wf switch
                {
                    "sine" or "sin" => VoltageWaveformType.Sine,
                    "pulse" or "square" or "sqr" or "sawtooth" or "saw" => VoltageWaveformType.Pulse,
                    "triangle" or "tri" => VoltageWaveformType.Triangle,
                    _ => VoltageWaveformType.Dc
                };
            }
            if (comp.TryGetProperty("frequency", out var freqProp)) vs.Frequency = ParseEngineeringValue(freqProp, 1000.0);
            if (comp.TryGetProperty("amplitude", out var ampProp)) vs.Amplitude = ParseEngineeringValue(ampProp, 5.0);
            if (comp.TryGetProperty("offset", out var offProp)) vs.Offset = ParseEngineeringValue(offProp, 0.0);
        }

        if (comp.TryGetProperty("resistance", out var resProp)) ApplyPrimaryValue(device, ParseEngineeringValue(resProp));
        if (comp.TryGetProperty("capacitance", out var capProp)) ApplyPrimaryValue(device, ParseEngineeringValue(capProp));
        if (comp.TryGetProperty("inductance", out var indProp)) ApplyPrimaryValue(device, ParseEngineeringValue(indProp));
        if (comp.TryGetProperty("voltage", out var voltProp)) ApplyPrimaryValue(device, ParseEngineeringValue(voltProp));
        if (comp.TryGetProperty("current", out var curProp)) ApplyPrimaryValue(device, ParseEngineeringValue(curProp));
    }

    private static void ApplyParameterObject(Device device, JsonElement paramsElem)
    {
        foreach (var prop in paramsElem.EnumerateObject())
        {
            string key = prop.Name.ToLowerInvariant();
            if (device is VoltageSourceDevice vs && key == "waveform")
            {
                string wf = prop.Value.GetString()?.ToLowerInvariant() ?? "";
                vs.WaveformType = wf switch
                {
                    "sine" or "sin" => VoltageWaveformType.Sine,
                    "pulse" or "square" or "sqr" or "sawtooth" or "saw" => VoltageWaveformType.Pulse,
                    "triangle" or "tri" => VoltageWaveformType.Triangle,
                    _ => VoltageWaveformType.Dc
                };
                continue;
            }

            double val = ParseEngineeringValue(prop.Value);
            device.SetParameter(key, val);
        }
    }

    private static string NormalizeDeviceType(string type)
    {
        string t = type.Trim().ToLowerInvariant();
        return t switch
        {
            "r" or "res" => "resistor",
            "c" or "cap" => "capacitor",
            "l" or "ind" => "inductor",
            "d" => "diode",
            "gnd" => "ground",
            "vsrc" or "v" or "voltage_source" => "vsource",
            "isrc" or "i" or "current_source" => "csource",
            "pot" => "potentiometer",
            "sw" or "spstauto" => "spst",
            "555" or "ne555" => "timer555",
            "bjt" or "bjt_npn" => "npn",
            "bjt_pnp" => "pnp",
            "mosfet_nmos" => "nmos",
            "mosfet_pmos" => "pmos",
            "op_amp" or "amp" => "opamp",
            "voltmeter" or "meter_v" => "voltmeter",
            "ammeter" or "meter_a" => "amperemeter",
            _ => t
        };
    }

    private static DeviceRotation ParseRotation(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int rotDeg))
        {
            return rotDeg switch
            {
                90 => DeviceRotation.Right,
                180 => DeviceRotation.Down,
                270 => DeviceRotation.Left,
                _ => DeviceRotation.Up
            };
        }
        if (elem.ValueKind == JsonValueKind.String)
        {
            string s = elem.GetString()?.ToLowerInvariant() ?? "";
            return s switch
            {
                "right" or "90" or "h" or "horizontal" => DeviceRotation.Right,
                "down" or "180" => DeviceRotation.Down,
                "left" or "270" => DeviceRotation.Left,
                _ => DeviceRotation.Up
            };
        }
        return DeviceRotation.Up;
    }

    private static double ParseNumber(JsonElement elem, double defaultValue = 0.0)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out double d)) return d;
        if (elem.ValueKind == JsonValueKind.String && double.TryParse(elem.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
        return defaultValue;
    }

    private static bool IsGroundNetName(string name)
    {
        string n = name.Trim().ToLowerInvariant();
        return n is "0" or "gnd" or "ground" or "earth" or "vss" or "rail_gnd";
    }

    private static string GetNetName(int nodeId, Dictionary<int, string> nodeMap)
    {
        if (nodeId == 0) return "gnd";
        return nodeMap.TryGetValue(nodeId, out var name) ? name : $"n{nodeId}";
    }

    private static string GetSemanticPinName(IDeviceDescriptor? desc, int pinIndex)
    {
        if (desc != null && pinIndex < desc.Pins.Count)
        {
            string raw = desc.Pins[pinIndex].Name;
            // Clean up: "Inverting (-)" -> "in-", "NonInverting (+)" -> "in+", "Output" -> "out"
            string lower = raw.ToLowerInvariant();
            if (lower.Contains("inverting") && lower.Contains("-")) return "in-";
            if (lower.Contains("noninverting") && lower.Contains("+")) return "in+";
            if (lower == "output") return "out";
            return raw.ToLowerInvariant();
        }
        return (pinIndex + 1).ToString();
    }

    private static string FormatComponentId(Device dev, int index)
    {
        char prefix = dev.Type switch
        {
            "resistor" => 'R',
            "capacitor" => 'C',
            "inductor" => 'L',
            "diode" or "led" or "zener" => 'D',
            "vsource" => 'V',
            "csource" => 'I',
            "npn" or "pnp" => 'Q',
            "nmos" or "pmos" => 'M',
            "opamp" => 'U',
            "timer555" => 'U',
            "ground" => 'G',
            _ => 'X'
        };
        return $"{prefix}{dev.Id}";
    }
}
