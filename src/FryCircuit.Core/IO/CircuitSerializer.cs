using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.IO;

/// <summary>
/// Universal circuit serializer supporting both the modern LLM-optimized (.fcircuit) JSON format
/// and the legacy FryCircuit (.frycircuit / .circuit) XML schema, with automatic format detection on load.
/// </summary>
public static class CircuitSerializer
{
    public const string DefaultFileExtension = ".frycircuit";
    public const string FcircuitFileExtension = ".fcircuit";
    public const string LlmFileExtension = ".fcircuit";
    public const string XmlFileExtension = ".frycircuit";
    public const string LegacyFileExtension = ".circuit";

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    /// <summary>
    /// Saves a circuit to disk. Automatically uses .fcircuit JSON or XML based on the file extension.
    /// </summary>
    public static void SaveToFile(Circuit circuit, string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".fcircuit" or ".json" or ".fcircuit.json")
        {
            string json = LlmCircuitSerializer.Serialize(circuit);
            File.WriteAllText(filePath, json, Encoding.UTF8);
            return;
        }

        using var stream = File.Create(filePath);
        var doc = BuildDocument(circuit);
        doc.Save(stream);
    }

    /// <summary>
    /// Loads a circuit from file, automatically detecting .fcircuit JSON, XML, or SPICE netlists.
    /// </summary>
    public static Circuit LoadFromFile(string filePath)
    {
        string text = File.ReadAllText(filePath, Encoding.UTF8);
        var circuit = Deserialize(text);
        if (string.IsNullOrWhiteSpace(circuit.Name) || circuit.Name == "Untitled")
        {
            circuit.Name = Path.GetFileNameWithoutExtension(filePath);
        }
        return circuit;
    }

    /// <summary>
    /// Loads a circuit from stream, auto-detecting format.
    /// </summary>
    public static Circuit LoadFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string text = reader.ReadToEnd();
        return Deserialize(text);
    }

    /// <summary>
    /// Deserializes circuit text, automatically detecting .fcircuit JSON, XML, or SPICE syntax.
    /// </summary>
    public static Circuit Deserialize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("Cannot deserialize empty circuit text.");
        }

        string trimmed = text.Trim();

        // Check if JSON / Markdown fence (starts with '{', '[', or '```')
        if (trimmed.StartsWith("{") || trimmed.StartsWith("[") || trimmed.StartsWith("```") || trimmed.Contains("\"components\""))
        {
            try
            {
                return LlmCircuitSerializer.Deserialize(trimmed);
            }
            catch when (trimmed.Contains("<circuit"))
            {
                // Fallback to XML if JSON parsing fails but XML tags exist
            }
        }

        // Check if XML
        if (trimmed.StartsWith("<"))
        {
            var doc = XDocument.Parse(trimmed);
            return DeserializeDocument(doc);
        }

        // Check if SPICE deck (starts with comment or directive)
        if (trimmed.StartsWith("*") || trimmed.StartsWith(";") || trimmed.StartsWith("."))
        {
            return SpiceNetlistEngine.ImportSpice(trimmed);
        }

        // Default: Attempt JSON first, then XML
        try
        {
            return LlmCircuitSerializer.Deserialize(trimmed);
        }
        catch
        {
            var doc = XDocument.Parse(trimmed);
            return DeserializeDocument(doc);
        }
    }

    /// <summary>
    /// Default serialization (uses LLM-friendly .fcircuit JSON).
    /// </summary>
    public static string Serialize(Circuit circuit)
    {
        return LlmCircuitSerializer.Serialize(circuit);
    }

    /// <summary>
    /// Explicitly serializes circuit into XML format.
    /// </summary>
    public static string SerializeXml(Circuit circuit)
    {
        var doc = BuildDocument(circuit);
        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Explicitly serializes circuit into .fcircuit JSON format.
    /// </summary>
    public static string SerializeJson(Circuit circuit)
    {
        return LlmCircuitSerializer.Serialize(circuit);
    }

    public static XDocument BuildDocument(Circuit circuit)
    {
        var root = new XElement("circuit",
            new XAttribute("version", circuit.Version),
            new XAttribute("name", circuit.Name)
        );

        root.Add(new XElement("frame",
            new XAttribute("xmin", circuit.Frame.XMin.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("ymin", circuit.Frame.YMin.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("xmax", circuit.Frame.XMax.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("ymax", circuit.Frame.YMax.ToString(CultureInfo.InvariantCulture))
        ));

        root.Add(new XElement("options",
            new XAttribute("speed", circuit.Options.Speed.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("logichigh", circuit.Options.LogicHigh.ToString(CultureInfo.InvariantCulture))
        ));

        var nodesElem = new XElement("nodes");
        foreach (var node in circuit.Nodes.Values.OrderBy(n => n.Id))
        {
            var nodeElem = new XElement("node", new XAttribute("id", node.Id));
            if (node.IsGround)
            {
                nodeElem.Add(new XAttribute("ground", "true"));
            }
            nodesElem.Add(nodeElem);
        }
        root.Add(nodesElem);

        var devicesElem = new XElement("devices");
        foreach (var dev in circuit.Devices.OrderBy(d => d.Id))
        {
            var devElem = new XElement("device",
                new XAttribute("id", dev.Id),
                new XAttribute("type", dev.Type),
                new XAttribute("x", dev.LocationX.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("y", dev.LocationY.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("rotation", DeviceTransform.RotationToString(dev.Transform.Rotation)),
                new XAttribute("flip", dev.Transform.Flip ? "true" : "false")
            );

            var pinsElem = new XElement("pins");
            foreach (var pin in dev.Pins)
            {
                pinsElem.Add(new XElement("pin",
                    new XAttribute("index", pin.Index),
                    new XAttribute("nodeId", pin.NodeId)
                ));
            }
            devElem.Add(pinsElem);

            var paramsElem = new XElement("parameters");
            foreach (var kvp in dev.Parameters)
            {
                paramsElem.Add(new XElement("parameter",
                    new XAttribute("name", kvp.Key),
                    new XAttribute("value", kvp.Value.ToString(CultureInfo.InvariantCulture))
                ));
            }
            devElem.Add(paramsElem);

            devicesElem.Add(devElem);
        }
        root.Add(devicesElem);

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    private static Circuit DeserializeDocument(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
        {
            throw new FormatException("Invalid circuit document: missing root element.");
        }

        var circuit = new Circuit();

        if (int.TryParse(root.Attribute("version")?.Value, out int version))
        {
            circuit.Version = version;
        }

        string? nameAttr = root.Attribute("name")?.Value ?? root.Element("name")?.Value;
        if (!string.IsNullOrWhiteSpace(nameAttr))
        {
            circuit.Name = nameAttr.Trim();
        }

        var frameElem = root.Descendants("frame").FirstOrDefault();
        if (frameElem != null)
        {
            double xmin = ParseDouble(frameElem.Attribute("xmin")?.Value, 0.0);
            double ymin = ParseDouble(frameElem.Attribute("ymin")?.Value, 0.0);
            double xmax = ParseDouble(frameElem.Attribute("xmax")?.Value, 20.0);
            double ymax = ParseDouble(frameElem.Attribute("ymax")?.Value, 20.0);
            circuit.Frame = new CircuitFrame(xmin, ymin, xmax, ymax);
        }

        var optionsElem = root.Descendants("options").FirstOrDefault();
        if (optionsElem != null)
        {
            circuit.Options.Speed = ParseDouble(optionsElem.Attribute("speed")?.Value, 1.0);
            circuit.Options.LogicHigh = ParseDouble(optionsElem.Attribute("logichigh")?.Value, 5.0);
        }

        // Parse Nodes
        foreach (var nodeElem in root.Descendants("node").Where(e => e.Parent?.Name.LocalName != "device"))
        {
            if (int.TryParse(nodeElem.Attribute("id")?.Value, out int nodeId))
            {
                string? gndVal = nodeElem.Attribute("ground")?.Value;
                bool isGround = string.Equals(gndVal, "yes", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(gndVal, "true", StringComparison.OrdinalIgnoreCase);
                circuit.GetOrCreateNode(nodeId, isGround);
            }
        }

        // Parse Devices
        foreach (var devElem in root.Descendants("device"))
        {
            int id = int.TryParse(devElem.Attribute("id")?.Value, out int did) ? did : 0;
            string type = devElem.Attribute("type")?.Value ?? "resistor";

            double locX = ParseDouble(devElem.Attribute("x")?.Value ?? devElem.Element("location")?.Attribute("x")?.Value, 0.0);
            double locY = ParseDouble(devElem.Attribute("y")?.Value ?? devElem.Element("location")?.Attribute("y")?.Value, 0.0);

            var device = DeviceFactory.CreateDevice(type, id, locX, locY);

            string? rotStr = devElem.Attribute("rotation")?.Value ?? devElem.Element("transform")?.Attribute("rotate")?.Value;
            string? flipStr = devElem.Attribute("flip")?.Value ?? devElem.Element("transform")?.Attribute("flip")?.Value;
            device.Transform.Rotation = DeviceTransform.ParseRotation(rotStr);
            device.Transform.Flip = string.Equals(flipStr, "yes", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(flipStr, "true", StringComparison.OrdinalIgnoreCase);

            // Pins
            var pinElems = devElem.Descendants("pin").ToList();
            if (pinElems.Count > 0)
            {
                device.Pins.Clear();
                for (int i = 0; i < pinElems.Count; i++)
                {
                    int nId = int.TryParse(pinElems[i].Attribute("nodeId")?.Value, out int nid) ? nid : 0;
                    circuit.GetOrCreateNode(nId);
                    device.Pins.Add(new Pin(i, nId));
                }
            }
            else
            {
                var legacyNodes = devElem.Elements("node").ToList();
                if (legacyNodes.Count > 0)
                {
                    device.Pins.Clear();
                    for (int i = 0; i < legacyNodes.Count; i++)
                    {
                        int nId = int.TryParse(legacyNodes[i].Attribute("id")?.Value, out int nid) ? nid : 0;
                        circuit.GetOrCreateNode(nId);
                        device.Pins.Add(new Pin(i, nId));
                    }
                }
            }

            // Parameters
            foreach (var paramElem in devElem.Descendants("parameter"))
            {
                string? pname = paramElem.Attribute("name")?.Value;
                if (!string.IsNullOrWhiteSpace(pname))
                {
                    double pval = ParseDouble(paramElem.Attribute("value")?.Value, 0.0);
                    device.SetParameter(pname, pval);
                }
            }

            device.SetupDefaultPins();
            circuit.AddDevice(device);
        }

        circuit.EnsureWiresSynthesized();
        return circuit;
    }

    private static double ParseDouble(string? str, double defaultVal = 0.0)
    {
        if (string.IsNullOrWhiteSpace(str)) return defaultVal;
        return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : defaultVal;
    }
}
