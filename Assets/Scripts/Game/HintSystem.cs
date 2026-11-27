using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class HintSystem
{
    private readonly Dictionary<WireCell, Action> _rotationHandlers = new Dictionary<WireCell, Action>();
    private Dictionary<WireCell, int[]> _targetAngles = new Dictionary<WireCell, int[]>();
    private bool _isWaitingForCell;

    public event Action SelectionRequested;
    public event Action SelectionDismissed;
    public event Action<WireCell, bool> HintCellStateChanged;
    public event Action VisualsReset;
    public event Action<bool> SolutionChanged;

    public bool IsWaitingForCell => _isWaitingForCell;
    public bool HasSolution => _targetAngles.Count > 0;

    public void BeginLevel()
    {
        EndLevel();
        var cells = Gameplay.WireSystem.Cells.OfType<WireCell>().ToList();
        if (!HintSolutionSolver.TrySolve(cells, out var targetAngles))
        {
            _targetAngles = new Dictionary<WireCell, int[]>();
            Debug.LogError($"Could not calculate a winning wire arrangement for level {LevelManager.LastId}.");
        }
        else
            _targetAngles = targetAngles;

        SolutionChanged?.Invoke(HasSolution);
        MouseManager.PointerClicked += OnPointerClicked;
    }

    public void EndLevel()
    {
        ClearSolution();
        MouseManager.PointerClicked -= OnPointerClicked;
    }

    public void ClearSolution()
    {
        CancelSelection();

        foreach (var handler in _rotationHandlers)
            handler.Key.Rotated -= handler.Value;

        _rotationHandlers.Clear();
        _targetAngles.Clear();
        VisualsReset?.Invoke();
        SolutionChanged?.Invoke(false);
    }

    public bool ActivateHint()
    {
        if (_isWaitingForCell || LevelManager.RemainingHints <= 0 || !HasSolution)
            return false;

        _isWaitingForCell = true;
        SelectionRequested?.Invoke();
        return true;
    }

    public bool TrySelectCell(WireCell cell)
    {
        if (!_isWaitingForCell)
            return false;

        if (cell == null || !cell.IsClickable || !_targetAngles.ContainsKey(cell) ||
            !LevelManager.TryConsumeHint())
        {
            CancelSelection();
            return true;
        }

        _isWaitingForCell = false;
        SelectionDismissed?.Invoke();

        if (!_rotationHandlers.ContainsKey(cell))
        {
            Action onRotated = () => UpdateCellState(cell);
            _rotationHandlers.Add(cell, onRotated);
            cell.Rotated += onRotated;
        }

        UpdateCellState(cell);
        return true;
    }

    private void OnPointerClicked(Collider2D collider)
    {
        if (!_isWaitingForCell)
            return;

        var cell = collider != null ? collider.GetComponentInParent<WireCell>() : null;
        if (cell == null || !cell.IsClickable)
            CancelSelection();
    }

    private void UpdateCellState(WireCell cell)
    {
        HintCellStateChanged?.Invoke(cell, IsInTargetPosition(cell));
    }

    private bool IsInTargetPosition(WireCell cell)
    {
        if (!_targetAngles.TryGetValue(cell, out var targetAngles))
            return false;

        var currentAngles = cell.OutputAngles
            .Select(angle => (angle % 360 + 360) % 360)
            .OrderBy(angle => angle);
        return currentAngles.SequenceEqual(targetAngles);
    }

    private void CancelSelection()
    {
        if (!_isWaitingForCell)
            return;

        _isWaitingForCell = false;
        SelectionDismissed?.Invoke();
    }
}
