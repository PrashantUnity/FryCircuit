using FryCircuit.Core.Models;
using FryCircuit.UI.ViewModels;
using Material.Icons;
using Xunit;

namespace FryCircuit.Plugin.Tests;

public sealed class WireProbingTests
{
    [Fact]
    public void WireProbing_InitialState_DefaultsAreClean()
    {
        using var vm = new MainWindowViewModel();
        vm.SelectedDevice = null;
        vm.SelectedWire = null;

        Assert.False(vm.HasSelectedWire);
        Assert.False(vm.HasSelectedDevice);
        Assert.True(vm.HasNoSelection);
        Assert.False(vm.IsSelectedWireProbed);
        Assert.Equal(MaterialIconKind.EyeOutline, vm.SelectedWireProbeEyeIconKind);
        Assert.Equal("View on Oscilloscope", vm.SelectedWireProbeButtonText);
    }

    [Fact]
    public void WireProbing_WireSelected_UpdatesInspectorStateAndExcludesComponent()
    {
        using var vm = new MainWindowViewModel();

        var wire = new CircuitWire(1, 4, 1, 0, 2, 0);
        vm.SelectedWire = wire;

        Assert.True(vm.HasSelectedWire);
        Assert.False(vm.HasSelectedDevice);
        Assert.False(vm.HasNoSelection);
        Assert.Equal(4, vm.SelectedWireNodeId);
        Assert.False(string.IsNullOrWhiteSpace(vm.SelectedWireConnectionDescription));
    }

    [Fact]
    public void WireProbing_ToggleEyeButton_AutoAssignsAndRevealsOscilloscope()
    {
        using var vm = new MainWindowViewModel();
        vm.IsOscilloscopeOpen = false;

        var wire = new CircuitWire(1, 3, 1, 0, 2, 0);
        vm.SelectedWire = wire;

        // Click the Eye button to view on oscilloscope
        vm.ToggleSelectedWireProbeCommand.Execute(null);

        // Oscilloscope must be auto-opened
        Assert.True(vm.IsOscilloscopeOpen);
        Assert.True(vm.IsSelectedWireProbed);
        Assert.True(vm.IsSelectedWireProbedCh1);
        Assert.Equal(3, vm.ProbedNodeCh1);
        Assert.Contains("Net #3", vm.ProbedCh1Label);
        Assert.Equal(MaterialIconKind.Eye, vm.SelectedWireProbeEyeIconKind);
        Assert.Contains("Probed on CH1", vm.SelectedWireProbeButtonText);
    }

    [Fact]
    public void WireProbing_ToggleEyeButton_DetachesWhenAlreadyProbed()
    {
        using var vm = new MainWindowViewModel();

        var wire = new CircuitWire(1, 5, 1, 0, 2, 0);
        vm.SelectedWire = wire;

        // 1st click: Probes the signal
        vm.ToggleSelectedWireProbeCommand.Execute(null);
        Assert.True(vm.IsSelectedWireProbed);

        // 2nd click: Detaches the signal from the oscilloscope
        vm.ToggleSelectedWireProbeCommand.Execute(null);
        Assert.False(vm.IsSelectedWireProbed);
        Assert.False(vm.IsSelectedWireProbedCh1);
        Assert.Null(vm.ProbedNodeCh1);
        Assert.Equal(MaterialIconKind.EyeOutline, vm.SelectedWireProbeEyeIconKind);
        Assert.Equal("View on Oscilloscope", vm.SelectedWireProbeButtonText);
    }

    [Fact]
    public void WireProbing_ChannelPillSwitching_SwitchesChannelCleanly()
    {
        using var vm = new MainWindowViewModel();

        var wire = new CircuitWire(1, 7, 1, 0, 2, 0);
        vm.SelectedWire = wire;

        // Probe directly to CH2
        vm.ProbeSelectedWireCh2Command.Execute(null);

        Assert.True(vm.IsSelectedWireProbed);
        Assert.True(vm.IsSelectedWireProbedCh2);
        Assert.False(vm.IsSelectedWireProbedCh1);
        Assert.Equal(7, vm.ProbedNodeCh2);
        Assert.Contains("Net #7", vm.ProbedCh2Label);
        Assert.Contains("Probed on CH2", vm.SelectedWireProbeButtonText);
        Assert.Equal(MaterialIconKind.Eye, vm.SelectedWireProbeEyeIconKind);

        // Detach
        vm.DetachSelectedWireProbeCommand.Execute(null);
        Assert.False(vm.IsSelectedWireProbed);
        Assert.Null(vm.ProbedNodeCh2);
    }
}
