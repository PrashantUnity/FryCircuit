using FryCircuit.Core.IO;
using FryCircuit.Core.Presets;
using Xunit;

namespace FryCircuit.Core.Tests;

public class CircuitSerializerTests
{
    [Fact]
    public void AllPresets_InstantiateCorrectlyInPureCSharp()
    {
        Assert.NotEmpty(CircuitPresets.PresetNames);

        foreach (var name in CircuitPresets.PresetNames)
        {
            var circuit = CircuitPresets.Create(name);
            Assert.NotNull(circuit);
            Assert.Equal(name, circuit.Name);
            Assert.True(circuit.Devices.Count > 0, $"{name} should have devices");
            Assert.True(circuit.Nodes.Count > 0, $"{name} should have nodes");
            Assert.True(circuit.Nodes.Values.Any(n => n.IsGround), $"{name} must have a ground reference");
        }
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripPreservesCircuit()
    {
        var original = CircuitPresets.CreateHalfWaveRectifier();
        string xml = CircuitSerializer.Serialize(original);

        Assert.NotEmpty(xml);
        Assert.Contains("Half-Wave Rectifier", xml);
        Assert.Contains("vsource", xml);
        Assert.Contains("diode", xml);
        Assert.Contains("resistor", xml);

        var restored = CircuitSerializer.Deserialize(xml);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Devices.Count, restored.Devices.Count);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);

        var origDiode = original.Devices.First(d => d.Type == "diode");
        var restDiode = restored.Devices.First(d => d.Type == "diode");
        Assert.Equal(origDiode.LocationX, restDiode.LocationX);
        Assert.Equal(origDiode.LocationY, restDiode.LocationY);
    }
}
