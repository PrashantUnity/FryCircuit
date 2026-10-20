using FryCircuit.Core.Models;

namespace FryCircuit.Core.History;

public class AddDeviceAction : IUndoableAction
{
    private readonly Circuit _circuit;
    private readonly Device _device;

    public string Description => $"Add {_device.Type.ToUpperInvariant()} #{_device.Id}";

    public AddDeviceAction(Circuit circuit, Device device)
    {
        _circuit = circuit;
        _device = device;
    }

    public void Undo()
    {
        _circuit.RemoveDevice(_device);
    }

    public void Redo()
    {
        _circuit.AddDevice(_device);
    }
}

public class RemoveDeviceAction : IUndoableAction
{
    private readonly Circuit _circuit;
    private readonly Device _device;
    private readonly List<CircuitWire> _removedWires;

    public string Description => $"Delete {_device.Type.ToUpperInvariant()} #{_device.Id}";

    public RemoveDeviceAction(Circuit circuit, Device device, IEnumerable<CircuitWire> attachedWires)
    {
        _circuit = circuit;
        _device = device;
        _removedWires = attachedWires.ToList();
    }

    public void Undo()
    {
        _circuit.AddDevice(_device);
        foreach (var wire in _removedWires)
        {
            if (!_circuit.Wires.Contains(wire))
            {
                _circuit.Wires.Add(wire);
            }
        }
    }

    public void Redo()
    {
        foreach (var wire in _removedWires)
        {
            _circuit.Wires.Remove(wire);
        }
        _circuit.RemoveDevice(_device);
    }
}

public class AddWireAction : IUndoableAction
{
    private readonly Circuit _circuit;
    private readonly CircuitWire _wire;

    public string Description => $"Connect Wire (Node {_wire.NodeId})";

    public AddWireAction(Circuit circuit, CircuitWire wire)
    {
        _circuit = circuit;
        _wire = wire;
    }

    public void Undo()
    {
        _circuit.RemoveWire(_wire);
    }

    public void Redo()
    {
        if (!_circuit.Wires.Contains(_wire))
        {
            _circuit.Wires.Add(_wire);
        }
    }
}

public class RemoveWireAction : IUndoableAction
{
    private readonly Circuit _circuit;
    private readonly CircuitWire _wire;

    public string Description => $"Cut Wire (Node {_wire.NodeId})";

    public RemoveWireAction(Circuit circuit, CircuitWire wire)
    {
        _circuit = circuit;
        _wire = wire;
    }

    public void Undo()
    {
        var dev1 = _circuit.Devices.FirstOrDefault(d => d.Id == _wire.FromDeviceId);
        var dev2 = _circuit.Devices.FirstOrDefault(d => d.Id == _wire.ToDeviceId);
        if (dev1 != null && dev2 != null)
        {
            _circuit.ConnectPins(dev1, _wire.FromPinIndex, dev2, _wire.ToPinIndex);
            _circuit.RouteAllWires();
        }
        else if (!_circuit.Wires.Contains(_wire))
        {
            _circuit.Wires.Add(_wire);
        }
    }

    public void Redo()
    {
        _circuit.RemoveWire(_wire);
    }
}

public class MoveDeviceAction : IUndoableAction
{
    private readonly Circuit? _circuit;
    private readonly Device _device;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly double _newX;
    private readonly double _newY;

    public string Description => $"Move {_device.Type.ToUpperInvariant()} #{_device.Id}";

    public MoveDeviceAction(Device device, double oldX, double oldY, double newX, double newY, Circuit? circuit = null)
    {
        _device = device;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
        _circuit = circuit;
    }

    public void Undo()
    {
        _device.LocationX = _oldX;
        _device.LocationY = _oldY;
        _circuit?.RouteAllWires();
    }

    public void Redo()
    {
        _device.LocationX = _newX;
        _device.LocationY = _newY;
        _circuit?.RouteAllWires();
    }
}

public class RotateDeviceAction : IUndoableAction
{
    private readonly Device _device;
    private readonly DeviceRotation _oldRotation;
    private readonly DeviceRotation _newRotation;

    public string Description => $"Rotate {_device.Type.ToUpperInvariant()} #{_device.Id}";

    public RotateDeviceAction(Device device, DeviceRotation oldRotation, DeviceRotation newRotation)
    {
        _device = device;
        _oldRotation = oldRotation;
        _newRotation = newRotation;
    }

    public void Undo()
    {
        _device.Transform = new DeviceTransform(_oldRotation);
    }

    public void Redo()
    {
        _device.Transform = new DeviceTransform(_newRotation);
    }
}

public class BatchCircuitAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions;
    public string Description { get; }

    public BatchCircuitAction(string description, IEnumerable<IUndoableAction> actions)
    {
        Description = description;
        _actions = actions.ToList();
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }

    public void Redo()
    {
        foreach (var action in _actions)
        {
            action.Redo();
        }
    }
}

