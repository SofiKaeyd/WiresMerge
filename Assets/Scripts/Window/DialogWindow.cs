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

    private DialogSystem _dialogSystem;
    private float _delay = 0.05f;
    private bool _isTyping = false;
    private CancellationTokenSource _cancellationTokenSource;
    private string _textToType = "";
    private TMP_SpriteAsset _defaultPersSpriteAsset;
    private bool _isOpen;

    public Collider2D Collider => _collider;

    private void Awake()
    {
        _defaultPersSpriteAsset = _persText.spriteAsset;
    }

    public void Start()
    {
        MouseManager.AddClickable(this);
    }

    public override async UniTask OnOpen()
    {
        Debug.Log("On Open Dialog");
        _isOpen = true;
        await base.OnOpen();

        _dialogSystem = Gameplay.DialogSystem;

        _dialogSystem.OnNextStep += UpdateUI;
        _dialogSystem.Start(LevelManager.LastDialogNodeId);

        _pauseButton.OnButtonClick += OnPauseClick;
        MouseManager.AddClickable(_pauseButton);
    }

    private void OnPauseClick(BaseButton button)
    {
        WindowManager.Open<PausePopup>();
    }

    private void OnButtonClick(BaseButton button)
    {
        if (_isTyping)
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
        if (_isOpen)
            ShowUserChoices();
    }

    private void OnAnimationInterrupted()
    {
        _isTyping = false;

        if (!string.IsNullOrEmpty(_textToType))
        {
            _persText.text = _textToType;
        }

        if (_isOpen)
            ShowUserChoices();
    }

    private void ShowUserChoices()
    {
        if (!_isOpen || _dialogSystem?.NextDialogNodes == null)
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

    private void StopAnimation()
    {
        _cancellationTokenSource?.Cancel();
        _persText.text = _textToType;
        _isTyping = false;
    }

    public override UniTask OnClose()
    {
        _isOpen = false;
        StopAnimation();

        _dialogSystem.OnNextStep -= UpdateUI;
        _pauseButton.OnButtonClick -= OnPauseClick;
        MouseManager.RemoveClickable(_pauseButton);

        ReleaseUserChoices();
        _persText.spriteAsset = _defaultPersSpriteAsset;
        _persText.text = "";

        return base.OnClose();
    }
}
