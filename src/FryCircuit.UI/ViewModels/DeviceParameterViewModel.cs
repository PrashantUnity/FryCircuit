using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _formattedValue = string.Empty;

    [ObservableProperty]
    private string _editText = string.Empty;

    private bool _isUpdatingEdit;

    public DeviceParameterViewModel(Device device, DeviceParameterSpec spec, Action onChanged)
    {
        _device = device;
        _spec = spec;
        _onChanged = onChanged;
        _value = spec.Getter(device);
        UpdateFormatted();
    }

    partial void OnValueChanged(double value)
    {
        _spec.Setter(_device, value);
        UpdateFormatted();
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
        Value = _spec.Getter(_device);
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
