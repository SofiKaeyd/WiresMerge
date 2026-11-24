using Cysharp.Threading.Tasks;
using System;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GameWindow : BaseWindow
{
    private const float CelebrationDuration = 4f;
    private const float PopupRevealDuration = 1f;
    private const float ConfettiBurstDuration = 0.5f;
    private const float ConfettiFallDuration = 2f;
    private const int ConfettiPieceCount = 100;

    private static readonly Color32[] ConfettiColors =
    {
        new Color32(255, 75, 92, 255),
        new Color32(255, 196, 61, 255),
        new Color32(65, 205, 190, 255),
        new Color32(117, 103, 255, 255),
        new Color32(255, 255, 255, 255),
        new Color32(255, 125, 48, 255)
    };

    [SerializeField] private BaseButton _pauseButton;
    [SerializeField] private Text _timerValue;
    [SerializeField] private Text _levelLabel;
    [SerializeField] private Text _answerStats;

    private bool _winStarted;

    public override async UniTask OnOpen()
    {
        _winStarted = false;
        _pauseButton.gameObject.SetActive(true);
        _levelLabel.text = LevelManager.CurrentLevelLabel;
        UpdateAnswerStatistics();
        Gameplay.Started += OnStarted;
        Gameplay.Finished += OnFinished;
        Gameplay.AnswerStatisticsChanged += UpdateAnswerStatistics;
        Gameplay.WireSystem.Win += OnWIn;
        _pauseButton.OnButtonClick += OnPauseClick;
        MouseManager.AddClickable(_pauseButton);
    }

    private void OnWIn()
    {
        if (_winStarted)
            return;

        _winStarted = true;
        Gameplay.TimerSystem.IsRunning = false;
        LevelManager.LoadLevel(LevelManager.LastId + 1);
        Gameplay.Win();
        AudioManager.PlayOneShot(Sound.WinPopup);
        Debug.Log("Score: " + Gameplay.Score);
        _pauseButton.gameObject.SetActive(false);
        MouseManager.RemoveClickable(_pauseButton);
        PlayVictoryCelebration();
    }

    private void PlayVictoryCelebration()
    {
        var celebrationCanvas = CreateCelebrationCanvas();
        CreateConfetti(celebrationCanvas.transform);

        DOTween.Sequence()
            .SetUpdate(true)
            .AppendInterval(CelebrationDuration - PopupRevealDuration)
            .AppendCallback(() => WindowManager.Open<WinPopup>())
            .AppendInterval(PopupRevealDuration)
            .OnComplete(() =>
            {
                if (celebrationCanvas != null)
                    Destroy(celebrationCanvas);
            });
    }

    private static GameObject CreateCelebrationCanvas()
    {
        var canvasObject = new GameObject(
            "VictoryCelebration",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private static void CreateConfetti(Transform parent)
    {
        var random = new System.Random();
        var bounds = new Vector2(1920f, 1080f);

        for (var i = 0; i < ConfettiPieceCount; i++)
        {
            var piece = new GameObject(
                "Confetti",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            piece.transform.SetParent(parent, false);

            var image = piece.GetComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = ConfettiColors[random.Next(ConfettiColors.Length)];
            image.raycastTarget = false;

            var rect = (RectTransform)piece.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(
                RandomRange(random, 8f, 18f),
                RandomRange(random, 6f, 14f));

            var originX = random.Next(2) == 0 ? -220f : 220f;
            rect.anchoredPosition = new Vector2(originX, -100f);
            rect.localRotation = Quaternion.Euler(0f, 0f, RandomRange(random, 0f, 360f));

            var burstPosition = rect.anchoredPosition + new Vector2(
                RandomRange(random, -bounds.x * 0.48f, bounds.x * 0.48f),
                RandomRange(random, 80f, bounds.y * 0.48f));
            var fallPosition = burstPosition + new Vector2(
                RandomRange(random, -280f, 280f),
                -bounds.y * 0.62f);
            var spin = RandomRange(random, -1000f, 1000f);
            var delay = RandomRange(random, 0f, 0.3f);

            DOTween.Sequence()
                .SetUpdate(true)
                .SetDelay(delay)
                .Append(rect.DOAnchorPos(burstPosition, ConfettiBurstDuration).SetEase(Ease.OutCubic))
                .Append(rect.DOAnchorPos(fallPosition, ConfettiFallDuration).SetEase(Ease.InQuad))
                .Join(rect.DORotate(
                    new Vector3(0f, 0f, spin),
                    ConfettiBurstDuration + ConfettiFallDuration,
                    RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear))
                .Insert(ConfettiFallDuration, image.DOFade(0f, ConfettiBurstDuration))
                .OnComplete(() =>
                {
                    if (piece != null)
                        Destroy(piece);
                });
        }
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private void OnFinished()
    {
        Gameplay.Started += OnStarted;
    }

    private void OnStarted()
    {
        Debug.Log("on started game window");
        _timerValue.text = LevelManager.LastTimerDuration.ToString();
        UpdateAnswerStatistics();
        Gameplay.TimerSystem.IntervalElapsed += OnIntervalElapsed;
        Gameplay.TimerSystem.TimerElapsed += OnTimerElapsed;
    }

    private void OnPauseClick(BaseButton button)
    {
        WindowManager.Open<PausePopup>();
    }

    private void OnIntervalElapsed(float remained)
    {
        _timerValue.text = ((int)remained).ToString();
    }

    private void OnTimerElapsed()
    {
        Debug.Log("LOSE");
        Gameplay.RecordLoss();
        WindowManager.Open<LosePopup>();
    }

    private void UpdateAnswerStatistics()
    {
        _answerStats.text =
            $"Статистика ответов: <color=#00FF00>{Gameplay.CorrectAnswers}</color> <color=#FF0000>{Gameplay.WrongAnswers}</color>";
    }

    public override async UniTask OnClose()
    {
        _pauseButton.OnButtonClick -= OnPauseClick;
        MouseManager.RemoveClickable(_pauseButton);

        Gameplay.Finished -= OnFinished;
        Gameplay.AnswerStatisticsChanged -= UpdateAnswerStatistics;
        Gameplay.WireSystem.Win -= OnWIn;
        Gameplay.Finish();
    }
}
