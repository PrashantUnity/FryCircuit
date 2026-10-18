namespace FryCircuit.Core.History;

public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}

public class UndoRedoManager
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    private readonly int _maxHistory;

    public event EventHandler? HistoryChanged;

    public UndoRedoManager(int maxHistory = 100)
    {
        _maxHistory = maxHistory;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? NextUndoDescription => CanUndo ? _undoStack.Peek().Description : null;
    public string? NextRedoDescription => CanRedo ? _redoStack.Peek().Description : null;

    public void Execute(IUndoableAction action)
    {
        action.Redo();
        _undoStack.Push(action);
        _redoStack.Clear();

        if (_undoStack.Count > _maxHistory)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Record(IUndoableAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();

        if (_undoStack.Count > _maxHistory)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
