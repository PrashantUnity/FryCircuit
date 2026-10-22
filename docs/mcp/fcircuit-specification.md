# `.fcircuit` JSON Format Specification

The `.fcircuit` format is a lightweight, human- and LLM-optimized JSON specification for representing electronic circuits, netlists, component parameters, and physical layout topology. It is designed so that autonomous AI agents can construct, inspect, and modify circuits without needing specialized CAD knowledge.

---

## 1. Top-Level Structure

A valid `.fcircuit` document is a UTF-8 JSON object containing metadata and a `components` array:

```json
{
  "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
  "version": 1,
  "name": "Circuit Name",
  "description": "Optional human-readable description of function.",
  "options": {
    "speed": 1.0,
    "logicHigh": 5.0
  },
  "components": [
    ...
  ]
}
```

### Top-Level Fields:
| Field | Type | Required | Description |
|---|---|---|---|
| `$schema` | `string` | No | Schema URI identifier. |
| `version` | `integer` | No | Schema version (default: `1`). |
| `name` | `string` | **Yes** | Descriptive title of the circuit. |
| `description` | `string` | No | Operational notes or design intent. |
| `options.speed` | `number` | No | Simulation speed multiplier (0.1 to 10.0, default: `1.0`). |
| `options.logicHigh`| `number` | No | Digital logic high voltage threshold in Volts (default: `5.0`). |
| `components` | `array` | **Yes** | Array of component descriptors. (Alias: `devices`). |

---

## 2. Component Descriptor Format

Each object in the `components` array represents an electronic device:

```json
{
  "id": "R1",
  "type": "resistor",
  "value": "10k",
  "nodes": ["netA", "netB"]
}
```

### Component Fields:
| Field | Type | Description |
|---|---|---|
| `id` | `string` or `number` | Unique reference designator (e.g. `"R1"`, `"V1"`, `"C_filter"`, `10`). |
| `type` | `string` | Component class identifier (e.g. `"resistor"`, `"vsource"`, `"opamp"`). Case-insensitive. |
| `value` | `string` or `number` | Primary physical value using engineering notation (e.g. `"10k"`, `"100nF"`, `"12V"`). |
| `nodes` | `string[]` | Ordered array of connected net names. Used for 2-terminal components (`[pin1, pin2]`). |
| `pins` | `object` | Semantic pin-to-net mapping. Recommended for multi-terminal components (`{"in+": "net1", ...}`). |
| `parameters` | `object` | Secondary physical properties (e.g. `{"waveform": "sine", "frequency": "1kHz"}`). |
| `x`, `y` | `number` | Optional canvas grid coordinates (auto-routed if omitted). |
| `rotation` | `string` | Optional rotation (`"up"`, `"down"`, `"left"`, `"right"`). |

---

## 3. Supported Component Catalog

### Passives & 2-Terminal Devices
| Type | Value Unit | Parameters | Connection Syntax |
|---|---|---|---|
| `resistor` | Ohms (`Ω`) | `tolerance` | `"nodes": ["n1", "n2"]` |
| `capacitor` | Farads (`F`) | `initialVoltage` | `"nodes": ["n1", "n2"]` |
| `inductor` | Henries (`H`) | `initialCurrent` | `"nodes": ["n1", "n2"]` |
| `switch` | State | `closed` (`true`/`false`) | `"nodes": ["n1", "n2"]` |
| `fuse` | Amperes (`A`) | `rating` | `"nodes": ["n1", "n2"]` |
| `lamp` | Watts (`W`) | `voltage` | `"nodes": ["n1", "n2"]` |
| `ground` | — | — | `"nodes": ["gnd"]` *(Mandatory in every circuit)* |

### Sources & Power
| Type | Default Value | Parameters | Connection Syntax |
|---|---|---|---|
| `vsource` | `10V` | `waveform` (`"dc"`, `"sine"`, `"square"`, `"triangle"`, `"pulse"`), `amplitude` (`"10V"`), `frequency` (`"1kHz"`), `offset` (`"0V"`) | `"pins": {"+": "vcc", "-": "gnd"}` |
| `battery` | `9V` | `internalResistance` | `"pins": {"+": "vcc", "-": "gnd"}` |
| `csource` | `1mA` | `frequency` | `"pins": {"+": "n1", "-": "n2"}` |

### Semiconductors & Active Devices
| Type | Semantic Pin Names | Parameters | Example Pin Mapping |
|---|---|---|---|
| `diode` | `anode` (+), `cathode` (-) | `breakdownVoltage`, `model` | `{"anode": "in", "cathode": "out"}` |
| `led` | `anode` (+), `cathode` (-) | `color` (`"red"`, `"green"`, `"blue"`), `forwardVoltage` | `{"anode": "vcc", "cathode": "k"}` |
| `zener` | `anode` (+), `cathode` (-) | `zenerVoltage` | `{"anode": "gnd", "cathode": "vreg"}` |
| `opamp` | `in-` (inv), `in+` (noninv), `out` | `gain`, `saturateVoltage` | `{"in-": "inv", "in+": "gnd", "out": "vout"}` |
| `npn` | `base` (b), `collector` (c), `emitter` (e) | `beta` (hFE, default: `100`) | `{"base": "b", "collector": "vcc", "emitter": "e"}` |
| `pnp` | `base` (b), `collector` (c), `emitter` (e) | `beta` (hFE) | `{"base": "b", "collector": "gnd", "emitter": "vcc"}` |
| `nmos` | `gate` (g), `drain` (d), `source` (s), `bulk` (b) | `thresholdVoltage`, `transconductance` | `{"gate": "in", "drain": "out", "source": "gnd"}` |
| `pmos` | `gate` (g), `drain` (d), `source` (s), `bulk` (b) | `thresholdVoltage` | `{"gate": "in", "drain": "out", "source": "vcc"}` |

### Integrated Circuits & Functional Modules
| Type | Semantic Pin Names | Parameters | Example Pin Mapping |
|---|---|---|---|
| `timer555` | `gnd`, `trig`, `out`, `reset`, `ctrl`, `thresh`, `disch`, `vcc` | — | `{"vcc": "vcc", "gnd": "gnd", "trig": "t", "thresh": "t", "out": "sq_out", "disch": "d", "reset": "vcc"}` |
| `potentiometer` | `t1` (in), `wiper` (out), `t2` (gnd) | `wiper` (0.0 to 1.0) | `{"t1": "vcc", "wiper": "vout", "t2": "gnd"}` |

### Digital Logic Gates
| Type | Input Pins | Output Pin | Logic Levels |
|---|---|---|---|
| `and`, `or`, `nand`, `nor`, `xor` | `in1`, `in2` | `out` | 0V = Low, 5V (or `logicHigh`) = High |
| `not` (Inverter) | `in` | `out` | Inverts logic state |

---

## 4. Engineering Unit Parser Rules

All values can be written using standard electrical engineering notation without needing exponential conversion:

| Suffix | Multiplier | Example Input | Parsed Value |
|---|---|---|---|
| `p` / `pico` | $10^{-12}$ | `"100pF"` | $100 \times 10^{-12} \text{ F}$ |
| `n` / `nano` | $10^{-9}$ | `"47nF"` | $47 \times 10^{-9} \text{ F}$ |
| `u` / `µ` / `micro` | $10^{-6}$ | `"10uF"` or `"10µF"` | $10 \times 10^{-6} \text{ F}$ |
| `m` / `milli` | $10^{-3}$ | `"2.5ms"`, `"10mH"` | $0.0025 \text{ s}$, $0.01 \text{ H}$ |
| *(none)* | $1.0$ | `"100"`, `"5V"` | $100.0$, $5.0 \text{ V}$ |
| `k` / `kilo` | $10^{3}$ | `"10k"`, `"4.7kΩ"` | $10000.0 \text{ }\Omega$ |
| `M` / `MEG` | $10^{6}$ | `"1M"`, `"10MEG"` | $10^{6}$, $10^{7}$ |
| `G` / `giga` | $10^{9}$ | `"1GHz"` | $10^{9} \text{ Hz}$ |
| `%` | $0.01$ | `"75%"` | $0.75$ |

*Note: Suffixes are case-insensitive. Trailing units (`V`, `Hz`, `F`, `H`, `A`, `Ω`, `Ohm`) are automatically stripped.*

---

## 5. Topological Wiring & Ground Reference Rules

1. **Ground Reference (`gnd`)**:
   Every solvable circuit must have at least one ground reference. Recognized ground net names are: `"gnd"`, `"ground"`, `"0"`, `"earth"`, `"vss"`.
   A dedicated `ground` device (e.g. `{"id": "GND", "type": "ground", "nodes": ["gnd"]}`) should always be included.

2. **Named Nets**:
   Any two component pins sharing the same net name (e.g. `"vin"`, `"vcc"`, `"stage1"`) are automatically wired together by the CAD engine's topological auto-router. Explicit wire coordinates are not required.

3. **Floating Node Warning**:
   Every net must connect to at least two device terminals (or a ground reference) to avoid singular MNA conductance matrices.
