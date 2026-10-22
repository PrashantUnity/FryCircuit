# FryCircuit `.fcircuit` Circuit Recipes

This document provides copy-pasteable, verified `.fcircuit` JSON blueprints for common electronic circuits. External AI agents and tools can pass any of these structures directly into the `circuit_load_schematic` MCP tool.

---

## 1. Precision Voltage Divider with Load

Divides a 12V DC input down to 6V, loaded with an external 10k resistor.

```json
{
  "name": "Precision Voltage Divider",
  "description": "12V DC input with dual 10k divider and 10k load resistor",
  "components": [
    {
      "id": "V1",
      "type": "vsource",
      "value": "12V",
      "parameters": { "voltage": 12.0 },
      "pins": { "+": "vin", "-": "gnd" }
    },
    {
      "id": "R1",
      "type": "resistor",
      "value": "10k",
      "nodes": ["vin", "vout"]
    },
    {
      "id": "R2",
      "type": "resistor",
      "value": "10k",
      "nodes": ["vout", "gnd"]
    },
    {
      "id": "R_load",
      "type": "resistor",
      "value": "10k",
      "nodes": ["vout", "gnd"]
    },
    {
      "id": "GND",
      "type": "ground",
      "nodes": ["gnd"]
    }
  ]
}
```

---

## 2. Inverting Op-Amp Audio Pre-Amplifier

Standard negative feedback inverting operational amplifier with $A_v = -5$ ($R_{fb} / R_{in} = 50\text{k} / 10\text{k}$).

```json
{
  "name": "Inverting Op-Amp Preamp",
  "description": "Gain of -5x inverting amplifier with 1kHz 1V AC sine input",
  "components": [
    {
      "id": "Vin",
      "type": "vsource",
      "waveform": "sine",
      "amplitude": "1V",
      "frequency": "1kHz",
      "pins": { "+": "sig_in", "-": "gnd" }
    },
    {
      "id": "Rin",
      "type": "resistor",
      "value": "10k",
      "nodes": ["sig_in", "inv_node"]
    },
    {
      "id": "Rfb",
      "type": "resistor",
      "value": "50k",
      "nodes": ["inv_node", "sig_out"]
    },
    {
      "id": "U1",
      "type": "opamp",
      "pins": {
        "in-": "inv_node",
        "in+": "gnd",
        "out": "sig_out"
      }
    },
    {
      "id": "Rload",
      "type": "resistor",
      "value": "2k",
      "nodes": ["sig_out", "gnd"]
    },
    {
      "id": "GND",
      "type": "ground",
      "nodes": ["gnd"]
    }
  ]
}
```

---

## 3. Full-Wave Diode Bridge Rectifier with Filter

Converts 12V RMS AC into smoothed DC with a 470µF reservoir capacitor.

```json
{
  "name": "Full-Wave Bridge Rectifier",
  "description": "Diode bridge with reservoir capacitor smoothing",
  "components": [
    {
      "id": "Vac",
      "type": "vsource",
      "waveform": "sine",
      "amplitude": "12V",
      "frequency": "60Hz",
      "pins": { "+": "ac1", "-": "ac2" }
    },
    {
      "id": "D1",
      "type": "diode",
      "pins": { "anode": "ac1", "cathode": "dc_pos" }
    },
    {
      "id": "D2",
      "type": "diode",
      "pins": { "anode": "gnd", "cathode": "ac1" }
    },
    {
      "id": "D3",
      "type": "diode",
      "pins": { "anode": "ac2", "cathode": "dc_pos" }
    },
    {
      "id": "D4",
      "type": "diode",
      "pins": { "anode": "gnd", "cathode": "ac2" }
    },
    {
      "id": "C_filter",
      "type": "capacitor",
      "value": "470uF",
      "nodes": ["dc_pos", "gnd"]
    },
    {
      "id": "R_bleed",
      "type": "resistor",
      "value": "1k",
      "nodes": ["dc_pos", "gnd"]
    },
    {
      "id": "GND",
      "type": "ground",
      "nodes": ["gnd"]
    }
  ]
}
```

---

## 4. 555 Timer Astable Square-Wave Oscillator

A classic NE555 multivibrator generating square pulses.

```json
{
  "name": "555 Timer Oscillator",
  "description": "Astable 555 pulse generator operating at 9V DC",
  "components": [
    {
      "id": "Vcc",
      "type": "vsource",
      "value": "9V",
      "parameters": { "voltage": 9.0 },
      "pins": { "+": "vcc", "-": "gnd" }
    },
    {
      "id": "U1",
      "type": "timer555",
      "pins": {
        "gnd": "gnd",
        "trig": "timing_net",
        "thresh": "timing_net",
        "out": "pulse_out",
        "reset": "vcc",
        "ctrl": "ctrl_net",
        "disch": "disch_net",
        "vcc": "vcc"
      }
    },
    {
      "id": "R1",
      "type": "resistor",
      "value": "10k",
      "nodes": ["vcc", "disch_net"]
    },
    {
      "id": "R2",
      "type": "resistor",
      "value": "47k",
      "nodes": ["disch_net", "timing_net"]
    },
    {
      "id": "C_timing",
      "type": "capacitor",
      "value": "10uF",
      "nodes": ["timing_net", "gnd"]
    },
    {
      "id": "C_ctrl",
      "type": "capacitor",
      "value": "10nF",
      "nodes": ["ctrl_net", "gnd"]
    },
    {
      "id": "R_load",
      "type": "resistor",
      "value": "1k",
      "nodes": ["pulse_out", "gnd"]
    },
    {
      "id": "GND",
      "type": "ground",
      "nodes": ["gnd"]
    }
  ]
}
```

---

## 5. RLC Resonant Tank Circuit

Second-order bandpass resonance with $f_0 \approx 1.59\text{ kHz}$.

```json
{
  "name": "RLC Resonant Tank",
  "description": "Series RLC tank filter with 10mH inductor and 1uF capacitor",
  "components": [
    {
      "id": "V1",
      "type": "vsource",
      "waveform": "sine",
      "amplitude": "5V",
      "frequency": "1.59kHz",
      "pins": { "+": "vin", "-": "gnd" }
    },
    {
      "id": "R1",
      "type": "resistor",
      "value": "100",
      "nodes": ["vin", "n_mid"]
    },
    {
      "id": "L1",
      "type": "inductor",
      "value": "10mH",
      "nodes": ["n_mid", "vout"]
    },
    {
      "id": "C1",
      "type": "capacitor",
      "value": "1uF",
      "nodes": ["vout", "gnd"]
    },
    {
      "id": "GND",
      "type": "ground",
      "nodes": ["gnd"]
    }
  ]
}
```
