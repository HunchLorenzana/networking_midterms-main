using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatBar : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image previewFillImage;
    [SerializeField] private Color increaseColor = new Color(0.35f, 0.9f, 0.4f);
    [SerializeField] private Color decreaseColor = new Color(0.95f, 0.3f, 0.3f);

    public void SetLabel(string label)
    {
        labelText.text = label;
    }

    public void Show(float fill, string value)
    {
        fillImage.fillAmount = Mathf.Clamp01(fill);
        previewFillImage.fillAmount = 0f;
        valueText.text = value;
    }

    // The bar keeps the smaller fill, and the coloured part shows the gain or loss
    public void ShowPreview(float fill, string value, float previewFill, string previewValue)
    {
        if (Mathf.Approximately(fill, previewFill))
        {
            Show(fill, value);

            return;
        }

        bool isIncrease = previewFill > fill;
        Color color = isIncrease ? increaseColor : decreaseColor;

        fillImage.fillAmount = Mathf.Clamp01(Mathf.Min(fill, previewFill));
        previewFillImage.fillAmount = Mathf.Clamp01(Mathf.Max(fill, previewFill));
        previewFillImage.color = color;
        valueText.text = value + " <color=#" + ColorUtility.ToHtmlStringRGB(color) + ">> " + previewValue + "</color>";
    }
}
