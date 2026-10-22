# FryCircuit Model Context Protocol (MCP) Integration

FryCircuit exposes its industrial schematic canvas, real-time SPICE-grade Modified Nodal Analysis (MNA) solver, component parameter tuner, and netlist export engines to external AI agents and IDEs via the **Model Context Protocol (MCP)**.

When running within **FryWorld**, FryCircuit registers 16 dedicated circuit engineering tools over the host's high-performance HTTP/SSE endpoint at `http://localhost:5242/sse`.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                 MCP Clients & AI Agents                     │
│  (Antigravity IDE, Claude Desktop, Cursor, Custom Scripts)  │
└──────────────────────────────┬──────────────────────────────┘
                               │ JSON-RPC 2.0 / HTTP SSE
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    FryWorld Host Process                    │
│   ┌──────────────────────────────────────────────────────┐  │
│   │   McpHttpSseTransport (http://localhost:5242/sse)    │  │
│   └──────────────────────────┬───────────────────────────┘  │
│                              │ Dispatches JSON-RPC requests │
│                              ▼                              │
│   ┌──────────────────────────────────────────────────────┐  │
│   │         McpServer (Tool & Prompt Registry)           │  │
│   └──────────────────────────┬───────────────────────────┘  │
└──────────────────────────────┼──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                     FryCircuit Plugin                       │
│   ┌──────────────────────────────────────────────────────┐  │
│   │   CircuitAiTools (16 Tools)                          │  │
│   ├──────────────────────────────────────────────────────┤  │
│   │ • Real-Time MNA SPICE Simulation Engine              │  │
│   │ • Topological Schematic Auto-Layout & Routing        │  │
│   │ • Live Nodal Voltages & Branch Current Telemetry     │  │
│   │ • Berkeley SPICE 3f5 Netlist & Markdown BOM Export   │  │
│   │ • Component Parameter Tuning & Null-Balance Solver   │  │
│   └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## Server Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `http://localhost:5242/sse` | `GET` | SSE event stream for live session initiation (`event: endpoint`). |
| `http://localhost:5242/sse` | `POST` | Direct JSON-RPC 2.0 invocation endpoint (synchronous tool execution). |
| `http://localhost:5242/message` | `POST` | JSON-RPC message endpoint for established SSE streaming clients (`?sessionId=...`). |

---

## Client Configuration

### 1. Antigravity IDE (`~/.gemini/config/mcp_config.json`)
```json
{
  "mcpServers": {
    "fryworld": {
      "serverUrl": "http://localhost:5242/sse"
    }
  }
}
```

### 2. Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "frycircuit": {
      "command": "curl",
      "args": ["-s", "-X", "POST", "http://localhost:5242/sse"]
    }
  }
}
```

### 3. Cursor (`.cursor/mcp.json`)
```json
{
  "mcpServers": {
    "fryworld": {
      "url": "http://localhost:5242/sse"
    }
  }
}
```

---

## Complete Tools Catalog (16 Tools)

| Tool Name | Domain | Description |
|---|---|---|
| [`circuit_load_schematic`](tools-reference.md#1-circuit_load_schematic) | CAD Engine | Synthesizes & auto-routes a complete circuit from `.fcircuit` JSON. |
| [`circuit_get_schematic`](tools-reference.md#2-circuit_get_schematic) | CAD Engine | Returns the active schematic formatted as clean `.fcircuit` JSON. |
| [`circuit_new_schematic`](tools-reference.md#3-circuit_new_schematic) | Workspace | Clears canvas and creates a fresh schematic with ground reference. |
| [`circuit_modify_component`](tools-reference.md#4-circuit_modify_component) | Tuning | Modifies component parameters (`resistance`, `voltage`, `frequency`, etc.). |
| [`circuit_control_simulation`](tools-reference.md#5-circuit_control_simulation) | SPICE Solver | Starts, pauses, toggles, or resets simulation, and adjusts speed. |
| [`circuit_step_simulation`](tools-reference.md#6-circuit_step_simulation) | SPICE Solver | Computes a specific number of time-steps (1 to 1000). |
| [`circuit_get_readings`](tools-reference.md#7-circuit_get_readings) | Telemetry | Returns live node voltages and branch currents across all devices. |
| [`circuit_export`](tools-reference.md#8-circuit_export) | EDA Export | Exports active schematic to SPICE netlist (`.cir`), BOM, or `.fcircuit`. |
| [`circuit_list_presets`](tools-reference.md#9-circuit_list_presets) | Library | Lists all 21 built-in engineering circuit presets. |
| [`circuit_load_preset`](tools-reference.md#10-circuit_load_preset) | Library | Loads a built-in engineering preset by name into Circuit Studio. |
| [`circuit_set_tool_mode`](tools-reference.md#11-circuit_set_tool_mode) | Canvas UI | Sets active canvas mode: `Select`, `Wire`, `Cut`, `Pan`, `ProbeCh1`, `ProbeCh2`. |
| [`circuit_zoom_to_fit`](tools-reference.md#12-circuit_zoom_to_fit) | Canvas UI | Centers and auto-scales canvas viewport to fit all components. |
| [`circuit_undo`](tools-reference.md#13-circuit_undo) | History | Reverts the last canvas schematic editing action. |
| [`circuit_redo`](tools-reference.md#14-circuit_redo) | History | Restores the last reverted canvas schematic edit. |
| [`circuit_open_file`](tools-reference.md#15-circuit_open_file) | File System | Opens a `.fcircuit` JSON or `.frycircuit` binary file from disk. |
| [`circuit_save_file`](tools-reference.md#16-circuit_save_file) | File System | Saves the active circuit schematic to disk. |

---

## Quick Example: Creating & Simulating a Circuit

```bash
# 1. Synthesize an RC Low-Pass Filter
curl -s -X POST http://localhost:5242/sse -H "Content-Type: application/json" -d '{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "circuit_load_schematic",
    "arguments": {
      "fcircuitJson": "{\"name\":\"RC Filter\",\"components\":[{\"id\":\"V1\",\"type\":\"vsource\",\"waveform\":\"sine\",\"amplitude\":\"5V\",\"frequency\":\"1kHz\",\"pins\":{\\"+\\":\\"vin\\",\\"-\\":\\"gnd\\"}},{\"id\":\"R1\",\"type\":\"resistor\",\"value\":\"1k\",\"nodes\":[\"vin\",\"vout\"]},{\"id\":\"C1\",\"type\":\"capacitor\",\"value\":\"100nF\",\"nodes\":[\"vout\",\"gnd\"]},{\"id\":\"GND\",\"type\":\"ground\",\"nodes\":[\"gnd\"]}]}"
    }
  },
  "id": 1
}'

# 2. Step the simulation 20 steps
curl -s -X POST http://localhost:5242/sse -H "Content-Type: application/json" -d '{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": { "name": "circuit_step_simulation", "arguments": { "steps": 20 } },
  "id": 2
}'

# 3. Read live voltages & currents
curl -s -X POST http://localhost:5242/sse -H "Content-Type: application/json" -d '{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": { "name": "circuit_get_readings", "arguments": {} },
  "id": 3
}'
```

---

## Detailed Documentation
- [Format Specification (`fcircuit-specification.md`)](fcircuit-specification.md): Formal grammar, component catalog, pin conventions, and unit parser rules.
- [Tools Reference (`tools-reference.md`)](tools-reference.md): Complete schemas, parameter tables, and request/response examples.
- [Circuit Recipes (`circuit-recipes.md`)](circuit-recipes.md): Copy-pasteable JSON blueprints for common analog, digital, and RF circuits.
