# FryCircuit MCP Tools Reference

Complete technical reference, JSON schemas, parameter tables, and request/response examples for all 16 MCP tools exposed by FryCircuit.

---

## 1. `circuit_load_schematic`

Loads or synthesizes a schematic into Circuit Studio using a `.fcircuit` JSON string. Automatically performs topological layout and synthesizes wiring.

### Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `fcircuitJson` | `string` | **Yes** | Complete `.fcircuit` JSON document containing `name` and `components` array. |

### Example Request:
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "circuit_load_schematic",
    "arguments": {
      "fcircuitJson": "{\"name\": \"RC Step\", \"components\": [{\"id\":\"V1\",\"type\":\"vsource\",\"value\":\"5V\",\"pins\":{\\"+\\":\\"vin\\",\\"-\\":\\"gnd\\"}},{\"id\":\"R1\",\"type\":\"resistor\",\"value\":\"1k\",\"nodes\":[\"vin\",\"vout\"]},{\"id\":\"C1\",\"type\":\"capacitor\",\"value\":\"10uF\",\"nodes\":[\"vout\",\"gnd\"]},{\"id\":\"GND\",\"type\":\"ground\",\"nodes\":[\"gnd\"]}]}"
    }
  },
  "id": 1
}
```

### Example Response:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "\"Successfully loaded schematic 'RC Step' with 4 components.\""
      }
    ],
    "isError": false
  }
}
```

---

## 2. `circuit_get_schematic`

Returns the currently loaded schematic formatted as clean `.fcircuit` JSON.

### Parameters:
*None (`{}`)*

### Response:
A valid `.fcircuit` JSON string containing all components, values, coordinates, and net names.

---

## 3. `circuit_new_schematic`

Clears the active canvas and resets Circuit Studio with a blank schematic and a default ground node.

### Parameters:
*None (`{}`)*

### Response:
```json
"Created new blank schematic canvas."
```

---

## 4. `circuit_modify_component`

Dynamically alters a parameter of an existing component in the circuit and triggers SPICE MNA solver re-initialization.

### Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `componentId` | `string` | **Yes** | Device ID or reference designator (e.g. `'1'`, `'R1'`, `'V1'`, `'LED1'`). |
| `parameterName` | `string` | **Yes** | Parameter to modify (e.g. `'resistance'`, `'capacitance'`, `'inductance'`, `'voltage'`, `'frequency'`, `'wiper'`). |
| `value` | `number` | **Yes** | New numerical value for the parameter. |

### Example Request:
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "circuit_modify_component",
    "arguments": {
      "componentId": "R1",
      "parameterName": "resistance",
      "value": 4700.0
    }
  },
  "id": 2
}
```

---

## 5. `circuit_control_simulation`

Controls the real-time simulation engine state and speed.

### Parameters:
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `action` | `string` | No | `"toggle"` | Simulation state: `"start"`, `"pause"`, `"toggle"`, or `"reset"`. |
| `speed` | `number` | No | `null` | Speed multiplier between `0.1` and `4.0` (e.g. `1.0`, `2.0`). |

---

## 6. `circuit_step_simulation`

Advances the MNA simulation engine by a discrete number of time-steps.

### Parameters:
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `steps` | `integer` | No | `10` | Number of simulation steps to compute (1 to 1000). |

---

## 7. `circuit_get_readings`

Queries live telemetry from all electrical nodes and device branches in the circuit.

### Parameters:
*None (`{}`)*

### Response Structure:
```json
{
  "circuit": "RC Step",
  "simTimeMs": 2.50,
  "nodes": [
    { "node": 0, "name": "GND", "isGround": true, "voltage": "0.000 V" },
    { "node": 1, "name": "N1", "isGround": false, "voltage": "5.000 V" },
    { "node": 2, "name": "N2", "isGround": false, "voltage": "3.160 V" }
  ],
  "devices": [
    { "id": 1, "type": "vsource", "parameters": "voltage=5", "current": "1.840 mA" },
    { "id": 2, "type": "resistor", "parameters": "resistance=1000", "current": "1.840 mA" },
    { "id": 3, "type": "capacitor", "parameters": "capacitance=1e-5", "current": "1.840 mA" }
  ]
}
```

---

## 8. `circuit_export`

Exports the active schematic into standard industrial and CAD formats.

### Parameters:
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `format` | `string` | No | `"spice"` | Output format: `'spice'` (SPICE .cir), `'bom'` (Bill of Materials Markdown), or `'fcircuit'` (JSON). |

---

## 9. `circuit_list_presets`

Lists the names of all 21 built-in engineering circuit presets.

### Parameters:
*None (`{}`)*

### Sample Output:
```json
[
  "Half-Wave Rectifier",
  "Resistor Voltage Divider",
  "RC Step Response",
  "LED with Current Limiter",
  "Inverting Op-Amp",
  "555 Timer Oscillator",
  "Transmon Qubit (Quantum JJ)",
  "RLC Resonant Tank"
]
```

---

## 10. `circuit_load_preset`

Loads a built-in engineering preset directly onto the interactive canvas by name.

### Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `presetName` | `string` | **Yes** | Exact name of preset (e.g. `'Inverting Op-Amp'`). |

---

## 11. `circuit_set_tool_mode`

Sets the active interactive canvas mouse/cursor tool mode.

### Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `mode` | `string` | **Yes** | Tool mode: `'Select'`, `'Wire'`, `'Cut'`, `'Pan'`, `'ProbeCh1'`, `'ProbeCh2'`. |

---

## 12. `circuit_zoom_to_fit`

Automatically scales and pans the viewport to fit all components onto the visible screen.

### Parameters:
*None (`{}`)*

---

## 13. `circuit_undo` & 14. `circuit_redo`

Reverts or reapplies canvas schematic editing operations.

### Parameters:
*None (`{}`)*

---

## 15. `circuit_open_file` & 16. `circuit_save_file`

Opens or saves schematic files on disk (`.fcircuit` JSON or `.frycircuit` binary).

### `circuit_open_file` Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `filePath` | `string` | **Yes** | Absolute path to the circuit file. |

### `circuit_save_file` Parameters:
| Parameter | Type | Required | Description |
|---|---|---|---|
| `filePath` | `string` | No | Destination file path. If omitted, saves to current path. |
