using UnityEngine;
using UnityEngine.UI;

public class UIBar : MonoBehaviour
{
    [SerializeField] private RectTransform mask;
    [SerializeField] private Image fill;

    public void SetFraction(float fraction)
    {
        mask.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    public void SetColor(Color color)
    {
        fill.color = color;
    }
}
