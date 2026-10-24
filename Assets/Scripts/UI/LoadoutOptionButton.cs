using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LoadoutOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = new Color(0.61f, 0.42f, 0.91f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.6f, 0.12f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.45f);

    private bool isLocked;

    public event Action HoverEntered;
    public event Action HoverExited;

    public Button Button => button;

    public void SetLabel(string text)
    {
        label.text = text;
    }

    public void SetSelected(bool isSelected)
    {
        background.color = isLocked ? lockedColor : (isSelected ? selectedColor : normalColor);
    }

    // Locked buttons cannot be clicked. The locked colour shows on the next SetSelected
    public void SetLocked(bool locked)
    {
        isLocked = locked;
        button.interactable = !locked;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HoverEntered?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverExited?.Invoke();
    }
}
