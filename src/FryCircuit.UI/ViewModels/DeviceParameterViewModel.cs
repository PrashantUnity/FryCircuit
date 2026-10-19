using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.ViewModels;

public partial class DeviceParameterViewModel : ObservableObject
{
    private readonly Device _device;
    private readonly DeviceParameterSpec _spec;
    private readonly Action _onChanged;

    public string ParameterName => _spec.ParameterName;
    public string DisplayLabel => _spec.DisplayLabel;
    public string Unit => _spec.Unit;
    public double Minimum => _spec.Minimum;
    public double Maximum => _spec.Maximum;
    public double Step => _spec.Step;

    public ObservableCollection<ParameterScaleOption> ScaleOptions { get; } = new();
    public bool HasScaleOptions => ScaleOptions.Count > 1;

    [ObservableProperty]
    private ParameterScaleOption? _activeScale;

    [ObservableProperty]
    private double _scaledSliderValue;

    [ObservableProperty]
    private double _scaledMinimum;

    [ObservableProperty]
    private double _scaledMaximum;

    [ObservableProperty]
    private double _scaledStep;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _formattedValue = string.Empty;

    [ObservableProperty]
    private string _editText = string.Empty;

    private bool _isUpdatingEdit;
    private bool _isUpdatingSlider;
    private bool _isUpdatingValue;

    public DeviceParameterViewModel(Device device, DeviceParameterSpec spec, Action onChanged)
    {
        _device = device;
        _spec = spec;
        _onChanged = onChanged;
        _value = spec.Getter(device);

        InitializeScales();
        UpdateFormatted();
    }

    private void InitializeScales()
    {
        ScaleOptions.Clear();
        var scales = ParameterScaleHelper.GetScales(_spec);
        foreach (var s in scales)
        {
            ScaleOptions.Add(s);
        }

        OnPropertyChanged(nameof(HasScaleOptions));

        if (HasScaleOptions)
        {
            var best = ParameterScaleHelper.FindMatchingScale(ScaleOptions, Value) ?? ScaleOptions[0];
            SetActiveScaleInternal(best);
            ScaledSliderValue = Math.Clamp(Value / best.Multiplier, ScaledMinimum, ScaledMaximum);
        }
        else
        {
            ScaledMinimum = Minimum;
            ScaledMaximum = Maximum;
            ScaledStep = Step;
            ScaledSliderValue = Math.Clamp(Value, ScaledMinimum, ScaledMaximum);
        }
    }

    private void SetActiveScaleInternal(ParameterScaleOption scale)
    {
        foreach (var opt in ScaleOptions)
        {
            opt.IsActive = (opt == scale);
        }

        ActiveScale = scale;

        if (Minimum < 0)
        {
            ScaledMinimum = Math.Max(-1000.0, Minimum / scale.Multiplier);
            ScaledMaximum = Math.Min(1000.0, Maximum / scale.Multiplier);
        }
        else
        {
            ScaledMinimum = Math.Max(scale.DefaultMin, Minimum / scale.Multiplier);
            ScaledMaximum = Math.Min(scale.DefaultMax, Maximum / scale.Multiplier);
        }

        ScaledStep = scale.Step;
    }

    [RelayCommand]
    public void SelectScale(ParameterScaleOption scale)
    {
        if (scale == null || scale == ActiveScale) return;

        // When switching scale, preserve the slider's numeric position in the new scale
        // so the user can immediately tune values in that scale
        double currentNum = ScaledSliderValue;
        SetActiveScaleInternal(scale);

        double targetNum = Math.Clamp(currentNum, ScaledMinimum, ScaledMaximum);
        double actualVal = targetNum * scale.Multiplier;
        actualVal = Math.Clamp(actualVal, Minimum, Maximum);

        _isUpdatingSlider = true;
        ScaledSliderValue = actualVal / scale.Multiplier;
        _isUpdatingSlider = false;

        Value = actualVal;
    }

    [RelayCommand]
    public void StepDecadeDown()
    {
        double target = Value / 10.0;
        if (target < Minimum) target = Minimum;
        Value = target;
    }

    [RelayCommand]
    public void StepDecadeUp()
    {
        double target = Value * 10.0;
        if (target > Maximum) target = Maximum;
        Value = target;
    }

    partial void OnScaledSliderValueChanged(double value)
    {
        if (_isUpdatingSlider) return;

        double actualVal = (HasScaleOptions && ActiveScale != null)
            ? value * ActiveScale.Multiplier
            : value;

        actualVal = Math.Clamp(actualVal, Minimum, Maximum);

        if (Math.Abs(actualVal - Value) > 1e-14)
        {
            _isUpdatingValue = true;
            Value = actualVal;
            _isUpdatingValue = false;
        }
    }

    partial void OnValueChanged(double value)
    {
        _spec.Setter(_device, value);
        UpdateFormatted();

        if (!_isUpdatingValue)
        {
            if (HasScaleOptions)
            {
                var bestScale = ParameterScaleHelper.FindMatchingScale(ScaleOptions, value);
                if (bestScale != null && bestScale != ActiveScale)
                {
                    SetActiveScaleInternal(bestScale);
                }

                _isUpdatingSlider = true;
                double multiplier = ActiveScale?.Multiplier ?? 1.0;
                ScaledSliderValue = Math.Clamp(value / multiplier, ScaledMinimum, ScaledMaximum);
                _isUpdatingSlider = false;
            }
            else
            {
                _isUpdatingSlider = true;
                ScaledSliderValue = Math.Clamp(value, ScaledMinimum, ScaledMaximum);
                _isUpdatingSlider = false;
            }
        }

        _onChanged?.Invoke();
    }

    partial void OnEditTextChanged(string value)
    {
        if (_isUpdatingEdit) return;
        if (DeviceDescriptorBase.TryParseEngineering(value, out double parsed))
        {
            double clamped = Math.Clamp(parsed, Minimum, Maximum);
            if (Math.Abs(clamped - Value) > 1e-12)
            {
                _isUpdatingEdit = true;
                Value = clamped;
                _isUpdatingEdit = false;
            }
        }
    }

    public void CommitEditText()
    {
        if (DeviceDescriptorBase.TryParseEngineering(EditText, out double parsed))
        {
            Value = Math.Clamp(parsed, Minimum, Maximum);
        }
        else
        {
            UpdateFormatted();
        }
    }

    public void RefreshFromDevice()
    {
        double val = _spec.Getter(_device);
        if (Math.Abs(val - Value) > 1e-14)
        {
            Value = val;
        }
    }

    private void UpdateFormatted()
    {
        FormattedValue = DeviceDescriptorBase.FormatEngineering(Value, _spec.Unit);
        if (!_isUpdatingEdit)
        {
            _isUpdatingEdit = true;
            EditText = FormattedValue;
            _isUpdatingEdit = false;
        }
    }
}
