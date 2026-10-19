using System;
using System.Linq;
using Avalonia.Media;
using FryCircuit.Core.Models;
using FryCircuit.UI.Controls;
using FryCircuit.UI.ViewModels;
using Xunit;

namespace FryCircuit.Plugin.Tests;

public sealed class MultiChannelOscilloscopeTests
{
    [Fact]
    public void Oscilloscope_DefaultChannels_HasFourStandardChannelsWithIndustryColors()
    {
        var scope = new OscilloscopeControl();

        Assert.Equal(4, scope.Channels.Count);
        Assert.Equal(4, scope.Traces.Count);

        // Standard Industry Palette: Amber, Cyan, Magenta, Emerald
        Assert.Equal("CH1", scope.Channels[0].Name);
        Assert.Equal(Color.FromRgb(245, 158, 11), scope.Channels[0].Color);

        Assert.Equal("CH2", scope.Channels[1].Name);
        Assert.Equal(Color.FromRgb(6, 182, 212), scope.Channels[1].Color);

        Assert.Equal("CH3", scope.Channels[2].Name);
        Assert.Equal(Color.FromRgb(236, 72, 153), scope.Channels[2].Color);

        Assert.Equal("CH4", scope.Channels[3].Name);
        Assert.Equal(Color.FromRgb(16, 185, 129), scope.Channels[3].Color);
    }

    [Fact]
    public void Oscilloscope_AddChannel_DynamicallyExpandsToChannelFiveAndBeyond()
    {
        var scope = new OscilloscopeControl();
        Assert.Equal(4, scope.Channels.Count);

        // Add 5th channel
        var ch5 = OscilloscopeChannel.CreateDefault(5);
        scope.Channels.Add(ch5);

        Assert.Equal(5, scope.Channels.Count);
        Assert.Equal(5, ch5.ChannelIndex);
        Assert.Equal("CH5", ch5.Name);
        Assert.Equal(Color.FromRgb(139, 92, 246), ch5.Color); // Violet
    }

    [Fact]
    public void Oscilloscope_PushSamples_MultiChannelValuesStreamedCorrectly()
    {
        var scope = new OscilloscopeControl();

        scope.PushSamples(5.0, 3.3, 1.8, 0.9);

        Assert.Equal(5.0, scope.Channels[0].CurrentValue);
        Assert.Equal(3.3, scope.Channels[1].CurrentValue);
        Assert.Equal(1.8, scope.Channels[2].CurrentValue);
        Assert.Equal(0.9, scope.Channels[3].CurrentValue);
    }

    [Fact]
    public void Oscilloscope_DisplayLayout_TogglesBetweenOverlayAndStacked()
    {
        var scope = new OscilloscopeControl();
        Assert.Equal(OscilloscopeDisplayLayout.Overlay, scope.DisplayLayout);
        Assert.Equal(OscilloscopeDisplayMode.TimeDomain, scope.DisplayMode);

        scope.DisplayLayout = OscilloscopeDisplayLayout.Stacked;
        Assert.Equal(OscilloscopeDisplayLayout.Stacked, scope.DisplayLayout);
        Assert.Equal(OscilloscopeDisplayMode.TimeDomain, scope.DisplayMode);

        scope.DisplayLayout = OscilloscopeDisplayLayout.XY;
        Assert.Equal(OscilloscopeDisplayLayout.XY, scope.DisplayLayout);

        scope.DisplayLayout = OscilloscopeDisplayLayout.FftSpectrum;
        Assert.Equal(OscilloscopeDisplayLayout.FftSpectrum, scope.DisplayLayout);
        Assert.Equal(OscilloscopeDisplayMode.FftSpectrum, scope.DisplayMode);
    }

    [Fact]
    public void Oscilloscope_TriggerSettings_CanConfigureModesAndSlopes()
    {
        var scope = new OscilloscopeControl();

        Assert.Equal(TriggerMode.Auto, scope.TriggerSettings.Mode);
        Assert.Equal(TriggerSlope.Rising, scope.TriggerSettings.Slope);
        Assert.Equal(1, scope.TriggerSettings.SourceChannelIndex);

        scope.TriggerSettings.Mode = TriggerMode.Normal;
        scope.TriggerSettings.Slope = TriggerSlope.Falling;
        scope.TriggerSettings.Level = 2.5;
        scope.TriggerSettings.SourceChannelIndex = 2;

        Assert.Equal(TriggerMode.Normal, scope.TriggerSettings.Mode);
        Assert.Equal(TriggerSlope.Falling, scope.TriggerSettings.Slope);
        Assert.Equal(2.5, scope.TriggerSettings.Level);
        Assert.Equal(2, scope.TriggerSettings.SourceChannelIndex);
    }

    [Fact]
    public void Oscilloscope_AutoSet_CalculatesStandardVoltsPerDiv()
    {
        var scope = new OscilloscopeControl();

        // Feed AC sine wave with 10V amplitude into CH1
        for (int i = 0; i < 100; i++)
        {
            double v = 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            scope.Channels[0].AddSample(v);
        }

        scope.AutoSet();

        // Nice scale for 10V peak should be standard engineering division (e.g. 2.5V/div)
        Assert.True(scope.Channels[0].VoltsPerDiv > 0.0);
        Assert.Equal(0.0, scope.Channels[0].VerticalOffsetDivs);
    }

    [Fact]
    public void ViewModel_MultiChannelProbing_SetsChannelTargetsAndLabels()
    {
        using var vm = new MainWindowViewModel();

        Assert.Equal(4, vm.ScopeChannels.Count);

        // Attach probe to Channel 3 on Net #8
        vm.SetProbedNode(3, 8);

        Assert.Equal(8, vm.ProbedNodeCh3);
        Assert.Contains("Net #8", vm.ProbedCh3Label);

        // Attach probe to Channel 4 on Net #12
        vm.SetProbedNode(4, 12);

        Assert.Equal(12, vm.ProbedNodeCh4);
        Assert.Contains("Net #12", vm.ProbedCh4Label);
    }

    [Fact]
    public void ViewModel_AddScopeChannel_ExpandsViewModelChannels()
    {
        using var vm = new MainWindowViewModel();
        var scope = new OscilloscopeControl();
        var canvas = new CircuitCanvas();

        vm.AttachControls(canvas, scope);

        int initialCount = scope.Channels.Count;
        vm.AddScopeChannelCommand.Execute(null);

        Assert.Equal(initialCount + 1, scope.Channels.Count);
    }

    [Fact]
    public void Oscilloscope_Panning_HorizontalTimeOffsetAndZoom_CanPanAndZoom()
    {
        var scope = new OscilloscopeControl();

        Assert.Equal(0.0, scope.TimeOffsetDivs);
        Assert.Equal(1.0, scope.TimebaseZoom);

        // Pan horizontally by 3.5 divisions
        scope.TimeOffsetDivs = 3.5;
        // Zoom timebase by 2.5x
        scope.TimebaseZoom = 2.5;

        Assert.Equal(3.5, scope.TimeOffsetDivs);
        Assert.Equal(2.5, scope.TimebaseZoom);
    }

    [Fact]
    public void Oscilloscope_Panning_VerticalChannelOffsets_CanPanPerChannel()
    {
        var scope = new OscilloscopeControl();

        Assert.Equal(0.0, scope.Channels[0].VerticalOffsetDivs);
        Assert.Equal(0.0, scope.Channels[1].VerticalOffsetDivs);

        // Shift CH1 up by 1.5 divisions and CH2 down by 2.0 divisions
        scope.Channels[0].VerticalOffsetDivs = 1.5;
        scope.Channels[1].VerticalOffsetDivs = -2.0;

        Assert.Equal(1.5, scope.Channels[0].VerticalOffsetDivs);
        Assert.Equal(-2.0, scope.Channels[1].VerticalOffsetDivs);
    }

    [Fact]
    public void Oscilloscope_ResetPanAndZoom_RestoresZeroOffsetsAndUnitZoom()
    {
        var scope = new OscilloscopeControl();

        scope.TimeOffsetDivs = -4.2;
        scope.TimebaseZoom = 3.0;
        scope.Channels[0].VerticalOffsetDivs = 2.5;
        scope.Channels[1].VerticalOffsetDivs = -1.8;

        scope.ResetPanAndZoom();

        Assert.Equal(0.0, scope.TimeOffsetDivs);
        Assert.Equal(1.0, scope.TimebaseZoom);
        Assert.Equal(0.0, scope.Channels[0].VerticalOffsetDivs);
        Assert.Equal(0.0, scope.Channels[1].VerticalOffsetDivs);
    }

    [Fact]
    public void ViewModel_ResetScopePanCommand_InvokesResetPan()
    {
        using var vm = new MainWindowViewModel();
        var scope = new OscilloscopeControl();
        var canvas = new CircuitCanvas();
        vm.AttachControls(canvas, scope);

        scope.TimeOffsetDivs = 5.0;
        scope.TimebaseZoom = 4.0;
        scope.Channels[0].VerticalOffsetDivs = -3.0;

        vm.ResetScopePanCommand.Execute(null);

        Assert.Equal(0.0, scope.TimeOffsetDivs);
        Assert.Equal(1.0, scope.TimebaseZoom);
        Assert.Equal(0.0, scope.Channels[0].VerticalOffsetDivs);
        Assert.Contains("reset to center", vm.StatusText);
    }

    [Fact]
    public void Oscilloscope_ZoomInTime_ZoomOutTime_ScalesTimebaseZoomCorrectly()
    {
        var scope = new OscilloscopeControl();
        Assert.Equal(1.0, scope.TimebaseZoom);

        scope.ZoomInTime();
        Assert.True(scope.TimebaseZoom > 1.0);

        double zoomed = scope.TimebaseZoom;
        scope.ZoomOutTime();
        Assert.True(scope.TimebaseZoom < zoomed);
    }

    [Fact]
    public void Oscilloscope_PanTime_AdjustsTimeOffsetDivs()
    {
        var scope = new OscilloscopeControl();
        Assert.Equal(0.0, scope.TimeOffsetDivs);

        scope.PanTime(-1.0);
        Assert.Equal(-1.0, scope.TimeOffsetDivs);

        scope.PanTime(2.5);
        Assert.Equal(1.5, scope.TimeOffsetDivs);
    }

    [Fact]
    public void Oscilloscope_ZoomInVoltage_ZoomOutVoltage_StepsVoltsPerDivAccurately()
    {
        var scope = new OscilloscopeControl();
        scope.Channels[0].VoltsPerDiv = 1.0;

        // Zoom In = taller waveform, smaller V/div (1.0 -> 0.5)
        scope.ZoomInVoltage(1);
        Assert.Equal(0.5, scope.Channels[0].VoltsPerDiv);

        scope.ZoomInVoltage(1);
        Assert.Equal(0.2, scope.Channels[0].VoltsPerDiv);

        // Zoom Out = smaller waveform, larger V/div (0.2 -> 0.5 -> 1.0)
        scope.ZoomOutVoltage(1);
        Assert.Equal(0.5, scope.Channels[0].VoltsPerDiv);

        scope.ZoomOutVoltage(1);
        Assert.Equal(1.0, scope.Channels[0].VoltsPerDiv);
    }

    [Fact]
    public void Oscilloscope_PanVoltage_AdjustsChannelVerticalOffset()
    {
        var scope = new OscilloscopeControl();
        scope.ActiveChannelIndex = 2;
        Assert.Equal(0.0, scope.Channels[1].VerticalOffsetDivs);

        scope.PanVoltage(0.5);
        Assert.Equal(0.5, scope.Channels[1].VerticalOffsetDivs);

        scope.PanVoltage(-1.5);
        Assert.Equal(-1.0, scope.Channels[1].VerticalOffsetDivs);
    }

    [Fact]
    public void Oscilloscope_ToggleBoxZoom_TogglesBoxZoomMode()
    {
        var scope = new OscilloscopeControl();
        Assert.False(scope.IsBoxZoomMode);

        scope.ToggleBoxZoom();
        Assert.True(scope.IsBoxZoomMode);

        scope.ToggleBoxZoom();
        Assert.False(scope.IsBoxZoomMode);
    }

    [Fact]
    public void ViewModel_ScopeZoomAndPanCommands_ExecuteExpectedScopeActions()
    {
        using var vm = new MainWindowViewModel();
        var scope = new OscilloscopeControl();
        var canvas = new CircuitCanvas();
        vm.AttachControls(canvas, scope);

        vm.ScopeZoomInTimeCommand.Execute(null);
        Assert.True(scope.TimebaseZoom > 1.0);

        vm.ScopePanRightTimeCommand.Execute(null);
        Assert.Equal(1.0, scope.TimeOffsetDivs);

        vm.ScopeZoomInVoltageCommand.Execute(null);
        Assert.Equal(0.5, scope.Channels[0].VoltsPerDiv);

        vm.ScopePanUpVoltageCommand.Execute(null);
        Assert.Equal(0.5, scope.Channels[0].VerticalOffsetDivs);

        vm.ScopeToggleBoxZoomCommand.Execute(null);
        Assert.True(scope.IsBoxZoomMode);
        Assert.True(vm.IsScopeBoxZoomActive);
    }
}
