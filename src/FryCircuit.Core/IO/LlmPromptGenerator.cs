using System.Text;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.IO;

/// <summary>
/// Generates turnkey prompt templates, few-shot examples, and schema specifications
/// for conversational LLMs (Claude, GPT-4, Gemini, DeepSeek, Ollama) to design and debug .fcircuit schematics.
/// </summary>
public static class LlmPromptGenerator
{
    /// <summary>
    /// Generates the complete system prompt and JSON schema guide for LLMs.
    /// </summary>
    public static string GenerateSystemPrompt()
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Role & Task");
        sb.AppendLine("You are an expert electrical engineer, analog circuit designer, and digital logic CAD architect.");
        sb.AppendLine("Your goal is to design circuits for the interactive real-time circuit simulator **FryCircuit**.");
        sb.AppendLine("Output circuits ONLY as valid JSON in the **.fcircuit** format enclosed in a ```json code fence.");
        sb.AppendLine();

        sb.AppendLine("# .fcircuit JSON Format Rules");
        sb.AppendLine("1. **Root Fields**:");
        sb.AppendLine("   - `\"name\"`: Short descriptive title of the circuit.");
        sb.AppendLine("   - `\"description\"`: 1-2 sentence engineering description (cutoff frequency, gain, etc.).");
        sb.AppendLine("   - `\"components\"`: Array of component objects.");
        sb.AppendLine();
        sb.AppendLine("2. **Component Fields**:");
        sb.AppendLine("   - `\"id\"`: Unique designator string (e.g. `\"R1\"`, `\"C1\"`, `\"U1\"`, `\"V1\"`, `\"Q1\"`).");
        sb.AppendLine("   - `\"type\"`: Component type name (see table below).");
        sb.AppendLine("   - `\"value\"`: Primary parameter value with engineering suffix (e.g. `\"10k\"`, `\"100nF\"`, `\"1kHz\"`, `\"5V\"`).");
        sb.AppendLine("   - `\"pins\"`: Object mapping pin name to net name (e.g. `{\"in-\": \"inv\", \"in+\": \"gnd\", \"out\": \"vout\"}`).");
        sb.AppendLine("   - `\"nodes\"`: (Alternative for 2-terminal passives) Positional array of 2 net names (e.g. `[\"vin\", \"vout\"]`).");
        sb.AppendLine("   - `\"parameters\"`: (Optional) Additional parameter settings like `{\"waveform\": \"sine\", \"amplitude\": \"5V\"}`.");
        sb.AppendLine("   - `\"x\"`, `\"y\"`: (Optional) Schematic grid coordinates. If omitted, FryCircuit automatically performs topological auto-layout!");
        sb.AppendLine();

        sb.AppendLine("# Supported Engineering Units & Suffixes");
        sb.AppendLine("- Metric suffixes: `p` (10^-12), `n` (10^-9), `u` or `µ` (10^-6), `m` (10^-3), `k` (10^3), `M` or `meg` (10^6), `G` (10^9).");
        sb.AppendLine("- Units: `V` (Volts), `A` (Amps), `Hz` (Hertz), `F` (Farads), `H` (Henries), `Ohm` or `Ω` (Resistance), `s` (Seconds).");
        sb.AppendLine("- Ground net name: use `\"gnd\"`, `\"ground\"`, or `\"0\"` for circuit common return.");
        sb.AppendLine();

        sb.AppendLine("# Component Types & Pin Aliases");
        sb.AppendLine("| Component Type | `type` Name | Primary `value` | Pins & Aliases |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine("| Resistor | `resistor` | Resistance (`10k`) | `nodes: [\"net1\", \"net2\"]` or `pins: {\"1\": \"net1\", \"2\": \"net2\"}` |");
        sb.AppendLine("| Capacitor | `capacitor` | Capacitance (`100nF`) | `nodes: [\"net1\", \"net2\"]` |");
        sb.AppendLine("| Inductor | `inductor` | Inductance (`1mH`) | `nodes: [\"net1\", \"net2\"]` |");
        sb.AppendLine("| Diode | `diode` | Forward Drop | `pins: {\"anode\": \"netA\", \"cathode\": \"netK\"}` |");
        sb.AppendLine("| LED | `led` | Forward Drop | `pins: {\"anode\": \"netA\", \"cathode\": \"netK\"}` |");
        sb.AppendLine("| Zener Diode | `zener` | Breakdown (`5.1V`) | `pins: {\"anode\": \"netA\", \"cathode\": \"netK\"}` |");
        sb.AppendLine("| Voltage Source | `vsource` | Voltage (`5V`) | `pins: {\"+\": \"netP\", \"-\": \"netN\"}`, `waveform: \"sine\"|\"pulse\"|\"dc\"`, `frequency: \"1kHz\"` |");
        sb.AppendLine("| Current Source | `csource` | Current (`10mA`) | `pins: {\"+\": \"netP\", \"-\": \"netN\"}` |");
        sb.AppendLine("| Op-Amp | `opamp` | Open-loop gain | `pins: {\"in-\": \"inv_net\", \"in+\": \"noninv_net\", \"out\": \"out_net\"}` |");
        sb.AppendLine("| BJT (NPN / PNP) | `npn` / `pnp` | Beta (`100`) | `pins: {\"base\": \"b_net\", \"collector\": \"c_net\", \"emitter\": \"e_net\"}` |");
        sb.AppendLine("| MOSFET (NMOS/PMOS) | `nmos` / `pmos` | Threshold Vth | `pins: {\"gate\": \"g_net\", \"drain\": \"d_net\", \"source\": \"s_net\"}` |");
        sb.AppendLine("| 555 Timer IC | `timer555` | Output Ron | `pins: {\"gnd\": \"gnd\", \"trig\": \"...\", \"out\": \"...\", \"reset\": \"vcc\", \"ctrl\": \"...\", \"thresh\": \"...\", \"disch\": \"...\", \"vcc\": \"vcc\"}` |");
        sb.AppendLine("| Switch (SPST) | `spst` | State (`0` or `1`) | `pins: {\"in\": \"net1\", \"out\": \"net2\"}` |");
        sb.AppendLine("| Logic Gates | `and`,`or`,`not`,`nand`,`nor`,`xor` | Logic High | `pins: {\"in1\": \"a\", \"in2\": \"b\", \"out\": \"y\"}` |");
        sb.AppendLine("| Voltmeter / Ammeter | `voltmeter` / `amperemeter` | Measurement | `pins: {\"+\": \"pos\", \"-\": \"neg\"}` |");
        sb.AppendLine("| Ground Reference | `ground` | Reference | `nodes: [\"gnd\"]` |");
        sb.AppendLine();

        sb.AppendLine("# Working Few-Shot Examples");
        sb.AppendLine("### Example 1: Inverting Op-Amp Amplifier (Gain = -10)");
        sb.AppendLine("```json");
        sb.AppendLine(GetSampleTemplate("Inverting Op-Amp"));
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Example 2: RC Low-Pass Filter (fc = 1.59 kHz)");
        sb.AppendLine("```json");
        sb.AppendLine(GetSampleTemplate("RC Low-Pass Filter"));
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Example 3: NPN Transistor Switch Driving an LED");
        sb.AppendLine("```json");
        sb.AppendLine(GetSampleTemplate("BJT LED Driver"));
        sb.AppendLine("```");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates a prompt containing an existing circuit schematic for modification or debugging.
    /// </summary>
    public static string GenerateCircuitInspectionPrompt(Circuit circuit)
    {
        string json = LlmCircuitSerializer.Serialize(circuit);
        var sb = new StringBuilder();

        sb.AppendLine(GenerateSystemPrompt());
        sb.AppendLine("# Current Circuit Schematic");
        sb.AppendLine("Below is the existing schematic currently open in FryCircuit Studio:");
        sb.AppendLine("```json");
        sb.AppendLine(json);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("# Instructions");
        sb.AppendLine("Analyze this circuit, suggest improvements, or generate a modified .fcircuit JSON based on the user's requirements.");

        return sb.ToString();
    }

    /// <summary>
    /// Returns sample .fcircuit JSON templates.
    /// </summary>
    public static string GetSampleTemplate(string templateName)
    {
        return templateName switch
        {
            "Inverting Op-Amp" =>
                """
                {
                  "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
                  "name": "Inverting Op-Amp Amplifier",
                  "description": "Gain = -10 Inverting amplifier with 1kHz AC input",
                  "components": [
                    {
                      "id": "V1",
                      "type": "vsource",
                      "waveform": "sine",
                      "amplitude": "1V",
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
                      "value": "100k",
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
                      "id": "Rload",
                      "type": "resistor",
                      "value": "1k",
                      "nodes": ["vout", "gnd"]
                    },
                    {
                      "id": "GND",
                      "type": "ground",
                      "nodes": ["gnd"]
                    }
                  ]
                }
                """,

            "RC Low-Pass Filter" =>
                """
                {
                  "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
                  "name": "RC Low-Pass Filter",
                  "description": "First-order passive low-pass filter with fc = 1.59 kHz",
                  "components": [
                    {
                      "id": "V1",
                      "type": "vsource",
                      "waveform": "sine",
                      "amplitude": "5V",
                      "frequency": "1kHz",
                      "pins": { "+": "vin", "-": "gnd" }
                    },
                    {
                      "id": "R1",
                      "type": "resistor",
                      "value": "1k",
                      "nodes": ["vin", "vout"]
                    },
                    {
                      "id": "C1",
                      "type": "capacitor",
                      "value": "100nF",
                      "nodes": ["vout", "gnd"]
                    },
                    {
                      "id": "VM1",
                      "type": "voltmeter",
                      "pins": { "+": "vout", "-": "gnd" }
                    },
                    {
                      "id": "GND",
                      "type": "ground",
                      "nodes": ["gnd"]
                    }
                  ]
                }
                """,

            "BJT LED Driver" =>
                """
                {
                  "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
                  "name": "NPN BJT LED Driver Switch",
                  "description": "Transistor switch controlling an indicator LED with 5V supply",
                  "components": [
                    {
                      "id": "Vcc",
                      "type": "vsource",
                      "waveform": "dc",
                      "amplitude": "5V",
                      "pins": { "+": "vcc", "-": "gnd" }
                    },
                    {
                      "id": "Vctrl",
                      "type": "vsource",
                      "waveform": "pulse",
                      "amplitude": "3.3V",
                      "frequency": "10Hz",
                      "pins": { "+": "ctrl", "-": "gnd" }
                    },
                    {
                      "id": "Rbase",
                      "type": "resistor",
                      "value": "10k",
                      "nodes": ["ctrl", "base_node"]
                    },
                    {
                      "id": "Rled",
                      "type": "resistor",
                      "value": "220",
                      "nodes": ["vcc", "led_anode"]
                    },
                    {
                      "id": "LED1",
                      "type": "led",
                      "pins": { "anode": "led_anode", "cathode": "collector_node" }
                    },
                    {
                      "id": "Q1",
                      "type": "npn",
                      "pins": { "base": "base_node", "collector": "collector_node", "emitter": "gnd" }
                    },
                    {
                      "id": "GND",
                      "type": "ground",
                      "nodes": ["gnd"]
                    }
                  ]
                }
                """,

            "555 Timer Oscillator" =>
                """
                {
                  "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
                  "name": "555 Astable Multivibrator",
                  "description": "Classic 555 pulse generator operating at approx 1 kHz",
                  "components": [
                    {
                      "id": "Vcc",
                      "type": "vsource",
                      "waveform": "dc",
                      "amplitude": "9V",
                      "pins": { "+": "vcc", "-": "gnd" }
                    },
                    {
                      "id": "R1",
                      "type": "resistor",
                      "value": "10k",
                      "nodes": ["vcc", "disch_node"]
                    },
                    {
                      "id": "R2",
                      "type": "resistor",
                      "value": "47k",
                      "nodes": ["disch_node", "timing_node"]
                    },
                    {
                      "id": "C1",
                      "type": "capacitor",
                      "value": "10nF",
                      "nodes": ["timing_node", "gnd"]
                    },
                    {
                      "id": "U1",
                      "type": "timer555",
                      "pins": {
                        "gnd": "gnd",
                        "trig": "timing_node",
                        "out": "pulse_out",
                        "reset": "vcc",
                        "ctrl": "ctrl_node",
                        "thresh": "timing_node",
                        "disch": "disch_node",
                        "vcc": "vcc"
                      }
                    },
                    {
                      "id": "C_bypass",
                      "type": "capacitor",
                      "value": "10nF",
                      "nodes": ["ctrl_node", "gnd"]
                    },
                    {
                      "id": "VM1",
                      "type": "voltmeter",
                      "pins": { "+": "pulse_out", "-": "gnd" }
                    },
                    {
                      "id": "GND",
                      "type": "ground",
                      "nodes": ["gnd"]
                    }
                  ]
                }
                """,

            _ => GetSampleTemplate("RC Low-Pass Filter")
        };
    }
}
