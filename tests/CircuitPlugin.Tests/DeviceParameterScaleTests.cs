using FryCircuit.Core.Devices;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Devices.Capacitor;
using FryCircuit.Core.Devices.Resistor;
using FryCircuit.Core.Devices.VoltageSource;
using FryCircuit.UI.ViewModels;
using Xunit;

namespace CircuitPlugin.Tests;

public class DeviceParameterScaleTests
{
    [Fact]
    public void FrequencyParameter_GeneratesExpectedScales()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        var scales = ParameterScaleHelper.GetScales(freqSpec);

        Assert.NotEmpty(scales);
        var labels = scales.Select(s => s.Label).ToList();
        Assert.Contains("Hz", labels);
        Assert.Contains("kHz", labels);
        Assert.Contains("MHz", labels);
        Assert.Contains("GHz", labels);
    }

    [Fact]
    public void ResistorParameter_GeneratesExpectedScales()
    {
        var resDesc = new ResistorDescriptor();
        var resSpec = resDesc.PrimaryParameter!;
        var scales = ParameterScaleHelper.GetScales(resSpec);

        Assert.NotEmpty(scales);
        var labels = scales.Select(s => s.Label).ToList();
        Assert.Contains("Ω", labels);
        Assert.Contains("kΩ", labels);
        Assert.Contains("MΩ", labels);
    }

    [Fact]
    public void CapacitorParameter_GeneratesExpectedScales()
    {
        var capDesc = new CapacitorDescriptor();
        var capSpec = capDesc.PrimaryParameter!;
        var scales = ParameterScaleHelper.GetScales(capSpec);

        Assert.NotEmpty(scales);
        var labels = scales.Select(s => s.Label).ToList();
        Assert.Contains("pF", labels);
        Assert.Contains("nF", labels);
        Assert.Contains("µF", labels);
        Assert.Contains("mF", labels);
    }

    [Fact]
    public void NonScaledParameter_ProducesNoScales()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var phaseSpec = vsDesc.Parameters.First(p => p.ParameterName == "Phase");
        var scales = ParameterScaleHelper.GetScales(phaseSpec);

        Assert.Empty(scales);
    }

    [Fact]
    public void ParameterScaleHelper_MatchesCorrectScaleForValue()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        var scales = ParameterScaleHelper.GetScales(freqSpec);

        var scale2k = ParameterScaleHelper.FindMatchingScale(scales, 2000.0);
        Assert.NotNull(scale2k);
        Assert.Equal("kHz", scale2k.Label);

        var scale10M = ParameterScaleHelper.FindMatchingScale(scales, 10e6);
        Assert.NotNull(scale10M);
        Assert.Equal("MHz", scale10M.Label);

        var scale50 = ParameterScaleHelper.FindMatchingScale(scales, 50.0);
        Assert.NotNull(scale50);
        Assert.Equal("Hz", scale50.Label);
    }

    [Fact]
    public void DeviceParameterViewModel_InitializesMatchingScaleAndScaledSlider()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var vsDevice = (VoltageSourceDevice)vsDesc.CreateInstance(1, 0, 0);
        vsDevice.Frequency = 2000.0;

        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        var vm = new DeviceParameterViewModel(vsDevice, freqSpec, () => { });

        Assert.True(vm.HasScaleOptions);
        Assert.NotNull(vm.ActiveScale);
        Assert.Equal("kHz", vm.ActiveScale.Label);
        Assert.Equal(2.0, vm.ScaledSliderValue, 2);
        Assert.Contains("2.0 kHz", vm.EditText);
    }

    [Fact]
    public void DeviceParameterViewModel_SliderAdjustment_UpdatesDeviceValue()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var vsDevice = (VoltageSourceDevice)vsDesc.CreateInstance(1, 0, 0);
        vsDevice.Frequency = 1000.0;

        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        int changeCount = 0;
        var vm = new DeviceParameterViewModel(vsDevice, freqSpec, () => changeCount++);

        Assert.Equal("kHz", vm.ActiveScale?.Label);

        // Adjust slider in kHz to 5.5
        vm.ScaledSliderValue = 5.5;

        Assert.Equal(5500.0, vm.Value, 1);
        Assert.Equal(5500.0, vsDevice.Frequency, 1);
        Assert.Contains("5.5 kHz", vm.EditText);
        Assert.True(changeCount > 0);
    }

    [Fact]
    public void DeviceParameterViewModel_SelectScale_ScalesValueCorrectly()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var vsDevice = (VoltageSourceDevice)vsDesc.CreateInstance(1, 0, 0);
        vsDevice.Frequency = 2000.0;

        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        var vm = new DeviceParameterViewModel(vsDevice, freqSpec, () => { });

        Assert.Equal("kHz", vm.ActiveScale?.Label);
        Assert.Equal(2.0, vm.ScaledSliderValue, 2);

        // Switch scale to MHz
        var mhzScale = vm.ScaleOptions.First(s => s.Label == "MHz");
        vm.SelectScale(mhzScale);

        Assert.Equal("MHz", vm.ActiveScale?.Label);
        Assert.Equal(2.0, vm.ScaledSliderValue, 2);
        Assert.Equal(2.0e6, vm.Value, 1);
        Assert.Equal(2.0e6, vsDevice.Frequency, 1);
        Assert.Contains("2.0 MHz", vm.EditText);
    }

    [Fact]
    public void DeviceParameterViewModel_DecadeSteppers_ShiftValueAndScale()
    {
        var vsDesc = new VoltageSourceDescriptor();
        var vsDevice = (VoltageSourceDevice)vsDesc.CreateInstance(1, 0, 0);
        vsDevice.Frequency = 500e3; // 500 kHz

        var freqSpec = vsDesc.Parameters.First(p => p.ParameterName == "Frequency");
        var vm = new DeviceParameterViewModel(vsDevice, freqSpec, () => { });

        Assert.Equal("kHz", vm.ActiveScale?.Label);

        // Step up x10 -> 5 MHz
        vm.StepDecadeUp();
        Assert.Equal(5e6, vm.Value, 1);
        Assert.Equal("MHz", vm.ActiveScale?.Label);
        Assert.Equal(5.0, vm.ScaledSliderValue, 2);

        // Step down /10 -> 500 kHz
        vm.StepDecadeDown();
        Assert.Equal(500e3, vm.Value, 1);
        Assert.Equal("kHz", vm.ActiveScale?.Label);
        Assert.Equal(500.0, vm.ScaledSliderValue, 2);
    }

    [Fact]
    public void DeviceParameterViewModel_EditTextSync_AutoSwitchesScale()
    {
        var resDesc = new ResistorDescriptor();
        var resDevice = (ResistorDevice)resDesc.CreateInstance(1, 0, 0);
        resDevice.Resistance = 1000.0; // 1 kΩ

        var resSpec = resDesc.PrimaryParameter!;
        var vm = new DeviceParameterViewModel(resDevice, resSpec, () => { });

        Assert.Equal("kΩ", vm.ActiveScale?.Label);
        Assert.Equal(1.0, vm.ScaledSliderValue, 2);

        // User types 10M
        vm.EditText = "10 MΩ";

        Assert.Equal("MΩ", vm.ActiveScale?.Label);
        Assert.Equal(10.0, vm.ScaledSliderValue, 2);
        Assert.Equal(10e6, vm.Value, 1);
        Assert.Equal(10e6, resDevice.Resistance, 1);
    }
}
