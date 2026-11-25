using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogWindow : BaseWindow, IClickable
{
    [SerializeField] private Collider2D _collider;
    [SerializeField] private TextMeshProUGUI _persText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GridLayoutGroup _choicesGrid;
    [SerializeField] private List<DialogChoiceButton> _userChoices = new List<DialogChoiceButton>();
    [SerializeField] private BaseButton _pauseButton;
    [SerializeField] private BaseButton _skipTrainingButton;
    [SerializeField] private Image _mainBackground;
    [SerializeField] private Image _dialogueBackground;
    [SerializeField] private Sprite _homeOutsideBackground;

    private DialogSystem _dialogSystem;
    private float _delay = 0.05f;
    private bool _isTyping = false;
    private bool _isStoryAnimationRunning;
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationTokenSource _storyAnimationCancellation;
    private string _textToType = "";
    private bool _skipTrainingRegistered;
    private TMP_SpriteAsset _defaultPersSpriteAsset;
    private Sprite _defaultBackgroundSprite;
    private Color _defaultMainBackgroundColor;
    private Color _defaultDialogueBackgroundColor;
    private RawImage _lightOverlay;
    private RawImage _darkeningOverlay;
    private bool _isOpen;
    private CancellationTokenSource _persistentLightFlickerCancellation;

    public Collider2D Collider => _collider;

    private void Awake()
    {
        _defaultPersSpriteAsset = _persText.spriteAsset;
        if (_mainBackground == null || _dialogueBackground == null)
        {
            Debug.LogError("DialogWindow requires references to both background images.", this);
            return;
        }

        _defaultBackgroundSprite = _mainBackground.sprite;
        _defaultMainBackgroundColor = _mainBackground.color;
        _defaultDialogueBackgroundColor = _dialogueBackground.color;
        _darkeningOverlay = CreateOverlay("BackgroundDarkness", Color.clear);
        _lightOverlay = CreateOverlay("color", Color.clear);
    }

    public void Start()
    {
        MouseManager.AddClickable(this);
    }

    public override async UniTask OnOpen()
    {
        Debug.Log("On Open Dialog");
        _isOpen = true;
        ResetStoryVisuals();
        await base.OnOpen();

        _dialogSystem = Gameplay.DialogSystem;

        _dialogSystem.OnNextStep += UpdateUI;
        _dialogSystem.Start(LevelManager.LastDialogNodeId);

        _pauseButton.OnButtonClick += OnPauseClick;
        MouseManager.AddClickable(_pauseButton);

        _skipTrainingButton.gameObject.SetActive(LevelManager.IsTrainingLevel);
        if (LevelManager.IsTrainingLevel)
        {
            _skipTrainingButton.OnButtonClick += OnSkipTrainingClick;
            MouseManager.AddClickable(_skipTrainingButton);
            _skipTrainingRegistered = true;
        }
    }

    private void OnPauseClick(BaseButton button)
    {
        WindowManager.Open<PausePopup>();
    }

    private void OnSkipTrainingClick(BaseButton button)
    {
        StopAnimation();
        if (!LevelManager.SkipTraining())
        {
            Debug.LogError("Cannot skip training: working level 1_1 was not found.");
            return;
        }

        WindowManager.Open<GameWindow>();
        LevelManager.ShowLevel();
    }

    private void OnButtonClick(BaseButton button)
    {
        if (_isTyping || _isStoryAnimationRunning)
            return;

        var success = _dialogSystem.NextStep((button as DialogChoiceButton).Node);
        if (!success && LevelManager.IsNextExist)
        {
            LoadGame();
        }
        else if (!success)
        {
            WindowManager.Open<RestartPopup>();
        }
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            OnClick();

        UpdateChoiceHighlight();
    }

    private void UpdateChoiceHighlight()
    {
        if (_userChoices.Count == 0)
            return;

        var mousePosition = CameraManager.MainCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(mousePosition, Vector2.zero);

        foreach (var choice in _userChoices)
            choice.SetHighlighted(choice.Collider == hit.collider);
    }

    public void OnClick()
    {
        if (_isStoryAnimationRunning)
            return;

        if (_isTyping)
        {
            StopAnimation();
            return;
        }

        if (_userChoices.Count != 0)
            return;

        var success = _dialogSystem.NextPersNode();
        if (!success && LevelManager.IsNextExist)
        {
            LoadGame();
        }
        else if (!success)
        {
            WindowManager.Open<RestartPopup>();
        }
    }

    private void LoadGame()
    {
        Debug.Log("GAME");
        WindowManager.Open<GameWindow>();
        LevelManager.ShowLevel();
    }

    private void UpdateUI()
    {
        ReleaseUserChoices();

        var currentNode = _dialogSystem.CurrentRootNode;
        _persText.spriteAsset = currentNode.InlineImageAsset != null
            ? currentNode.InlineImageAsset
            : _defaultPersSpriteAsset;
        _persText.text = "";

        if (!currentNode.IsPlayer)
        {
            _nameText.text = currentNode.Speaker;
            _textToType = GetFormattedText(currentNode);
            StartStoryAnimation(currentNode);
            StartAnimation();
        }
        else
        {
            ShowUserChoices();
        }
    }

    private async UniTask TypeText(string textToType, CancellationToken token)
    {
        _isTyping = true;
        _persText.text = "";

        _choicesGrid.gameObject.SetActive(false);

        try
        {
            if (string.IsNullOrEmpty(textToType))
            {
                _persText.text = "";
                OnAnimationComplete();
                return;
            }

            for (var index = 0; index < textToType.Length;)
            {
                token.ThrowIfCancellationRequested();

                if (textToType[index] == '<')
                {
                    var tagEnd = textToType.IndexOf('>', index);
                    if (tagEnd >= 0)
                    {
                        _persText.text += textToType.Substring(index, tagEnd - index + 1);
                        index = tagEnd + 1;
                        continue;
                    }
                }

                _persText.text += textToType[index];
                index++;
                await UniTask.Delay((int)(_delay * 700));
            }

            OnAnimationComplete();
        }
        catch (OperationCanceledException)
        {
            OnAnimationInterrupted();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in TypeText: {ex.Message}");
            OnAnimationInterrupted();
        }
    }

    private void OnAnimationComplete()
    {
        _isTyping = false;
        if (_isOpen && !_isStoryAnimationRunning)
            ShowUserChoices();
    }

    private void OnAnimationInterrupted()
    {
        _isTyping = false;

        if (!string.IsNullOrEmpty(_textToType))
        {
            _persText.text = _textToType;
        }

        if (_isOpen && !_isStoryAnimationRunning)
            ShowUserChoices();
    }

    private void ShowUserChoices()
    {
        if (!_isOpen || _isTyping || _isStoryAnimationRunning || _dialogSystem?.NextDialogNodes == null)
            return;

        _choicesGrid.gameObject.SetActive(true);

        foreach (var node in _dialogSystem.NextDialogNodes)
        {
            if (!node.IsPlayer)
                continue;

            var button = Pool.Get<DialogChoiceButton>(_choicesGrid.transform);
            _userChoices.Add(button);
            button.Node = node;
            button.OnButtonClick += OnButtonClick;
            button.SetText(
                GetFormattedText(node),
                LevelManager.IsNextExist && _dialogSystem.IsTerminalChoice(node),
                node.InlineImageAsset);
            MouseManager.AddClickable(button);
        }
    }

    private string GetFormattedText(DialogNode node)
    {
        if (node.HasInlineImages && node.InlineImageAsset == null)
        {
            Debug.LogError(
                $"Dialog node {node.Id} has image URLs but no local image asset. Re-import the graph from Google Sheets.");
            return node.FormattedTextWithImageFallback;
        }

        return node.FormattedText;
    }

    private void ReleaseUserChoices()
    {
        foreach (var choice in _userChoices)
        {
            if (choice == null)
                continue;

            choice.OnButtonClick -= OnButtonClick;
            MouseManager.RemoveClickable(choice);
            Pool.Release(choice);
        }

        _userChoices.Clear();
    }

    private void StartAnimation()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        TypeText(_textToType, _cancellationTokenSource.Token).Forget();
    }

    private void StartStoryAnimation(DialogNode node)
    {
        if (node.HasParameter(DialogNode.StartAnimation1Parameter))
        {
            CancelPersistentLightFlicker();
            _isStoryAnimationRunning = true;
            _storyAnimationCancellation = new CancellationTokenSource();
            RunStartAnimation1(_storyAnimationCancellation.Token).Forget();
        }
        else if (node.HasParameter(DialogNode.StartAnimation2Parameter))
        {
            CancelPersistentLightFlicker();
            _isStoryAnimationRunning = true;
            _storyAnimationCancellation = new CancellationTokenSource();
            RunStartAnimation2(_storyAnimationCancellation.Token).Forget();
        }
        else if (node.HasParameter(DialogNode.StartAnimation3Parameter))
        {
            StartPersistentLightFlicker();
        }
        else if (node.HasParameter(DialogNode.StartAnimation4Parameter))
        {
            CancelPersistentLightFlicker();
            _isStoryAnimationRunning = true;
            _storyAnimationCancellation = new CancellationTokenSource();
            RunStartAnimation4(_storyAnimationCancellation.Token).Forget();
        }
    }

    private async UniTask RunStartAnimation1(CancellationToken token)
    {
        try
        {
            if (_homeOutsideBackground == null)
            {
                Debug.LogError("HomeEnzoOutside sprite is not assigned to DialogWindow.", this);
                return;
            }

            var lightColor = new Color(1f, 0.96f, 0.78f, 0.4f);
            _lightOverlay.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0f);
            await FadeLight(0.4f, 1.5f, lightColor, token);
            await TransitionBackground(_homeOutsideBackground, 0.6f, false, token);
            await FlickerLight(2f, token);
            await TransitionBackground(_defaultBackgroundSprite, 0.6f, true, token);
            StartPersistentLightFlicker();
        }
        finally
        {
            FinishStoryAnimation();
        }
    }

    private async UniTask RunStartAnimation2(CancellationToken token)
    {
        try
        {
            await RunStartAnimation2Core(token);
        }
        finally
        {
            FinishStoryAnimation();
        }
    }

    private async UniTask RunStartAnimation4(CancellationToken token)
    {
        try
        {
            const float duration = 1f;
            const float initialAlpha = 0.4f;
            const float peakAlpha = 0.9f;
            var elapsed = 0f;
            _lightOverlay.color = new Color(1f, 0.96f, 0.78f, initialAlpha);

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var flicker = 0.75f + 0.25f * (0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 12f));
                SetLightAlpha(Mathf.Lerp(initialAlpha, peakAlpha, progress) * flicker);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetLightAlpha(peakAlpha);
            await RunStartAnimation2Core(token);
        }
        finally
        {
            FinishStoryAnimation();
        }
    }

    private async UniTask RunStartAnimation2Core(CancellationToken token)
    {
        const float duration = 1.2f;
        var initialLightAlpha = Mathf.Max(_lightOverlay.color.a, 0.4f);
        const float darknessAlpha = 0.90f;
        var elapsed = 0f;
        var dialogueBackgroundColor = _defaultDialogueBackgroundColor;
        var darkDialogueBackgroundColor = new Color(
            dialogueBackgroundColor.r * 0.88f,
            dialogueBackgroundColor.g * 0.88f,
            dialogueBackgroundColor.b * 0.88f,
            dialogueBackgroundColor.a);

        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var remainingLight = 1f - progress;
            var flicker = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 12f));

            SetLightAlpha(initialLightAlpha * flicker * remainingLight);
            _darkeningOverlay.color = new Color(0f, 0f, 0f, darknessAlpha * progress);
            _dialogueBackground.color = Color.Lerp(
                dialogueBackgroundColor,
                darkDialogueBackgroundColor,
                progress);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetLightAlpha(0f);
        _darkeningOverlay.color = new Color(0f, 0f, 0f, darknessAlpha);
        _dialogueBackground.color = darkDialogueBackgroundColor;
    }

    private async UniTask FadeLight(
        float targetAlpha,
        float duration,
        Color lightColor,
        CancellationToken token)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            var alpha = Mathf.Lerp(0f, targetAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            _lightOverlay.color = new Color(lightColor.r, lightColor.g, lightColor.b, alpha);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        _lightOverlay.color = new Color(lightColor.r, lightColor.g, lightColor.b, targetAlpha);
    }

    private async UniTask TransitionBackground(
        Sprite targetSprite,
        float duration,
        bool flickerLight,
        CancellationToken token)
    {
        await FadeBackgroundAlpha(0f, duration / 2f, flickerLight, token);
        _mainBackground.sprite = targetSprite;
        await FadeBackgroundAlpha(1f, duration / 2f, flickerLight, token);
    }

    private async UniTask FadeBackgroundAlpha(
        float targetAlpha,
        float duration,
        bool flickerLight,
        CancellationToken token)
    {
        var startAlpha = _mainBackground.color.a;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            var color = _defaultMainBackgroundColor;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            _mainBackground.color = color;
            if (flickerLight)
                SetLightAlpha(GetSoftFlickerAlpha(Time.unscaledTime));

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        var finalColor = _defaultMainBackgroundColor;
        finalColor.a = targetAlpha;
        _mainBackground.color = finalColor;
    }

    private async UniTask FlickerLight(float duration, CancellationToken token)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            SetLightAlpha(GetSoftFlickerAlpha(elapsed));
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetLightAlpha(0.4f);
    }

    private void StartPersistentLightFlicker()
    {
        CancelPersistentLightFlicker();
        var alpha = _lightOverlay.color.a > 0f ? _lightOverlay.color.a : 0.4f;
        _lightOverlay.color = new Color(1f, 0.96f, 0.78f, alpha);

        _persistentLightFlickerCancellation = new CancellationTokenSource();
        RunPersistentLightFlicker(_persistentLightFlickerCancellation.Token).Forget();
    }

    private async UniTask RunPersistentLightFlicker(CancellationToken token)
    {
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                SetLightAlpha(GetSoftFlickerAlpha(Time.unscaledTime));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPersistentLightFlicker()
    {
        _persistentLightFlickerCancellation?.Cancel();
        _persistentLightFlickerCancellation?.Dispose();
        _persistentLightFlickerCancellation = null;
    }

    private static float GetSoftFlickerAlpha(float time)
    {
        return 0.36f + 0.04f * Mathf.Sin(time * Mathf.PI * 4f);
    }

    private void SetLightAlpha(float alpha)
    {
        var color = _lightOverlay.color;
        color.a = alpha;
        _lightOverlay.color = color;
    }

    private RawImage CreateOverlay(string overlayName, Color color)
    {
        var overlayObject = new GameObject(
            overlayName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        var rectTransform = (RectTransform)overlayObject.transform;
        rectTransform.SetParent(_mainBackground.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetAsFirstSibling();

        var overlay = overlayObject.GetComponent<RawImage>();
        overlay.texture = Texture2D.whiteTexture;
        overlay.color = color;
        overlay.raycastTarget = false;
        return overlay;
    }

    private void FinishStoryAnimation()
    {
        _isStoryAnimationRunning = false;
        _storyAnimationCancellation?.Dispose();
        _storyAnimationCancellation = null;
        if (_isOpen && !_isTyping)
            ShowUserChoices();
    }

    private void CancelStoryAnimation()
    {
        _storyAnimationCancellation?.Cancel();
        _storyAnimationCancellation?.Dispose();
        _storyAnimationCancellation = null;
        _isStoryAnimationRunning = false;
    }

    private void ResetStoryVisuals()
    {
        if (_mainBackground == null || _dialogueBackground == null)
            return;

        _mainBackground.sprite = _defaultBackgroundSprite;
        _mainBackground.color = _defaultMainBackgroundColor;
        _dialogueBackground.color = _defaultDialogueBackgroundColor;
        if (_lightOverlay != null)
            _lightOverlay.color = new Color(1f, 0.96f, 0.78f, 0f);
        if (_darkeningOverlay != null)
            _darkeningOverlay.color = Color.clear;
    }

    private void StopAnimation()
    {
        _cancellationTokenSource?.Cancel();
        _persText.text = _textToType;
        _isTyping = false;
    }

    public override UniTask OnClose()
    {
        _isOpen = false;
        CancelPersistentLightFlicker();
        CancelStoryAnimation();
        StopAnimation();
        ResetStoryVisuals();

        _dialogSystem.OnNextStep -= UpdateUI;
        _pauseButton.OnButtonClick -= OnPauseClick;
        MouseManager.RemoveClickable(_pauseButton);
        if (_skipTrainingRegistered)
        {
            _skipTrainingButton.OnButtonClick -= OnSkipTrainingClick;
            MouseManager.RemoveClickable(_skipTrainingButton);
            _skipTrainingRegistered = false;
        }

        ReleaseUserChoices();
        _persText.spriteAsset = _defaultPersSpriteAsset;
        _persText.text = "";

        return base.OnClose();
    }
}
