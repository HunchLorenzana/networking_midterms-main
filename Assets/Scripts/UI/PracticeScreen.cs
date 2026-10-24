using UnityEngine;
using UnityEngine.UI;

// Shown during the lobby phase in the Match scene. Its panel is visible only in practice, since a real match leaves the lobby phase right away
public class PracticeScreen : MonoBehaviour
{
    [SerializeField] private MatchDirector director;
    [SerializeField] private GameObject panel;
    [SerializeField] private LoadoutMenu loadoutMenu;
    [SerializeField] private CharacterMenu characterMenu;
    [SerializeField] private Button loadoutButton;
    [SerializeField] private Button characterButton;
    [SerializeField] private Button mainMenuButton;

    void Start()
    {
        loadoutButton.onClick.AddListener(loadoutMenu.Open);
        characterButton.onClick.AddListener(characterMenu.Open);
        mainMenuButton.onClick.AddListener(MenuLaunch.ReturnToMainMenu);
    }

    void OnEnable()
    {
        panel.SetActive(director.IsPractice);
    }
}
