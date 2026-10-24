using DG.Tweening;
using TMPro;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    [SerializeField] private GameObject homeScreen;
    [SerializeField] private GameObject workshopScreen;
    [SerializeField] private GameObject comingSoonPanel;
    [SerializeField] private TMP_Text comingSoonText;
    [SerializeField] private float comingSoonSeconds = 1.6f;

    private Tween hideComingSoonTween;

    void Awake()
    {
        comingSoonPanel.SetActive(false);
        ShowHome();
    }

    public void ShowHome()
    {
        workshopScreen.SetActive(false);
        homeScreen.SetActive(true);
    }

    public void ShowWorkshop()
    {
        homeScreen.SetActive(false);
        workshopScreen.SetActive(true);
    }

    public void ShowComingSoon(string featureName)
    {
        comingSoonText.text = featureName + ": coming soon";
        comingSoonPanel.SetActive(true);
        hideComingSoonTween?.Kill();
        hideComingSoonTween = DOVirtual.DelayedCall(comingSoonSeconds, () => comingSoonPanel.SetActive(false)).SetLink(comingSoonPanel);
    }
}
