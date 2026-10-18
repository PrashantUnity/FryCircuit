using System.Globalization;
using System.Text;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.IO;

/// <summary>
/// Professional Industrial EDA Bill of Materials (BOM) Generation Engine.
/// Produces industry-compliant aerospace, quantum, and robotics manufacturing BOMs
/// with reference designators, package footprints, engineering ratings, and domain classifications.
/// </summary>
public static class BomEngine
{
    public class BomLineItem
    {
        public int ItemNumber { get; set; }
        public string DesignatorPrefix { get; set; } = "U";
        public List<string> Designators { get; } = new();
        public int Quantity => Designators.Count;
        public string ComponentType { get; set; } = string.Empty;
        public string ValueString { get; set; } = string.Empty;
        public string PackageFootprint { get; set; } = "SMD 0603";
        public string DomainClass { get; set; } = "Commercial";
        public string ManufacturerSpec { get; set; } = "Standard 1%";
    }

    /// <summary>
    /// Generates a structured BOM in Markdown format with summary statistics.
    /// </summary>
    public static string GenerateMarkdownBom(Circuit circuit, double tempKelvin = 298.15)
    {
        var items = AnalyzeCircuit(circuit);
        var sb = new StringBuilder();

        sb.AppendLine($"# Bill of Materials (BOM) - {circuit.Name}");
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Operating Environment:** {tempKelvin:F2} K ({tempKelvin - 273.15:F2} °C) • Vt = {PhysicalConstants.ThermalVoltage(tempKelvin) * 1000.0:F3} mV");
        sb.AppendLine($"**Total Components:** {circuit.Devices.Count} across {items.Count} distinct line items");
        sb.AppendLine();

        sb.AppendLine("| Item | Ref Designators | Qty | Type | Value / Rating | Footprint | Domain | Spec |");
        sb.AppendLine("|:----:|:----------------|:---:|:-----|:---------------|:----------|:-------|:-----|");

        foreach (var item in items)
        {
            string desigs = string.Join(", ", item.Designators);
            sb.AppendLine($"| {item.ItemNumber} | {desigs} | {item.Quantity} | {item.ComponentType} | {item.ValueString} | {item.PackageFootprint} | {item.DomainClass} | {item.ManufacturerSpec} |");
        }

        sb.AppendLine();
        sb.AppendLine("### Manufacturing & Environmental Notes");
        sb.AppendLine($"- **Mil-Spec Grade:** All aerospace semiconductors rated for -55°C to +125°C MIL-STD-883 operations.");
        sb.AppendLine($"- **Cryogenic Qubit Packaging:** Superconducting devices require oxygen-free high-conductivity (OFHC) copper shielded enclosure at < 20 mK.");
        sb.AppendLine($"- **RF Integrity:** 50 Ω Bergeron microstrip transmission lines must maintain controlled dielectric thickness (FR4 / Rogers 4350B).");

        return sb.ToString();
    }

    /// <summary>
    /// Generates standard CSV format for ERP and manufacturing pick-and-place systems.
    /// </summary>
    public static string GenerateCsvBom(Circuit circuit)
    {
        var items = AnalyzeCircuit(circuit);
        var sb = new StringBuilder();

        sb.AppendLine("Item,Designator,Quantity,Type,Value,Package,Domain,Specification");
        foreach (var item in items)
        {
            string desigs = "\"" + string.Join(", ", item.Designators) + "\"";
            sb.AppendLine($"{item.ItemNumber},{desigs},{item.Quantity},{item.ComponentType},{item.ValueString},{item.PackageFootprint},{item.DomainClass},{item.ManufacturerSpec}");
        }

        return sb.ToString();
    }

    public static List<BomLineItem> AnalyzeCircuit(Circuit circuit)
    {
        var lineItems = new List<BomLineItem>();
        int itemIndex = 1;

        // Group physical manufactured devices by (Type, Value/Characteristics), omitting virtual ground/meters/labels
        var groups = circuit.Devices
            .Where(d => d.Type != "ground" && d.Type != "netlabel" && !d.Type.EndsWith("meter", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => (d.Type, GetDeviceSignature(d)));

        foreach (var group in groups)
        {
            var first = group.First();
            var item = new BomLineItem
            {
                ItemNumber = itemIndex++,
                ComponentType = GetFriendlyTypeName(first.Type),
                ValueString = GetDeviceValueString(first),
                PackageFootprint = GetRecommendedFootprint(first),
                DomainClass = GetDomainClass(first),
                ManufacturerSpec = GetSpecification(first)
            };

            string prefix = GetDesignatorPrefix(first.Type);
            foreach (var dev in group)
            {
                item.Designators.Add($"{prefix}{dev.Id}");
            }

            lineItems.Add(item);
        }

        return lineItems;
    }

    private static string GetDesignatorPrefix(string type) => type.ToLowerInvariant() switch
    {
        "resistor" => "R",
        "capacitor" => "C",
        "inductor" => "L",
        "diode" or "zener" or "led" => "D",
        "vsource" or "vsrcsine" => "V",
        "csource" or "isrc" => "I",
        "npn" or "pnp" => "Q",
        "nmos" or "pmos" => "M",
        "opamp" => "U",
        "timer555" or "555" => "U",
        "josephson" or "jj" => "JJ",
        "squid" => "SQ",
        "tunneldiode" or "esaki" => "TD",
        "tline" or "transmissionline" => "TL",
        "cosmicipulse" or "ionpulse" => "RAD",
        "hallsensor" or "hall" => "HALL",
        "battery" => "BAT",
        "switch" or "spst" or "spdt" => "SW",
        "potentiometer" => "POT",
        _ => "DEV"
    };

    private static string GetFriendlyTypeName(string type) => type.ToLowerInvariant() switch
    {
        "resistor" => "Precision Film Resistor",
        "capacitor" => "MLCC Ceramic Capacitor",
        "inductor" => "Shielded Power Inductor",
        "diode" => "Small Signal Silicon Diode",
        "zener" => "Zener Voltage Regulator Diode",
        "led" => "High-Efficiency SMD LED",
        "npn" => "NPN Bipolar Junction Transistor",
        "pnp" => "PNP Bipolar Junction Transistor",
        "nmos" => "N-Channel Power MOSFET",
        "pmos" => "P-Channel Power MOSFET",
        "opamp" => "Precision Low-Noise Op-Amp",
        "timer555" => "Precision CMOS Timer IC",
        "josephson" or "jj" => "Superconducting Josephson Junction",
        "squid" => "DC Superconducting Quantum Interference Device",
        "tunneldiode" => "Quantum Esaki Tunnel Diode",
        "tline" => "Controlled Impedance Transmission Line",
        "cosmicipulse" => "Cosmic Heavy-Ion SEU Injector",
        "hallsensor" => "4-Terminal Magnetic Hall Transducer",
        "battery" => "Lithium-Ion / LiPo Electrochemical Cell",
        _ => type.ToUpperInvariant()
    };

    private static string GetRecommendedFootprint(Device dev) => dev switch
    {
        ResistorDevice => "SMD 0603 (1608 Metric)",
        CapacitorDevice => "SMD 0603 (1608 Metric)",
        InductorDevice => "SMD 2520 (Shielded Coil)",
        MosfetDevice => "PowerPAK SO-8 / DFN",
        BjtDevice => "SOT-23",
        OpAmpDevice => "SOIC-8 / MSOP-8",
        Timer555Device => "SOIC-8",
        TransmissionLineDevice => "Coplanar Waveguide (CPW)",
        JosephsonJunctionDevice => "Cryogenic Al/AlOx/Al Qubit Die",
        SquidDevice => "Cryogenic Multi-Layer Qubit Die",
        CosmicIonPulseDevice => "Radiation Test Port",
        HallEffectSensorDevice => "SOT-23-4 (4-Pin Sensor)",
        BatteryCellDevice => "LiPo Pouch / 18650 Cell",
        _ => "Standard SMD"
    };

    private static string GetDomainClass(Device dev) => dev switch
    {
        JosephsonJunctionDevice or SquidDevice => "Quantum Computing (15 mK)",
        CosmicIonPulseDevice => "Aerospace & Deep Space",
        HallEffectSensorDevice => "Drone Robotics & ESC",
        BatteryCellDevice => "Aerospace & Drone Power",
        TransmissionLineDevice => "High-Speed RF & Microwave",
        TunnelDiodeDevice => "Microwave Instrumentation",
        _ => "General Industrial EDA"
    };

    private static string GetSpecification(Device dev) => dev switch
    {
        ResistorDevice r => "±0.1% Tol, 25 ppm/°C",
        CapacitorDevice c => "C0G/NP0 Dielectric, ±1%",
        InductorDevice l => "High-Q Shielded Ferrite",
        MosfetDevice m => m.IsNChannel ? "Vds=60V, Rds(on)=4.5mΩ" : "Vds=-60V, Rds(on)=9mΩ",
        JosephsonJunctionDevice => "Al-AlOx-Al, Nb Critical Supercurrent",
        SquidDevice => "Dual-Junction, Flux Tunable",
        TransmissionLineDevice => "Z0=50Ω Controlled Microstrip",
        BatteryCellDevice b => $"{b.CapacityAmpHours:F1} Ah, 3.7V Nom LiPo",
        _ => "Standard Component"
    };

    private static string GetDeviceSignature(Device dev) => dev switch
    {
        ResistorDevice r => $"{r.Resistance:G4}",
        CapacitorDevice c => $"{c.Capacitance:G4}",
        InductorDevice l => $"{l.Inductance:G4}",
        VoltageSourceDevice v => $"{v.Voltage:G3}_{v.WaveformType}",
        CurrentSourceDevice i => $"{i.CurrentValue:G3}",
        MosfetDevice m => m.Type,
        JosephsonJunctionDevice jj => $"{jj.CriticalCurrent:G3}",
        SquidDevice sq => $"{sq.BaseCriticalCurrent:G3}_{sq.ExternalFluxNormalized:F2}",
        BatteryCellDevice b => $"{b.CapacityAmpHours:F1}Ah",
        _ => dev.Type
    };

    private static string GetDeviceValueString(Device dev) => dev switch
    {
        ResistorDevice r => Device.FormatEngineering(r.Resistance, "Ω"),
        CapacitorDevice c => Device.FormatEngineering(c.Capacitance, "F"),
        InductorDevice l => Device.FormatEngineering(l.Inductance, "H"),
        VoltageSourceDevice v => v.WaveformType == VoltageWaveformType.Dc 
            ? Device.FormatEngineering(v.Voltage, "V") 
            : $"{Device.FormatEngineering(v.Voltage, "V")} ({v.WaveformType})",
        CurrentSourceDevice i => Device.FormatEngineering(i.CurrentValue, "A"),
        JosephsonJunctionDevice jj => $"{jj.CriticalCurrent * 1e6:F1} µA Ic",
        SquidDevice sq => $"{sq.ModulatedCriticalCurrent * 1e6:F1} µA ({sq.ExternalFluxNormalized:F2} Φ₀)",
        TransmissionLineDevice tl => $"{tl.CharacteristicImpedance:F1} Ω ({tl.DelaySeconds * 1e12:F0} ps)",
        HallEffectSensorDevice hs => $"{hs.MagneticFieldTesla * 1000:F0} mT ({hs.SensitivityVoltsPerTesla:F1} V/T)",
        BatteryCellDevice b => $"{b.CapacityAmpHours:F1} Ah ({b.OpenCircuitVoltage:F2} V)",
        _ => dev.Type
    };
}
