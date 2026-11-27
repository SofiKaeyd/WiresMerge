using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class HintAnimationManager : IDisposable
{
    private const float InstructionFadeDuration = 0.2f;
    private const float PulseScaleMultiplier = 1.1f;
    private const float PulseDuration = 0.35f;
    private const float ReturnToNormalDuration = 0.2f;

    private readonly Text _instruction;
    private readonly Color _instructionColor;
    private readonly Vector2 _instructionPosition;
    private readonly Dictionary<WireCell, Tween> _cellTweens = new Dictionary<WireCell, Tween>();
    private readonly Dictionary<WireCell, Vector3> _originalScales = new Dictionary<WireCell, Vector3>();
    private readonly HashSet<WireCell> _pulsingCells = new HashSet<WireCell>();
    private Tween _instructionTween;

    public HintAnimationManager(Text instruction)
    {
        if (instruction == null)
            throw new ArgumentNullException(nameof(instruction));

        _instruction = instruction;
        _instructionColor = instruction.color;
        _instructionPosition = instruction.rectTransform.anchoredPosition;
        Reset();
    }

    public void ShowInstruction()
    {
        _instructionTween?.Kill();
        _instruction.text = "Нажмите на провод, который хотите проверить";
        _instruction.gameObject.SetActive(true);
        _instruction.color = WithAlpha(_instructionColor, 0f);
        _instruction.rectTransform.anchoredPosition = _instructionPosition + Vector2.down * 12f;

        _instructionTween = DOTween.Sequence()
            .Join(_instruction.DOFade(_instructionColor.a, InstructionFadeDuration))
            .Join(_instruction.rectTransform
                .DOAnchorPos(_instructionPosition, InstructionFadeDuration)
                .SetEase(Ease.OutCubic));
    }

    public void HideInstruction()
    {
        _instructionTween?.Kill();
        if (!_instruction.gameObject.activeSelf)
            return;

        _instructionTween = DOTween.Sequence()
            .Join(_instruction.DOFade(0f, InstructionFadeDuration))
            .Join(_instruction.rectTransform
                .DOAnchorPos(_instructionPosition + Vector2.up * 10f, InstructionFadeDuration)
                .SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                _instruction.text = string.Empty;
                _instruction.rectTransform.anchoredPosition = _instructionPosition;
                _instruction.gameObject.SetActive(false);
            });
    }

    public void ShowCellState(WireCell cell, bool isCorrect)
    {
        if (cell == null)
            return;

        if (!_originalScales.ContainsKey(cell))
            _originalScales.Add(cell, cell.transform.localScale);

        if (isCorrect)
        {
            if (!_pulsingCells.Remove(cell))
                return;

            if (_cellTweens.TryGetValue(cell, out var currentTween))
                currentTween.Kill();

            var targetScale = _originalScales[cell];
            _cellTweens[cell] = cell.transform
                .DOScale(targetScale, ReturnToNormalDuration)
                .SetEase(Ease.OutSine)
                .OnComplete(() =>
                {
                    if (cell != null)
                        cell.transform.localScale = targetScale;

                    _cellTweens.Remove(cell);
                    _originalScales.Remove(cell);
                });
            return;
        }

        if (!_pulsingCells.Add(cell))
            return;

        if (_cellTweens.TryGetValue(cell, out var tween))
            tween.Kill();

        var originalScale = _originalScales[cell];
        _cellTweens[cell] = cell.transform
            .DOScale(originalScale * PulseScaleMultiplier, PulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void Reset()
    {
        _instructionTween?.Kill();
        _instructionTween = null;
        _instruction.text = string.Empty;
        _instruction.color = _instructionColor;
        _instruction.rectTransform.anchoredPosition = _instructionPosition;
        _instruction.gameObject.SetActive(false);

        foreach (var cell in _originalScales)
        {
            if (_cellTweens.TryGetValue(cell.Key, out var tween))
                tween.Kill();

            if (cell.Key != null)
                cell.Key.transform.localScale = cell.Value;
        }

        _cellTweens.Clear();
        _originalScales.Clear();
        _pulsingCells.Clear();
    }

    public void Dispose()
    {
        Reset();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
