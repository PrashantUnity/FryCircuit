# FryCircuit

FryCircuit is an industrial-grade electronic circuit design, real-time SPICE Modified Nodal Analysis (MNA) simulation, and schematic capture environment built for the **FryWorld** ecosystem.

It features pure C# GPU/particle-accelerated current visualization, live oscilloscope probing, multi-domain physics modeling (cryogenic qubits to aerospace high-temperature electronics), and extensive AI agent integration.

---

## ⚡ Key Features

- **Real-Time SPICE MNA Solver**: Sub-millivolt accuracy nodal matrix solver computing dynamic voltages, branch currents, and non-linear semiconductor responses.
- **21 Built-In Engineering Presets**: From 555 timers and op-amps to Josephson Junctions and SQUID magnetometers.
- **Topological Auto-Router**: Automatically resolves named net connections without requiring manual trace routing.
- **Industry EDA Exporters**: Berkeley SPICE 3f5 / LTspice compatible netlists (`.cir`), Markdown Bill of Materials (BOM), and `.fcircuit` JSON.

---

## 🤖 Model Context Protocol (MCP) Integration

FryCircuit provides 16 tools to autonomous AI agents over the Model Context Protocol (MCP) for direct schematic synthesis, parameter tuning, simulation stepping, and telemetry querying.

See the complete documentation:
- 📖 [MCP Integration Guide](docs/mcp/README.md)
- 📐 [`.fcircuit` Format Specification](docs/mcp/fcircuit-specification.md)
- 🛠️ [Tools Reference](docs/mcp/tools-reference.md)
- ⚡ [Circuit Recipes & Blueprints](docs/mcp/circuit-recipes.md)

---

## 📜 License
MIT License. See [LICENSE](LICENSE) for details.
