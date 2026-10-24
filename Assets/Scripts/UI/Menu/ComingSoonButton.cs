using UnityEngine;
using UnityEngine.UI;

public class ComingSoonButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private string featureName;

    void Start()
    {
        button.onClick.AddListener(() => screenManager.ShowComingSoon(featureName));
    }
}
