using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogChoiceButton : BaseButton
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Image _backgroundImage;

    public DialogNode Node { get; set; }

    public void SetText(string text, bool leadsToLevel)
    {
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
}
