using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmPurchasePopup : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action pendingConfirm;
    private bool isShowing;

    // Awake can run during the first Show (if the popup was hidden before it woke), so it only hides when not showing
    void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Hide);

        if (!isShowing)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show(string title, string body, bool canConfirm, Action onConfirm)
    {
        titleText.text = title;
        bodyText.text = body;
        confirmButton.interactable = canConfirm;
        pendingConfirm = onConfirm;
        isShowing = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        pendingConfirm = null;
        isShowing = false;
        gameObject.SetActive(false);
    }

    private void Confirm()
    {
        Action confirm = pendingConfirm;

        Hide();
        confirm?.Invoke();
    }
}
