using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Devices.Ground;
using FryCircuit.Core.Devices.VoltageSource;
using FryCircuit.Core.IO;
using FryCircuit.Core.Presets;
using FryCircuit.Core.Simulation;
using Xunit;

namespace FryCircuit.Core.Tests;

public class LlmCircuitSerializerTests
{
    [Theory]
    [InlineData("10k", 10000.0)]
    [InlineData("4.7k", 4700.0)]
    [InlineData("100nF", 1e-7)]
    [InlineData("10uF", 1e-5)]
    [InlineData("100pF", 1e-10)]
    [InlineData("1mH", 0.001)]
    [InlineData("5V", 5.0)]
    [InlineData("1kHz", 1000.0)]
    [InlineData("2.5ms", 0.0025)]
    [InlineData("10MEG", 1e7)]
    [InlineData("1M", 1e6)]
    [InlineData("500mA", 0.5)]
    [InlineData("50%", 0.5)]
    [InlineData("100", 100.0)]
    [InlineData("-15V", -15.0)]
    public void ParseEngineeringValue_ParsesCorrectly(string input, double expected)
    {
        double actual = LlmCircuitSerializer.ParseEngineeringValue(input);
        Assert.True(Math.Abs(actual - expected) / Math.Max(1.0, Math.Abs(expected)) < 1e-4,
            $"Expected {expected} for '{input}' but got {actual}");
    }

    [Fact]
    public void ExtractJsonContent_StripsMarkdownFencesAndChatText()
    {
        string responseWithMarkdown =
            """
            Here is your circuit design:
            ```json
            {
              "name": "Test Circuit",
              "components": [
                { "id": "R1", "type": "resistor", "value": "1k", "nodes": ["vin", "vout"] }
              ]
            }
            ```
            Hope this helps!
            """;

        string extracted = LlmCircuitSerializer.ExtractJsonContent(responseWithMarkdown);
        Assert.StartsWith("{", extracted);
        Assert.EndsWith("}", extracted);
        Assert.Contains("Test Circuit", extracted);
        Assert.DoesNotContain("```", extracted);
    }

    [Fact]
    public void Deserialize_InvertingOpAmp_ResolvesNamedNetsAndSemanticPins()
    {
        string json =
            """
            {
              "name": "Inverting OpAmp Test",
              "components": [
                {
                  "id": "V1",
                  "type": "vsource",
                  "waveform": "sine",
                  "amplitude": "2V",
                  "frequency": "1kHz",
                  "pins": { "+": "vin", "-": "gnd" }
                },
                {
                  "id": "Rin",
                  "type": "resistor",
                  "value": "10k",
                  "nodes": ["vin", "inv_node"]
                },
                {
                  "id": "Rfb",
                  "type": "resistor",
                  "value": "50k",
                  "nodes": ["inv_node", "vout"]
                },
                {
                  "id": "U1",
                  "type": "opamp",
                  "pins": {
                    "in-": "inv_node",
                    "in+": "gnd",
                    "out": "vout"
                  }
                },
                {
                  "id": "GND",
                  "type": "ground",
                  "nodes": ["gnd"]
                }
              ]
            }
            """;

        var circuit = LlmCircuitSerializer.Deserialize(json);

        Assert.Equal("Inverting OpAmp Test", circuit.Name);
        Assert.True(circuit.Devices.Count >= 5);

        // Ground node must be identified
        var gndNode = circuit.FindGroundNode();
        Assert.NotNull(gndNode);

        // Op-Amp pin connections
        var opamp = circuit.Devices.OfType<OpAmpDevice>().FirstOrDefault();
        Assert.NotNull(opamp);

        // Inverting pin (pin 0) and non-inverting pin (pin 1)
        int invertingNodeId = opamp.Pins[0].NodeId;
        int nonInvertingNodeId = opamp.Pins[1].NodeId;
        int outNodeId = opamp.Pins[2].NodeId;

        // Non-inverting pin must be connected to ground
        Assert.Equal(gndNode.Id, nonInvertingNodeId);

        // Rin and Rfb must share the inverting node
        var rin = circuit.Devices.First(d => d is ResistorDevice r && Math.Abs(r.Resistance - 10000.0) < 1.0);
        var rfb = circuit.Devices.First(d => d is ResistorDevice r && Math.Abs(r.Resistance - 50000.0) < 1.0);
        Assert.Contains(rin.Pins, p => p.NodeId == invertingNodeId);
        Assert.Contains(rfb.Pins, p => p.NodeId == invertingNodeId);

        // Rfb and OpAmp output must share the out node
        Assert.Contains(rfb.Pins, p => p.NodeId == outNodeId);

        // Auto-layout should have separated devices
        var uniqueLocations = circuit.Devices.Select(d => (d.LocationX, d.LocationY)).Distinct().Count();
        Assert.Equal(circuit.Devices.Count, uniqueLocations);
    }

    [Fact]
    public void Deserialize_BjtTransistorDriver_ResolvesPinAliases()
    {
        string json =
            """
            {
              "name": "BJT LED Switch",
              "components": [
                {
                  "id": "Q1",
                  "type": "npn",
                  "value": "150",
                  "pins": {
                    "base": "b_net",
                    "collector": "c_net",
                    "emitter": "gnd"
                  }
                },
                {
                  "id": "D1",
                  "type": "led",
                  "pins": {
                    "anode": "vcc",
                    "cathode": "c_net"
                  }
                }
              ]
            }
            """;

        var circuit = LlmCircuitSerializer.Deserialize(json);
        var bjt = circuit.Devices.OfType<BjtDevice>().FirstOrDefault();
        var led = circuit.Devices.OfType<LedDevice>().FirstOrDefault();

        Assert.NotNull(bjt);
        Assert.NotNull(led);
        Assert.Equal(150.0, bjt.BetaF);

        // Collector (pin 1) must connect to LED cathode (pin 1)
        Assert.Equal(bjt.Pins[1].NodeId, led.Pins[1].NodeId);

        // Emitter (pin 2) must be on ground (node 0)
        Assert.Equal(0, bjt.Pins[2].NodeId);
    }

    [Fact]
    public void Deserialize_Timer555_ResolvesNamedPins()
    {
        string json =
            """
            {
              "name": "555 Timer Test",
              "components": [
                {
                  "id": "U1",
                  "type": "timer555",
                  "pins": {
                    "gnd": "gnd",
                    "trig": "trigger_net",
                    "out": "out_net",
                    "vcc": "vcc_net"
                  }
                }
              ]
            }
            """;

        var circuit = LlmCircuitSerializer.Deserialize(json);
        var timer = circuit.Devices.First(d => d.Type == "timer555");
        Assert.NotNull(timer);

        // Pin 0 is GND
        Assert.Equal(0, timer.Pins[0].NodeId);
        // Pin 1 is TRIG
        Assert.True(timer.Pins[1].NodeId > 0);
        // Pin 2 is OUT
        Assert.True(timer.Pins[2].NodeId > 0);
        Assert.NotEqual(timer.Pins[1].NodeId, timer.Pins[2].NodeId);
    }

    [Fact]
    public void RoundTrip_SerializeAndDeserialize_PreservesCircuitState()
    {
        var original = CircuitPresets.CreateHalfWaveRectifier();
        string json = LlmCircuitSerializer.Serialize(original);

        Assert.NotEmpty(json);
        Assert.Contains("components", json);
        Assert.Contains("vsource", json);
        Assert.Contains("diode", json);
        Assert.Contains("resistor", json);

        var restored = LlmCircuitSerializer.Deserialize(json);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Devices.Count, restored.Devices.Count);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);

        var origDiode = original.Devices.First(d => d.Type == "diode");
        var restDiode = restored.Devices.First(d => d.Type == "diode");
        Assert.Equal(origDiode.LocationX, restDiode.LocationX);
        Assert.Equal(origDiode.LocationY, restDiode.LocationY);
    }

    [Fact]
    public void CircuitSerializer_Deserialize_AutoDetectsAllFormats()
    {
        // 1. JSON .fcircuit format
        string json =
            """
            {
              "name": "Auto Detect JSON",
              "components": [
                { "id": "R1", "type": "resistor", "value": "10k", "nodes": ["in", "out"] }
              ]
            }
            """;
        var cJson = CircuitSerializer.Deserialize(json);
        Assert.Equal("Auto Detect JSON", cJson.Name);
        Assert.Single(cJson.Devices);

        // 2. XML .frycircuit format
        string xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <circuit version="10" name="Auto Detect XML">
              <frame xmin="0" ymin="0" xmax="20" ymax="20" />
              <nodes><node id="1" /></nodes>
              <devices><device id="1" type="resistor" x="2" y="2"><pins><pin index="0" nodeId="1"/><pin index="1" nodeId="1"/></pins></device></devices>
            </circuit>
            """;
        var cXml = CircuitSerializer.Deserialize(xml);
        Assert.Equal("Auto Detect XML", cXml.Name);
        Assert.Single(cXml.Devices);

        // 3. SPICE netlist format
        string spice =
            """
            * Auto Detect SPICE
            R1 in out 10k
            C1 out 0 100n
            .END
            """;
        var cSpice = CircuitSerializer.Deserialize(spice);
        Assert.True(cSpice.Devices.Count >= 2);
    }

    [Fact]
    public void LlmPromptGenerator_GeneratesTurnkeyPromptWithSchemaAndExamples()
    {
        string prompt = LlmPromptGenerator.GenerateSystemPrompt();
        Assert.NotEmpty(prompt);
        Assert.Contains(".fcircuit", prompt);
        Assert.Contains("Inverting Op-Amp", prompt);
        Assert.Contains("RC Low-Pass Filter", prompt);
        Assert.Contains("components", prompt);
        Assert.Contains("Engineering Units", prompt);
    }

    [Fact]
    public void RoundTrip_ControlBlockDiagramEquivalence_PreservesPolynomialsAndSigns()
    {
        var original = CircuitPresets.CreateFeedbackBlockDiagramEquivalence();
        string json = LlmCircuitSerializer.Serialize(original);

        Assert.NotEmpty(json);
        Assert.Contains("\"numerator\": \"1, 2\"", json);
        Assert.Contains("\"denominator\": \"1, 3, 3\"", json);
        Assert.Contains("\"signs\": \"+-\"", json);

        var restored = LlmCircuitSerializer.Deserialize(json);
        Assert.Equal(original.Devices.Count, restored.Devices.Count);

        var origTf = original.Devices.OfType<TransferFunctionDevice>().First(d => d.Id == 5);
        var restTf = restored.Devices.OfType<TransferFunctionDevice>().First(d => d.Id == 5);
        Assert.Equal(origTf.NumeratorString, restTf.NumeratorString);
        Assert.Equal(origTf.DenominatorString, restTf.DenominatorString);

        var origSum = original.Devices.OfType<SummingJunctionDevice>().First();
        var restSum = restored.Devices.OfType<SummingJunctionDevice>().First();
        Assert.Equal(origSum.Signs, restSum.Signs);
    }

    [Fact]
    public void Deserialize_ControlClosedLoop_SimulatesCorrectlyInMna()
    {
        string json =
            """
            {
              "name": "Closed Loop Control Test",
              "components": [
                {
                  "id": "STEP1",
                  "type": "step",
                  "step_time": "0.0",
                  "initial_value": "0.0",
                  "final_value": "1.0",
                  "pins": { "out": "ref", "ref": "gnd" }
                },
                {
                  "id": "SUM1",
                  "type": "sum",
                  "signs": "+-",
                  "pins": { "in1": "ref", "in2": "feedback", "out": "err", "ref": "gnd" }
                },
                {
                  "id": "TF1",
                  "type": "tf",
                  "numerator": "1",
                  "denominator": "1, 1",
                  "pins": { "in": "err", "out": "feedback", "ref": "gnd" }
                },
                {
                  "id": "GND1",
                  "type": "ground",
                  "pins": { "gnd": "gnd" }
                }
              ]
            }
            """;

        var circuit = LlmCircuitSerializer.Deserialize(json);
        Assert.Equal(4, circuit.Devices.Count);

        var solver = new MnaSolver(circuit);

        // Step simulation for 1000 steps of 1ms (1.0 second)
        // For G(s) = 1/(s+1) in unity negative feedback, closed loop is T(s) = 1/(s + 2)
        // Step response: y(t) = 0.5 * (1 - e^(-2t))
        // At t = 1.0s, expected = 0.5 * (1 - e^(-2)) = 0.5 * (1 - 0.1353) ~= 0.4323 V
        for (int i = 0; i < 1000; i++)
        {
            solver.Step(1e-3);
        }

        var tfDev = circuit.Devices.OfType<TransferFunctionDevice>().First();
        int outNode = tfDev.Pins[1].NodeId;
        double vOut = circuit.Nodes[outNode].Voltage;

        double expected = 0.5 * (1.0 - Math.Exp(-2.0));
        Assert.True(Math.Abs(vOut - expected) < 0.05, $"Expected {expected} but got {vOut}");
    }
}
