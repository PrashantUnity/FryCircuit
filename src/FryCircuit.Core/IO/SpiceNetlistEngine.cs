using System.Globalization;
using System.Text;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.IO;

/// <summary>
/// Professional EDA SPICE Netlist Engine.
/// Provides export and import compatibility with Berkeley SPICE 3f5, LTspice, ngspice, and WRSPICE
/// for cryogenic quantum devices, aerospace radiation pulses, transmission lines, and classical electronics.
/// </summary>
public static class SpiceNetlistEngine
{
    /// <summary>
    /// Exports a Circuit into standard SPICE netlist syntax (.cir / .sp).
    /// </summary>
    public static string ExportSpice(Circuit circuit, double tempKelvin = 298.15, string title = "FryCircuit Industrial EDA Netlist")
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine($"* {title}");
        sb.AppendLine($"* Circuit: {circuit.Name} (v{circuit.Version})");
        sb.AppendLine($"* Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"* Physical Thermal Anchor: {tempKelvin:F2} K ({tempKelvin - 273.15:F2} deg C)");
        sb.AppendLine($"* Thermal Voltage Vt = {PhysicalConstants.ThermalVoltage(tempKelvin) * 1000.0:F4} mV");
        sb.AppendLine();

        // SPICE Temperature Directive
        sb.AppendLine($".TEMP {tempKelvin - 273.15:F2}");
        sb.AppendLine();

        // Map node IDs to SPICE node names
        string GetNodeName(int nodeId)
        {
            if (circuit.Nodes.TryGetValue(nodeId, out var node) && node.IsGround)
            {
                return "0";
            }
            if (nodeId == 0) return "0";
            return $"N{nodeId}";
        }

        bool hasDiode = false;
        bool hasNmos = false;
        bool hasPmos = false;
        bool has555 = false;

        sb.AppendLine("* --- Components ---");
        foreach (var dev in circuit.Devices.OrderBy(d => d.Id))
        {
            switch (dev)
            {
                case ResistorDevice r:
                {
                    string n1 = GetNodeName(r.Pins[0].NodeId);
                    string n2 = GetNodeName(r.Pins[1].NodeId);
                    sb.AppendLine($"R{r.Id} {n1} {n2} {r.Resistance.ToString("G6", inv)}");
                    break;
                }
                case CapacitorDevice c:
                {
                    string n1 = GetNodeName(c.Pins[0].NodeId);
                    string n2 = GetNodeName(c.Pins[1].NodeId);
                    sb.AppendLine($"C{c.Id} {n1} {n2} {c.Capacitance.ToString("G6", inv)}");
                    break;
                }
                case InductorDevice l:
                {
                    string n1 = GetNodeName(l.Pins[0].NodeId);
                    string n2 = GetNodeName(l.Pins[1].NodeId);
                    sb.AppendLine($"L{l.Id} {n1} {n2} {l.Inductance.ToString("G6", inv)}");
                    break;
                }
                case DiodeDevice d:
                {
                    hasDiode = true;
                    string a = GetNodeName(d.Pins[0].NodeId);
                    string k = GetNodeName(d.Pins[1].NodeId);
                    sb.AppendLine($"D{d.Id} {a} {k} DMOD_{d.Id}");
                    sb.AppendLine($"* DMOD_{d.Id}: Is={d.SaturationCurrent:E3}A, N={d.EmissionCoefficient:F2}, Cjo={d.ZeroBiasCapacitance:E3}F");
                    break;
                }
                case VoltageSourceDevice vs:
                {
                    string p = GetNodeName(vs.Pins[0].NodeId);
                    string n = GetNodeName(vs.Pins[1].NodeId);
                    if (vs.WaveformType == VoltageWaveformType.Sine)
                    {
                        sb.AppendLine($"V{vs.Id} {p} {n} SINE({vs.Offset.ToString("G5", inv)} {vs.Amplitude.ToString("G5", inv)} {vs.Frequency.ToString("G5", inv)})");
                    }
                    else if (vs.WaveformType == VoltageWaveformType.Pulse)
                    {
                        double period = 1.0 / Math.Max(1.0, vs.Frequency);
                        double pulseWidth = period * 0.5;
                        sb.AppendLine($"V{vs.Id} {p} {n} PULSE(0 {vs.Amplitude.ToString("G5", inv)} 0 10p 10p {pulseWidth.ToString("G5", inv)} {period.ToString("G5", inv)})");
                    }
                    else
                    {
                        sb.AppendLine($"V{vs.Id} {p} {n} DC {vs.Voltage.ToString("G5", inv)}");
                    }
                    break;
                }
                case CurrentSourceDevice cs:
                {
                    string p = GetNodeName(cs.Pins[0].NodeId);
                    string n = GetNodeName(cs.Pins[1].NodeId);
                    sb.AppendLine($"I{cs.Id} {p} {n} DC {cs.Current.ToString("G5", inv)}");
                    break;
                }
                case MosfetDevice mos:
                {
                    string g = GetNodeName(mos.Pins[0].NodeId);
                    string d = GetNodeName(mos.Pins[1].NodeId);
                    string s = GetNodeName(mos.Pins[2].NodeId);
                    if (!mos.IsNChannel)
                    {
                        hasPmos = true;
                        sb.AppendLine($"M{mos.Id} {d} {g} {s} {s} PMOS_MOD");
                    }
                    else
                    {
                        hasNmos = true;
                        sb.AppendLine($"M{mos.Id} {d} {g} {s} {s} NMOS_MOD");
                    }
                    break;
                }
                case TransmissionLineDevice tl:
                {
                    string inP = GetNodeName(tl.Pins[0].NodeId);
                    string inN = GetNodeName(tl.Pins[1].NodeId);
                    string outP = GetNodeName(tl.Pins[2].NodeId);
                    string outN = GetNodeName(tl.Pins[3].NodeId);
                    sb.AppendLine($"T{tl.Id} {inP} {inN} {outP} {outN} Z0={tl.CharacteristicImpedance.ToString("G5", inv)} TD={tl.DelaySeconds.ToString("E4", inv)}");
                    break;
                }
                case JosephsonJunctionDevice jj:
                {
                    string p1 = GetNodeName(jj.Pins[0].NodeId);
                    string p2 = GetNodeName(jj.Pins[1].NodeId);
                    sb.AppendLine($"* Superconducting Josephson Junction (WRSPICE / PSCAN Macro):");
                    sb.AppendLine($"B{jj.Id} {p1} {p2} JJ_MOD IC={jj.CriticalCurrent.ToString("E4", inv)} RN={jj.NormalResistance.ToString("G5", inv)} CAP={jj.JunctionCapacitance.ToString("E4", inv)}");
                    break;
                }
                case SquidDevice sq:
                {
                    string p1 = GetNodeName(sq.Pins[0].NodeId);
                    string p2 = GetNodeName(sq.Pins[1].NodeId);
                    sb.AppendLine($"* DC SQUID Quantum Magnetometer (Modulated Ic={sq.ModulatedCriticalCurrent:E3} A, Flux={sq.ExternalFluxNormalized:F3} Phi0):");
                    sb.AppendLine($"B_SQ{sq.Id} {p1} {p2} SQUID_MOD IC={sq.ModulatedCriticalCurrent.ToString("E4", inv)} RN={sq.NormalResistance.ToString("G5", inv)} CAP={sq.TotalCapacitance.ToString("E4", inv)}");
                    break;
                }
                case CosmicIonPulseDevice ion:
                {
                    string p1 = GetNodeName(ion.Pins[0].NodeId);
                    string p2 = GetNodeName(ion.Pins[1].NodeId);
                    sb.AppendLine($"* Aerospace Heavy-Ion Radiation Strike SEU Source (Qcoll={ion.CollectedCharge * 1e15:F2} fC):");
                    sb.AppendLine($"I_ION{ion.Id} {p1} {p2} EXP(0 {ion.PeakCurrent.ToString("E4", inv)} {ion.StrikeTimeSeconds.ToString("E4", inv)} {ion.RiseTimeSeconds.ToString("E4", inv)} {(ion.StrikeTimeSeconds + ion.RiseTimeSeconds).ToString("E4", inv)} {ion.FallTimeSeconds.ToString("E4", inv)})");
                    break;
                }
                case TunnelDiodeDevice td:
                {
                    string a = GetNodeName(td.Pins[0].NodeId);
                    string k = GetNodeName(td.Pins[1].NodeId);
                    sb.AppendLine($"* Quantum Tunnel (Esaki) Diode with Negative Differential Resistance:");
                    sb.AppendLine($"D_TD{td.Id} {a} {k} TD_MOD (Ip={td.PeakCurrent:E3} Vp={td.PeakVoltage:F3} Iv={td.ValleyCurrent:E3} Vv={td.ValleyVoltage:F3})");
                    break;
                }
                case HallEffectSensorDevice hs:
                {
                    string bP = GetNodeName(hs.Pins[0].NodeId);
                    string bN = GetNodeName(hs.Pins[1].NodeId);
                    string oP = GetNodeName(hs.Pins[2].NodeId);
                    string oN = GetNodeName(hs.Pins[3].NodeId);
                    sb.AppendLine($"* Hall Effect Magnetic Sensor (Sensitivity={hs.SensitivityVoltsPerTesla:F2} V/T, Bz={hs.MagneticFieldTesla * 1000:F1} mT):");
                    sb.AppendLine($"R_BIAS{hs.Id} {bP} {bN} {hs.InputResistance:F1}");
                    sb.AppendLine($"E_HALL{hs.Id} {oP} {oN} VALUE = {{ (V({bP}) - V({bN})) * {hs.SensitivityVoltsPerTesla:E4} * {hs.MagneticFieldTesla:E4} }}");
                    break;
                }
                case BatteryCellDevice bat:
                {
                    string p = GetNodeName(bat.Pins[0].NodeId);
                    string n = GetNodeName(bat.Pins[1].NodeId);
                    sb.AppendLine($"* Lithium-Ion / LiPo Electrochemical Battery Cell (SOC={bat.StateOfCharge * 100:F1}%, Cap={bat.CapacityAmpHours:F2}Ah):");
                    sb.AppendLine($"V_BAT{bat.Id} {p} N_INT_{bat.Id} DC {bat.OpenCircuitVoltage.ToString("G5", inv)}");
                    sb.AppendLine($"R_ESR{bat.Id} N_INT_{bat.Id} {n} {bat.NominalSeriesResistance.ToString("G5", inv)}");
                    break;
                }
                case BjtDevice bjt:
                {
                    string b = GetNodeName(bjt.Pins[0].NodeId);
                    string c = GetNodeName(bjt.Pins[1].NodeId);
                    string e = GetNodeName(bjt.Pins[2].NodeId);
                    string modName = bjt.IsNpn ? $"NPN_MOD_{bjt.Id}" : $"PNP_MOD_{bjt.Id}";
                    sb.AppendLine($"Q{bjt.Id} {c} {b} {e} {modName}");
                    sb.AppendLine($".MODEL {modName} {(bjt.IsNpn ? "NPN" : "PNP")} (BF={bjt.BetaF.ToString("G5", inv)} BR={bjt.BetaR.ToString("G5", inv)} IS={bjt.Isat.ToString("E3", inv)} VAF={bjt.EarlyVoltage.ToString("G5", inv)})");
                    break;
                }
                case OpAmpDevice op:
                {
                    string inN = GetNodeName(op.Pins[0].NodeId);
                    string inP = GetNodeName(op.Pins[1].NodeId);
                    string outPin = GetNodeName(op.Pins[2].NodeId);
                    sb.AppendLine($"* Operational Amplifier (Gain={op.Gain:G4}, Rails=[{op.MinVoltage:F1}V, {op.MaxVoltage:F1}V]):");
                    sb.AppendLine($"E{op.Id} {outPin} 0 {inP} {inN} {op.Gain.ToString("G6", inv)}");
                    break;
                }
                case FuseDevice fuse:
                {
                    string n1 = GetNodeName(fuse.Pins[0].NodeId);
                    string n2 = GetNodeName(fuse.Pins[1].NodeId);
                    double r = fuse.IsBlown ? 1e12 : fuse.InternalResistance;
                    sb.AppendLine($"R_FUSE{fuse.Id} {n1} {n2} {r.ToString("G5", inv)} ; Rating={fuse.CurrentRating:F2}A, Blown={fuse.IsBlown}");
                    break;
                }
                case SwitchDevice sw:
                {
                    if (sw.Type.Equals("spdt", StringComparison.OrdinalIgnoreCase) && sw.Pins.Count >= 3)
                    {
                        string pCommon = GetNodeName(sw.Pins[0].NodeId);
                        string pTh1 = GetNodeName(sw.Pins[1].NodeId);
                        string pTh2 = GetNodeName(sw.Pins[2].NodeId);
                        sb.AppendLine($"R_SW1_{sw.Id} {pCommon} {pTh1} {(sw.IsClosed ? 1e-4 : 1e12).ToString("G5", inv)}");
                        sb.AppendLine($"R_SW2_{sw.Id} {pCommon} {pTh2} {(sw.IsClosed ? 1e12 : 1e-4).ToString("G5", inv)}");
                    }
                    else if (sw.Pins.Count >= 2)
                    {
                        string n1 = GetNodeName(sw.Pins[0].NodeId);
                        string n2 = GetNodeName(sw.Pins[1].NodeId);
                        sb.AppendLine($"R_SW{sw.Id} {n1} {n2} {(sw.IsClosed ? 1e-4 : 1e12).ToString("G5", inv)}");
                    }
                    break;
                }
                case Timer555Device t555:
                {
                    has555 = true;
                    string gnd = GetNodeName(t555.Pins[0].NodeId);
                    string trig = GetNodeName(t555.Pins[1].NodeId);
                    string outp = GetNodeName(t555.Pins[2].NodeId);
                    string rst = GetNodeName(t555.Pins[3].NodeId);
                    string ctrl = GetNodeName(t555.Pins[4].NodeId);
                    string thresh = GetNodeName(t555.Pins[5].NodeId);
                    string disch = GetNodeName(t555.Pins[6].NodeId);
                    string vcc = GetNodeName(t555.Pins[7].NodeId);
                    sb.AppendLine($"X555_{t555.Id} {gnd} {trig} {outp} {rst} {ctrl} {thresh} {disch} {vcc} NE555");
                    break;
                }
                case NetLabelDevice nl:
                {
                    string n = GetNodeName(nl.Pins[0].NodeId);
                    sb.AppendLine($"* NetLabel \"{nl.NetName}\" -> {n}");
                    break;
                }
                case GroundDevice gnd:
                {
                    // Ground references are handled via node 0
                    break;
                }
                default:
                    sb.AppendLine($"* Device {dev.Type} (Id={dev.Id})");
                    break;
            }
        }

        sb.AppendLine();
        sb.AppendLine("* --- Semiconductor Device Models ---");
        if (hasDiode)
        {
            sb.AppendLine(".MODEL DMOD D (IS=1e-14 RS=0.1 N=1.0 CJO=2pF BV=100)");
        }
        if (hasNmos)
        {
            sb.AppendLine(".MODEL NMOS_MOD NMOS (VTO=1.2 KP=0.05 LAMBDA=0.01 CGSO=10p CGDO=5p)");
        }
        if (hasPmos)
        {
            sb.AppendLine(".MODEL PMOS_MOD PMOS (VTO=-1.2 KP=0.02 LAMBDA=0.02 CGSO=10p CGDO=5p)");
        }
        if (has555)
        {
            sb.AppendLine();
            sb.AppendLine("* --- NE555 Behavioral Macromodel ---");
            sb.AppendLine(".SUBCKT NE555 1 2 3 4 5 6 7 8");
            sb.AppendLine("* 1:GND 2:TRIG 3:OUT 4:RESET 5:CTRL 6:THRESH 7:DISCH 8:VCC");
            sb.AppendLine("R1 8 5 5k");
            sb.AppendLine("R2 5 N_INT_555_2 5k");
            sb.AppendLine("R3 N_INT_555_2 1 5k");
            sb.AppendLine(".ENDS NE555");
        }

        sb.AppendLine();
        sb.AppendLine("* --- Simulation Analysis Control ---");
        sb.AppendLine(".TRAN 10p 10u");
        sb.AppendLine(".OPTIONS RELTOL=1e-5 ABSTOL=1e-12 VNTOL=1e-6 CHGTOL=1e-15");
        sb.AppendLine(".END");

        return sb.ToString();
    }

    /// <summary>
    /// Parses a standard SPICE netlist string into a live Circuit instance.
    /// </summary>
    public static Circuit ImportSpice(string netlistText, string circuitName = "Imported SPICE Circuit")
    {
        var circuit = new Circuit(circuitName);
        var nodeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Ground is always node 0
        nodeMap["0"] = 0;
        nodeMap["GND"] = 0;
        circuit.GetOrCreateNode(0, isGround: true);

        int nextNodeId = 1;
        int GetOrCreateNodeId(string nodeName)
        {
            nodeName = nodeName.Trim();
            if (nodeMap.TryGetValue(nodeName, out int existingId))
            {
                return existingId;
            }
            int newId = nextNodeId++;
            nodeMap[nodeName] = newId;
            circuit.GetOrCreateNode(newId);
            return newId;
        }

        var lines = netlistText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        int devId = 1;
        int gridX = 2;
        int gridY = 2;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("*") || line.StartsWith(";")) continue;
            if (line.StartsWith(".")) continue; // Ignore directives in component pass

            var tokens = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3) continue;

            string name = tokens[0].ToUpperInvariant();
            char devTypeChar = name[0];

            try
            {
                switch (devTypeChar)
                {
                    case 'R': // Resistor: R1 N1 N2 VAL
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        double resVal = ParseSpiceValue(tokens[3]);
                        var res = new ResistorDevice(devId++, x: gridX, y: gridY, resistance: resVal);
                        res.Pins[0].NodeId = n1;
                        res.Pins[1].NodeId = n2;
                        circuit.AddDevice(res);
                        break;
                    }
                    case 'C': // Capacitor: C1 N1 N2 VAL
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        double capVal = ParseSpiceValue(tokens[3]);
                        var cap = new CapacitorDevice(devId++, x: gridX, y: gridY, capacitance: capVal);
                        cap.Pins[0].NodeId = n1;
                        cap.Pins[1].NodeId = n2;
                        circuit.AddDevice(cap);
                        break;
                    }
                    case 'L': // Inductor: L1 N1 N2 VAL
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        double indVal = ParseSpiceValue(tokens[3]);
                        var ind = new InductorDevice(devId++, x: gridX, y: gridY, inductance: indVal);
                        ind.Pins[0].NodeId = n1;
                        ind.Pins[1].NodeId = n2;
                        circuit.AddDevice(ind);
                        break;
                    }
                    case 'D': // Diode: D1 N1 N2 [MODEL]
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        var diode = new DiodeDevice(devId++, x: gridX, y: gridY);
                        diode.Pins[0].NodeId = n1;
                        diode.Pins[1].NodeId = n2;
                        circuit.AddDevice(diode);
                        break;
                    }
                    case 'V': // Voltage Source: V1 N+ N- [DC] VAL
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        double voltVal = 5.0;
                        for (int i = 3; i < tokens.Length; i++)
                        {
                            if (double.TryParse(tokens[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                voltVal = parsed;
                                break;
                            }
                            if (tokens[i].Equals("DC", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
                            {
                                voltVal = ParseSpiceValue(tokens[i + 1]);
                                break;
                            }
                        }
                        var vs = new VoltageSourceDevice(devId++, x: gridX, y: gridY, voltage: voltVal);
                        vs.Pins[0].NodeId = n1;
                        vs.Pins[1].NodeId = n2;
                        circuit.AddDevice(vs);
                        break;
                    }
                    case 'I': // Current Source: I1 N+ N- [DC] VAL
                    {
                        int n1 = GetOrCreateNodeId(tokens[1]);
                        int n2 = GetOrCreateNodeId(tokens[2]);
                        double curVal = 0.001;
                        for (int i = 3; i < tokens.Length; i++)
                        {
                            if (double.TryParse(tokens[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                curVal = parsed;
                                break;
                            }
                            if (tokens[i].Equals("DC", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
                            {
                                curVal = ParseSpiceValue(tokens[i + 1]);
                                break;
                            }
                        }
                        var cs = new CurrentSourceDevice(devId++, x: gridX, y: gridY, current: curVal);
                        cs.Pins[0].NodeId = n1;
                        cs.Pins[1].NodeId = n2;
                        circuit.AddDevice(cs);
                        break;
                    }
                    case 'M': // MOSFET: M1 ND NG NS NB MODEL
                    {
                        if (tokens.Length >= 4)
                        {
                            int nd = GetOrCreateNodeId(tokens[1]);
                            int ng = GetOrCreateNodeId(tokens[2]);
                            int ns = GetOrCreateNodeId(tokens[3]);
                            bool isPmos = tokens.Length > 5 && tokens[5].ToUpperInvariant().Contains("PMOS");
                            var mos = new MosfetDevice(devId++, isPmos ? "pmos" : "nmos", x: gridX, y: gridY);
                            mos.Pins[0].NodeId = ng;
                            mos.Pins[1].NodeId = nd;
                            mos.Pins[2].NodeId = ns;
                            circuit.AddDevice(mos);
                        }
                        break;
                    }
                    case 'T': // Transmission line: T1 N1+ N1- N2+ N2- Z0=50 TD=1n
                    {
                        if (tokens.Length >= 5)
                        {
                            int inP = GetOrCreateNodeId(tokens[1]);
                            int inN = GetOrCreateNodeId(tokens[2]);
                            int outP = GetOrCreateNodeId(tokens[3]);
                            int outN = GetOrCreateNodeId(tokens[4]);
                            double z0 = 50.0;
                            double td = 1e-9;
                            for (int i = 5; i < tokens.Length; i++)
                            {
                                string t = tokens[i].ToUpperInvariant();
                                if (t.StartsWith("Z0=")) z0 = ParseSpiceValue(t[3..]);
                                if (t.StartsWith("TD=")) td = ParseSpiceValue(t[3..]);
                            }
                            var tl = new TransmissionLineDevice(devId++, x: gridX, y: gridY)
                            {
                                CharacteristicImpedance = z0,
                                DelaySeconds = td
                            };
                            tl.Pins[0].NodeId = inP;
                            tl.Pins[1].NodeId = inN;
                            tl.Pins[2].NodeId = outP;
                            tl.Pins[3].NodeId = outN;
                            circuit.AddDevice(tl);
                        }
                        break;
                    }
                    case 'B': // Josephson Junction (WRSPICE)
                    {
                        int p1 = GetOrCreateNodeId(tokens[1]);
                        int p2 = GetOrCreateNodeId(tokens[2]);
                        double ic = 10e-6;
                        double rn = 100.0;
                        double cap = 50e-15;
                        for (int i = 3; i < tokens.Length; i++)
                        {
                            string t = tokens[i].ToUpperInvariant();
                            if (t.StartsWith("IC=")) ic = ParseSpiceValue(t[3..]);
                            if (t.StartsWith("RN=")) rn = ParseSpiceValue(t[3..]);
                            if (t.StartsWith("CAP=")) cap = ParseSpiceValue(t[4..]);
                        }
                        var jj = new JosephsonJunctionDevice(devId++, x: gridX, y: gridY)
                        {
                            CriticalCurrent = ic,
                            NormalResistance = rn,
                            JunctionCapacitance = cap
                        };
                        jj.Pins[0].NodeId = p1;
                        jj.Pins[1].NodeId = p2;
                        circuit.AddDevice(jj);
                        break;
                    }
                }

                // Layout grid stepping
                gridX += 3;
                if (gridX > 16)
                {
                    gridX = 2;
                    gridY += 3;
                }
            }
            catch
            {
                // Resilient parser continues on individual component error
            }
        }

        // Add visual ground device at node 0 if referenced
        if (nodeMap.ContainsKey("0") || nodeMap.ContainsKey("GND"))
        {
            var gndDev = new GroundDevice(devId++, x: gridX, y: gridY + 2);
            gndDev.Pins[0].NodeId = 0;
            circuit.AddDevice(gndDev);
        }

        circuit.Frame = new CircuitFrame(0, 0, Math.Max(18, gridX + 4), Math.Max(14, gridY + 6));
        return circuit;
    }

    /// <summary>
    /// Parses standard SPICE engineering metric unit suffixes (p, n, u, m, k, meg, g, t).
    /// </summary>
    public static double ParseSpiceValue(string str)
    {
        str = str.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(str)) return 0.0;

        double multiplier = 1.0;
        int suffixLen = 0;

        if (str.EndsWith("MEG")) { multiplier = 1e6; suffixLen = 3; }
        else if (str.EndsWith("MIL")) { multiplier = 25.4e-6; suffixLen = 3; }
        else if (str.EndsWith("F")) { multiplier = 1e-15; suffixLen = 1; }
        else if (str.EndsWith("P")) { multiplier = 1e-12; suffixLen = 1; }
        else if (str.EndsWith("N")) { multiplier = 1e-9; suffixLen = 1; }
        else if (str.EndsWith("U")) { multiplier = 1e-6; suffixLen = 1; }
        else if (str.EndsWith("M")) { multiplier = 1e-3; suffixLen = 1; }
        else if (str.EndsWith("K")) { multiplier = 1e3; suffixLen = 1; }
        else if (str.EndsWith("G")) { multiplier = 1e9; suffixLen = 1; }
        else if (str.EndsWith("T")) { multiplier = 1e12; suffixLen = 1; }

        string numPart = suffixLen > 0 ? str[..^suffixLen] : str;
        if (double.TryParse(numPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
        {
            return val * multiplier;
        }

        return 0.0;
    }
}
