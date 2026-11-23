using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogChoiceButton : BaseButton
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Image _backgroundImage;

    private TMP_SpriteAsset _defaultSpriteAsset;

    public DialogNode Node { get; set; }

    private void Awake()
    {
        _defaultSpriteAsset = _text.spriteAsset;
    }

    public void SetText(string text, bool leadsToLevel, TMP_SpriteAsset inlineImageAsset = null)
    {
        _text.spriteAsset = inlineImageAsset != null ? inlineImageAsset : _defaultSpriteAsset;

        _text.text = leadsToLevel
            ? $"{text}  <color=blue> ></color>"
            : text;
        SetHighlighted(false);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (_backgroundImage.gameObject.activeSelf != isHighlighted)
            _backgroundImage.gameObject.SetActive(isHighlighted);
    }

    public override void OnClick()
    {
        base.OnClick();
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_text != null)
            _text.spriteAsset = _defaultSpriteAsset;
    }

    private void OnDestroy()
    {
        Dispose();
    }
}
