using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BaseInputField : MonoBehaviour, IClickable, IDisposable
{
    [SerializeField] private Collider2D _collider;
    [SerializeField] private Text _initialText;
    [SerializeField] private Text _displayText;
    [SerializeField] private int _characterLimit = 20;
    [SerializeField] private Image _background;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _unselectedColor = Color.white;
    [SerializeField] private Text caretText;

    private bool _isSelected = false;
    private InputField _inputField;
    private bool _inputFieldInitialized;

    private string _lastText;
    private bool caretVisible = true;
    private float caretBlinkRate = 0.5f;
    private float caretTimer = 0f;

    public Collider2D Collider => _collider;
    public string InitialTextValue => _initialText.text;
    public string DisplayTextValue => _displayText.text;
    public Action<string> OnValueChanged { get; set; }

    public void Init(string initialText, string displayText)
    {
        InitializeInputField();
        _isSelected = false;
        _initialText.text = initialText;
        _inputField.text = displayText;
        UpdateVisuals();
    }

    public virtual void OnClick()
    {
        _isSelected = true;
        _inputField.ActivateInputField();
        caretText.gameObject.SetActive(true);
        UpdateVisuals();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var mousePosition = CameraManager.MainCamera.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.Raycast(mousePosition, Vector2.zero);
            if (_isSelected && hit.collider != _collider)
            {
                _isSelected = false;
                _inputField.DeactivateInputField();
                UpdateVisuals();
            }
        }
        UpdateCaret();
    }

    private void InitializeInputField()
    {
        EnsureEventSystem();

        if (_inputField == null)
            _inputField = GetComponent<InputField>();

        if (_inputField == null)
            _inputField = gameObject.AddComponent<InputField>();

        if (_inputFieldInitialized)
            return;

        _inputField.textComponent = _displayText;
        _inputField.placeholder = _initialText;
        _inputField.targetGraphic = _background;
        _inputField.transition = Selectable.Transition.None;
        _inputField.lineType = InputField.LineType.SingleLine;
        _inputField.characterLimit = Mathf.Max(0, _characterLimit);
        _inputField.caretWidth = 0;
        _inputField.onValueChanged.AddListener(HandleInputValueChanged);
        _inputField.onEndEdit.AddListener(HandleInputEndEdit);
        _inputFieldInitialized = true;
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
    }

    private void HandleInputValueChanged(string text)
    {
        _displayText.text = text;
        UpdateVisuals();
        OnValueChanged?.Invoke(text);
    }

    private void HandleInputEndEdit(string text)
    {
        _isSelected = false;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _background.color = _isSelected ? _selectedColor : _unselectedColor;
        _initialText.gameObject.SetActive(string.IsNullOrEmpty(_displayText.text));
    }

    private void UpdateCaret()
    {
        if (_isSelected)
        {
            caretTimer += Time.deltaTime;
            if (caretTimer >= caretBlinkRate)
            {
                caretVisible = !caretVisible;
                caretText.gameObject.SetActive(caretVisible);
                caretTimer = 0f;
            }

            if (_lastText != _displayText.text)
            {
                UpdateCaretPosition();
                _lastText = _displayText.text;
            }
        }
        else
        {
            caretText.gameObject.SetActive(false);
        }
    }

    private void UpdateCaretPosition()
    {
        if (string.IsNullOrEmpty(_displayText.text))
        {
            caretText.rectTransform.anchoredPosition = new Vector2(0, 0);
            return;
        }

        float textWidth = _displayText.preferredWidth;
        float xOffset = 0f;

        switch (_displayText.alignment)
        {
            case TextAnchor.MiddleCenter:
                xOffset = -textWidth * 0.5f;
                break;
            default:
                xOffset = 0f;
                break;
        }

        float caretOffset = 2f;
        float caretX = xOffset + textWidth + caretOffset;
        caretText.rectTransform.anchoredPosition = new Vector2(caretX, 0);
    }

    public virtual void Dispose()
    {
        if (_inputFieldInitialized)
        {
            _inputField.onValueChanged.RemoveListener(HandleInputValueChanged);
            _inputField.onEndEdit.RemoveListener(HandleInputEndEdit);
            _inputFieldInitialized = false;
        }

        OnValueChanged = null;
    }
}
