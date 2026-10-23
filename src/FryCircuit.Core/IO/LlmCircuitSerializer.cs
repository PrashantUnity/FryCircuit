using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.Control;
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

            if (dev is TransferFunctionDevice tfDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["numerator"] = tfDev.NumeratorString;
                paramsObj["denominator"] = tfDev.DenominatorString;
                compObj["parameters"] = paramsObj;
            }
            else if (dev is SummingJunctionDevice sumDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["signs"] = sumDev.Signs;
                compObj["parameters"] = paramsObj;
            }
            else if (dev is StateSpaceBlockDevice ssDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["a"] = ssDev.AString;
                paramsObj["b"] = ssDev.BString;
                paramsObj["c"] = ssDev.CString;
                paramsObj["d"] = ssDev.DString;
                compObj["parameters"] = paramsObj;
            }
            else if (dev is MathExpressionDevice mathDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["expression"] = mathDev.Expression;
                compObj["parameters"] = paramsObj;
            }
            else if (dev is DiscreteTransferFunctionDevice dtfDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["numerator"] = dtfDev.NumeratorString;
                paramsObj["denominator"] = dtfDev.DenominatorString;
                paramsObj["sample_time"] = dtfDev.SampleTime;
                compObj["parameters"] = paramsObj;
            }
            else if (dev is DcMotorPlantDevice motorDev)
            {
                var paramsObj = compObj["parameters"] as JsonObject ?? new JsonObject();
                paramsObj["ra"] = FormatEngineeringValue(motorDev.ResistanceRa, "Ω");
                paramsObj["la"] = FormatEngineeringValue(motorDev.InductanceLa, "H");
                paramsObj["kt"] = FormatEngineeringValue(motorDev.TorqueConstantKt, "N·m/A");
                paramsObj["ke"] = FormatEngineeringValue(motorDev.BackEmfConstantKe, "V/(rad/s)");
                paramsObj["j"] = FormatEngineeringValue(motorDev.RotorInertiaJ, "kg·m²");
                paramsObj["b"] = FormatEngineeringValue(motorDev.ViscousDampingB, "N·m·s");
                compObj["parameters"] = paramsObj;
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

        // Strip trailing units (case-insensitive) - longest composite first
        string[] unitSuffixes = [
            "V/(RAD/S)", "V/RAD/S", "N·M/A", "N*M/A", "N·M·S", "N*M*S", "KG·M²", "KG*M^2", "RAD/S",
            "OHMS", "OHM", "RPM", "N·M", "N*M", "HZ", "SEC", "DEG", "V", "F", "H", "A", "S", "Ω"
        ];
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

        // Control Blocks (tf, sum, pid, gain, step, integrator, derivative, saturation, delay)
        if (type is "tf" or "transferfunction" or "gain" or "integrator" or "derivative" or "saturation" or "delay")
        {
            if (pinSpec is "in" or "input" or "u" or "r" or "in1" or "sig_in") return 0;
            if (pinSpec is "out" or "output" or "y" or "out1" or "sig_out") return 1;
            if (pinSpec is "ref" or "gnd" or "ground" or "common" or "0") return 2;
        }

        if (type is "sum" or "summingjunction" or "comparator")
        {
            if (pinSpec is "in1" or "ina" or "in_a" or "inputa" or "input_a" or "r" or "+" or "pos") return 0;
            if (pinSpec is "in2" or "inb" or "in_b" or "inputb" or "input_b" or "fb" or "feedback" or "-" or "neg") return 1;
            if (pinSpec is "in3" or "inc" or "in_c" or "inputc" or "input_c") return 2;
            if (pinSpec is "out" or "output" or "err" or "error" or "e" or "sum" or "diff") return 3;
            if (pinSpec is "ref" or "gnd" or "ground" or "common" or "0") return 4;
        }

        if (type is "pid" or "pidcontroller")
        {
            if (pinSpec is "err" or "error" or "in" or "input" or "e") return 0;
            if (pinSpec is "out" or "output" or "u" or "ctrl" or "control") return 1;
            if (pinSpec is "ref" or "gnd" or "ground" or "common") return 2;
        }

        if (type is "step" or "stepsource" or "ramp" or "rampsource" or "const" or "constant" or "noise" or "pulse")
        {
            if (pinSpec is "out" or "output" or "+" or "pos" or "step" or "sig") return 0;
            if (pinSpec is "ref" or "gnd" or "ground" or "-" or "neg") return 1;
        }

        if (type is "mult" or "multiplier" or "prod" or "product")
        {
            if (pinSpec is "in1" or "ina" or "in_a" or "inputa" or "input1" or "a") return 0;
            if (pinSpec is "in2" or "inb" or "in_b" or "inputb" or "input2" or "b") return 1;
            if (pinSpec is "out" or "output" or "prod" or "y") return 2;
            if (pinSpec is "ref" or "gnd" or "ground" or "common") return 3;
        }

        if (type is "div" or "divider")
        {
            if (pinSpec is "num" or "numerator" or "in1" or "ina" or "a") return 0;
            if (pinSpec is "den" or "denominator" or "in2" or "inb" or "b") return 1;
            if (pinSpec is "out" or "output" or "quot" or "y") return 2;
            if (pinSpec is "ref" or "gnd" or "ground" or "common") return 3;
        }

        if (type is "statespace" or "ss" or "discretetf" or "dtf" or "zoh" or "unitdelay" or "deadzone" or "relay" or "ratelimiter" or "mathexpr")
        {
            if (pinSpec is "in" or "input" or "u" or "x" or "in1") return 0;
            if (pinSpec is "out" or "output" or "y" or "out1") return 1;
            if (pinSpec is "ref" or "gnd" or "ground" or "common") return 2;
        }

        if (type is "dcmotor" or "motorplant")
        {
            if (pinSpec is "arm+" or "armature+" or "in+" or "pos" or "va+" or "+") return 0;
            if (pinSpec is "arm-" or "armature-" or "in-" or "neg" or "va-" or "-") return 1;
            if (pinSpec is "tl" or "load" or "torque" or "t_load") return 2;
            if (pinSpec is "w" or "omega" or "speed" or "rpm" or "rad_s") return 3;
            if (pinSpec is "theta" or "angle" or "pos_out" or "position") return 4;
            if (pinSpec is "ref" or "gnd" or "ground" or "mech_gnd") return 5;
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

        if (device is TransferFunctionDevice tfComp)
        {
            if (comp.TryGetProperty("numerator", out var numProp))
                tfComp.NumeratorString = numProp.GetString() ?? numProp.GetRawText();
            if (comp.TryGetProperty("denominator", out var denProp))
                tfComp.DenominatorString = denProp.GetString() ?? denProp.GetRawText();
        }
        else if (device is SummingJunctionDevice sumComp)
        {
            if (comp.TryGetProperty("signs", out var signsProp))
                sumComp.Signs = signsProp.GetString() ?? "+-";
        }
        else if (device is StepSourceDevice stepComp)
        {
            if (comp.TryGetProperty("step_time", out var stProp)) stepComp.StepTime = ParseEngineeringValue(stProp, 0.01);
            if (comp.TryGetProperty("initial_value", out var ivProp) || comp.TryGetProperty("init_val", out ivProp)) stepComp.InitialValue = ParseEngineeringValue(ivProp, 0.0);
            if (comp.TryGetProperty("final_value", out var fvProp) || comp.TryGetProperty("final_val", out fvProp)) stepComp.FinalValue = ParseEngineeringValue(fvProp, 1.0);
        }
        else if (device is PidControllerDevice pidComp)
        {
            if (comp.TryGetProperty("kp", out var kpProp)) pidComp.Kp = ParseEngineeringValue(kpProp, 1.0);
            if (comp.TryGetProperty("ki", out var kiProp)) pidComp.Ki = ParseEngineeringValue(kiProp, 0.0);
            if (comp.TryGetProperty("kd", out var kdProp)) pidComp.Kd = ParseEngineeringValue(kdProp, 0.0);
        }
        else if (device is GainBlockDevice gainComp)
        {
            if (comp.TryGetProperty("gain", out var gProp)) gainComp.Gain = ParseEngineeringValue(gProp, 1.0);
            else if (comp.TryGetProperty("k", out var kProp)) gainComp.Gain = ParseEngineeringValue(kProp, 1.0);
        }
        else if (device is StateSpaceBlockDevice ssComp)
        {
            if (comp.TryGetProperty("a", out var aProp)) ssComp.AString = aProp.GetString() ?? aProp.GetRawText();
            if (comp.TryGetProperty("b", out var bProp)) ssComp.BString = bProp.GetString() ?? bProp.GetRawText();
            if (comp.TryGetProperty("c", out var cProp)) ssComp.CString = cProp.GetString() ?? cProp.GetRawText();
            if (comp.TryGetProperty("d", out var dProp)) ssComp.DString = dProp.GetString() ?? dProp.GetRawText();
            ssComp.RebuildModel();
        }
        else if (device is MathExpressionDevice mathComp)
        {
            if (comp.TryGetProperty("expression", out var expProp) || comp.TryGetProperty("expr", out expProp) || comp.TryGetProperty("func", out expProp))
                mathComp.Expression = expProp.GetString() ?? expProp.GetRawText();
        }
        else if (device is DiscreteTransferFunctionDevice dtfComp)
        {
            if (comp.TryGetProperty("numerator", out var numProp)) dtfComp.NumeratorString = numProp.GetString() ?? numProp.GetRawText();
            if (comp.TryGetProperty("denominator", out var denProp)) dtfComp.DenominatorString = denProp.GetString() ?? denProp.GetRawText();
            if (comp.TryGetProperty("sample_time", out var tsProp) || comp.TryGetProperty("ts", out tsProp))
                dtfComp.SampleTime = ParseEngineeringValue(tsProp, 0.01);
        }
        else if (device is RampSourceDevice rampComp)
        {
            if (comp.TryGetProperty("slope", out var sProp)) rampComp.Slope = ParseEngineeringValue(sProp, 1.0);
            if (comp.TryGetProperty("start_time", out var stProp)) rampComp.StartTime = ParseEngineeringValue(stProp, 0.0);
            if (comp.TryGetProperty("initial_value", out var ivProp)) rampComp.InitialValue = ParseEngineeringValue(ivProp, 0.0);
        }
        else if (device is ConstantSourceDevice constComp)
        {
            if (comp.TryGetProperty("value", out var vProp) || comp.TryGetProperty("val", out vProp) || comp.TryGetProperty("k", out vProp))
                constComp.Value = ParseEngineeringValue(vProp, 1.0);
        }
        else if (device is DcMotorPlantDevice motorComp)
        {
            if (comp.TryGetProperty("ra", out var raP) || comp.TryGetProperty("resistancera", out raP) || comp.TryGetProperty("resistance", out raP))
                motorComp.ResistanceRa = ParseEngineeringValue(raP, 1.0);
            if (comp.TryGetProperty("la", out var laP) || comp.TryGetProperty("inductancela", out laP) || comp.TryGetProperty("inductance", out laP))
                motorComp.InductanceLa = ParseEngineeringValue(laP, 0.005);
            if (comp.TryGetProperty("kt", out var ktP) || comp.TryGetProperty("torqueconstantkt", out ktP))
                motorComp.TorqueConstantKt = ParseEngineeringValue(ktP, 0.05);
            if (comp.TryGetProperty("ke", out var keP) || comp.TryGetProperty("backemfconstantke", out keP))
                motorComp.BackEmfConstantKe = ParseEngineeringValue(keP, 0.05);
            if (comp.TryGetProperty("j", out var jP) || comp.TryGetProperty("rotorinertiaj", out jP) || comp.TryGetProperty("inertia", out jP))
                motorComp.RotorInertiaJ = ParseEngineeringValue(jP, 0.001);
            if (comp.TryGetProperty("b", out var bP) || comp.TryGetProperty("viscousdampingb", out bP) || comp.TryGetProperty("damping", out bP))
                motorComp.ViscousDampingB = ParseEngineeringValue(bP, 0.002);
        }
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

            if (device is TransferFunctionDevice tfObj)
            {
                if (key is "numerator" or "num")
                {
                    tfObj.NumeratorString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText();
                    continue;
                }
                if (key is "denominator" or "den")
                {
                    tfObj.DenominatorString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText();
                    continue;
                }
            }
            if (device is SummingJunctionDevice sumObj && key is "signs" or "polarity")
            {
                sumObj.Signs = prop.Value.GetString() ?? "+-";
                continue;
            }
            if (device is StepSourceDevice stepObj)
            {
                if (key is "step_time" or "steptime") { stepObj.StepTime = ParseEngineeringValue(prop.Value, 0.01); continue; }
                if (key is "initial_value" or "init_val" or "initial") { stepObj.InitialValue = ParseEngineeringValue(prop.Value, 0.0); continue; }
                if (key is "final_value" or "final_val" or "final") { stepObj.FinalValue = ParseEngineeringValue(prop.Value, 1.0); continue; }
            }
            if (device is PidControllerDevice pidObj)
            {
                if (key is "kp" or "p") { pidObj.Kp = ParseEngineeringValue(prop.Value, 1.0); continue; }
                if (key is "ki" or "i") { pidObj.Ki = ParseEngineeringValue(prop.Value, 0.0); continue; }
                if (key is "kd" or "d") { pidObj.Kd = ParseEngineeringValue(prop.Value, 0.0); continue; }
            }
            if (device is GainBlockDevice gainObj && key is "gain" or "k")
            {
                gainObj.Gain = ParseEngineeringValue(prop.Value, 1.0);
                continue;
            }
            if (device is StateSpaceBlockDevice ssObj)
            {
                if (key == "a") { ssObj.AString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText(); continue; }
                if (key == "b") { ssObj.BString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText(); continue; }
                if (key == "c") { ssObj.CString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText(); continue; }
                if (key == "d") { ssObj.DString = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText(); continue; }
            }
            if (device is MathExpressionDevice mathObj && key is "expression" or "expr" or "func")
            {
                mathObj.Expression = prop.Value.GetString() ?? prop.Value.GetRawText();
                continue;
            }
            if (device is DiscreteTransferFunctionDevice dtfObj)
            {
                if (key is "numerator" or "num") { dtfObj.NumeratorString = prop.Value.GetString() ?? prop.Value.GetRawText(); continue; }
                if (key is "denominator" or "den") { dtfObj.DenominatorString = prop.Value.GetString() ?? prop.Value.GetRawText(); continue; }
            }
            if (device is DcMotorPlantDevice motorObj)
            {
                if (key is "ra" or "resistance" or "resistancera" or "r") { motorObj.ResistanceRa = ParseEngineeringValue(prop.Value, 1.0); continue; }
                if (key is "la" or "inductance" or "inductancela" or "l") { motorObj.InductanceLa = ParseEngineeringValue(prop.Value, 0.005); continue; }
                if (key is "kt" or "torqueconstant" or "torqueconstantkt") { motorObj.TorqueConstantKt = ParseEngineeringValue(prop.Value, 0.05); continue; }
                if (key is "ke" or "backemfconstant" or "backemfconstantke") { motorObj.BackEmfConstantKe = ParseEngineeringValue(prop.Value, 0.05); continue; }
                if (key is "j" or "inertia" or "rotorinertiaj") { motorObj.RotorInertiaJ = ParseEngineeringValue(prop.Value, 0.001); continue; }
                if (key is "b" or "damping" or "viscousdampingb") { motorObj.ViscousDampingB = ParseEngineeringValue(prop.Value, 0.002); continue; }
            }

            double val = ParseEngineeringValue(prop.Value);
            device.SetParameter(key, val);
        }

        if (device is StateSpaceBlockDevice ssFinal)
        {
            ssFinal.RebuildModel();
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
            "dcfan" or "cooler" or "blower" => "fan",
            "bldcmotor" or "drone_motor" => "bldc",
            "bldcesc" or "speedcontroller" => "esc",
            "buck" or "dcdc" => "bec",
            "piezo" or "beeper" => "buzzer",
            "tvsdiode" => "tvs",
            "transfer_function" or "transferfunction" or "plant" => "tf",
            "summing_junction" or "summer" or "adder" => "sum",
            "pid_controller" => "pid",
            "gain_block" => "gain",
            "step_source" or "step_gen" => "step",
            "ramp_source" or "rampgen" => "ramp",
            "constant_source" or "setpoint" => "const",
            "noise_source" or "whitenoise" or "noisegen" => "noise",
            "pulse_source" or "pulsegen" => "pulse",
            "multiplier" or "product" => "mult",
            "divider" or "quotient" => "div",
            "math_expression" or "function" or "mathfunc" => "mathexpr",
            "dead_zone" or "deadband" => "deadzone",
            "relay_block" or "hysteresis" => "relay",
            "rate_limiter" or "slew" or "slewrate" => "ratelimiter",
            "state_space" or "statespacemodel" => "statespace",
            "dc_motor" or "motor_plant" or "dcmotorplant" => "dcmotor",
            "zero_order_hold" or "sample_hold" or "samplehold" => "zoh",
            "unit_delay" or "z_delay" or "zdelay" or "zinv" => "unitdelay",
            "discrete_transfer_function" or "discrete_tf" or "dtf" or "ztrans" => "discretetf",
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
