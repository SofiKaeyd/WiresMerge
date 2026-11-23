using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class WinPopup : BaseWindow
{
    private const float RevealDuration = 1f;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private BaseButton _nextButton;
    [SerializeField] private BaseButton _exitButton;

    private Sequence _revealSequence;
    private bool _buttonsRegistered;

    public override UniTask OnOpen()
    {
        if (_canvasGroup == null)
        {
            Debug.LogError("WinPopup requires a CanvasGroup component.");
            RegisterButtons();
            return UniTask.CompletedTask;
        }

        _canvasGroup.DOKill();
        transform.DOKill();
        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.9f;

        _revealSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_canvasGroup.DOFade(1f, RevealDuration))
            .Join(transform.DOScale(Vector3.one, RevealDuration).SetEase(Ease.OutBack))
            .OnComplete(RegisterButtons);

        return UniTask.CompletedTask;
    }

    private void RegisterButtons()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _nextButton.OnButtonClick += OnNextButton;
        _exitButton.OnButtonClick += OnExitButton;
        MouseManager.AddClickable(_nextButton);
        MouseManager.AddClickable(_exitButton);
        _buttonsRegistered = true;
    }

    private void OnNextButton(BaseButton button)
    {
        WindowManager.Open<DialogWindow>();
    }

    private void OnExitButton(BaseButton button)
    {
        SceneHandler.LoadScene(SceneHandler.MainScene);
        WindowManager.Open<MenuWindow>();
    }

    public override UniTask OnClose()
    {
        _revealSequence?.Kill();
        transform.DOKill();
        _canvasGroup?.DOKill();

        if (_buttonsRegistered)
        {
            _nextButton.OnButtonClick -= OnNextButton;
            _exitButton.OnButtonClick -= OnExitButton;

            MouseManager.RemoveClickable(_nextButton);
            MouseManager.RemoveClickable(_exitButton);
            _buttonsRegistered = false;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;

        return UniTask.CompletedTask;
    }

}

