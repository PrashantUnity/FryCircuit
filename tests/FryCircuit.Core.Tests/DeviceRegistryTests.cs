using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.Diode;
using FryCircuit.Core.Devices.Resistor;
using FryCircuit.Core.Devices.Switch;
using Xunit;

namespace FryCircuit.Core.Tests;

public class DeviceRegistryTests
{
    [Theory]
    [InlineData("resistor")]
    [InlineData("capacitor")]
    [InlineData("inductor")]
    [InlineData("ground")]
    [InlineData("vsource")]
    [InlineData("csource")]
    [InlineData("diode")]
    [InlineData("zener")]
    [InlineData("led")]
    [InlineData("npn")]
    [InlineData("nmos")]
    [InlineData("opamp")]
    [InlineData("potentiometer")]
    [InlineData("spst")]
    [InlineData("and")]
    [InlineData("timer555")]
    [InlineData("josephson")]
    [InlineData("squid")]
    [InlineData("tunneldiode")]
    [InlineData("tline")]
    [InlineData("cosmicipulse")]
    [InlineData("hallsensor")]
    [InlineData("battery")]
    [InlineData("netlabel")]
    [InlineData("voltmeter")]
    [InlineData("amperemeter")]
    [InlineData("lamp")]
    public void Registry_CreatesRegisteredDeviceTypes(string typeName)
    {
        var dev = DeviceRegistry.CreateDevice(typeName, 1, 5, 5);

        Assert.NotNull(dev);
        Assert.Equal(1, dev.Id);
        Assert.Equal(5.0, dev.LocationX);
        Assert.Equal(5.0, dev.LocationY);
        Assert.NotEmpty(dev.Pins);
    }

    [Fact]
    public void SwitchDescriptor_HandleClick_TogglesSwitch()
    {
        var sw = new SwitchDevice(1, "spst", 0, 0);
        bool initialState = sw.IsClosed;

        bool handled = DeviceRegistry.HandleClick(sw);

        Assert.True(handled);
        Assert.Equal(!initialState, sw.IsClosed);
    }

    [Fact]
    public void FormatLabel_ReturnsProperEngineeringUnits()
    {
        var res = new ResistorDevice(1, 0, 0, 4700.0);
        string label = DeviceRegistry.FormatLabel(res);

        Assert.Equal("4.7 kΩ", label);
    }

    [Fact]
    public void GetPrimaryParameter_AllowsLiveTuning()
    {
        var res = new ResistorDevice(1, 0, 0, 1000.0);
        var spec = DeviceRegistry.GetPrimaryParameter(res);

        Assert.NotNull(spec);
        Assert.Equal("Resistance", spec.DisplayLabel);
        Assert.Equal("Ω", spec.Unit);
        Assert.Equal(1000.0, spec.Getter(res));

        spec.Setter(res, 2200.0);
        Assert.Equal(2200.0, res.Resistance);
    }

    [Fact]
    public void LedDescriptor_ProvidesWavelengthParameterAndTuning()
    {
        var led = new LedDevice(1, 0, 0, 650.0);
        var spec = DeviceRegistry.GetPrimaryParameter(led);

        Assert.NotNull(spec);
        Assert.Equal("Wavelength", spec.DisplayLabel);
        Assert.Equal("nm", spec.Unit);
        Assert.Equal(650.0, spec.Getter(led));

        spec.Setter(led, 525.0);
        Assert.Equal(525.0, led.Wavelength);
        Assert.Equal("Green", led.ColorName);
        Assert.Equal("#00E060", led.ColorHex);
    }

    [Theory]
    [InlineData(650.0, "Red", "#FF2020", 1.91)]
    [InlineData(610.0, "Orange", "#FF8C00", 2.03)]
    [InlineData(585.0, "Yellow", "#FFD700", 2.12)]
    [InlineData(525.0, "Green", "#00E060", 2.36)]
    [InlineData(500.0, "Cyan", "#00F0FF", 2.48)]
    [InlineData(470.0, "Blue", "#0080FF", 2.64)]
    [InlineData(405.0, "Violet", "#8B00FF", 3.06)]
    [InlineData(0.0, "White", "#FFFFFF", 3.2)]
    public void LedDevice_PhysicsAndColor_MatchesSpecification(double wavelength, string expectedName, string expectedHex, double expectedVf)
    {
        var led = new LedDevice(1, 0, 0, wavelength);
        Assert.Equal(expectedName, led.ColorName);
        Assert.Equal(expectedHex, led.ColorHex);
        Assert.InRange(led.ForwardVoltage, expectedVf - 0.05, expectedVf + 0.05);

        string label = DeviceRegistry.FormatLabel(led);
        Assert.Contains(expectedName, label);
    }
}

