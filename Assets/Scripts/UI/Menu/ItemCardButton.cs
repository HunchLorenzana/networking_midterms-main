using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Color ownedColor = new Color(0.61f, 0.42f, 0.91f);
    [SerializeField] private Color equippedColor = new Color(1f, 0.6f, 0.12f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.45f);

    public event Action HoverEntered;
    public event Action HoverExited;

    public Button Button => button;

    public void SetContent(string itemName, string info)
    {
        nameText.text = itemName;
        infoText.text = info;
    }

    public void SetState(bool isEquipped, bool isLocked)
    {
        background.color = isEquipped ? equippedColor : (isLocked ? lockedColor : ownedColor);
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
