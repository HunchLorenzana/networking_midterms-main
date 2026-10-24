using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CharacterMenu : MonoBehaviour
{
    private const string PREFS_KEY = "CharacterId";

    [SerializeField] private GameObject panel;
    [SerializeField] private LoadoutOptionButton[] optionButtons;
    [SerializeField] private Button closeButton;
    [SerializeField] private LoadoutMenu loadoutMenu;

    private int shownSelection = -1;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;

            optionButtons[i].Button.onClick.AddListener(() => Select(index));
        }

        closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
        PlayerController.LocalSpawned += HandleLocalSpawned;
        PlayerController.LocalDespawned += HandleLocalDespawned;
    }

    void OnDestroy()
    {
        PlayerController.LocalSpawned -= HandleLocalSpawned;
        PlayerController.LocalDespawned -= HandleLocalDespawned;
    }

    void Update()
    {
        PlayerController local = PlayerController.Local;

        if (local == null || !panel.activeSelf) return;

        if (MatchManager.Instance.Phase != MatchPhase.Lobby)
        {
            Close();
            return;
        }

        int selection = local.Participant.CharacterId;

        if (selection == shownSelection) return;

        shownSelection = selection;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].SetSelected(i == selection);
        }
    }

    public void Open()
    {
        if (PlayerController.Local == null) return;

        loadoutMenu.Close();
        shownSelection = -1;
        panel.SetActive(true);
        PlayerController.Local.InputReader.SetBlocked(true);
    }

    public void Close()
    {
        if (!panel.activeSelf) return;

        panel.SetActive(false);

        if (PlayerController.Local != null)
        {
            PlayerController.Local.InputReader.SetBlocked(false);
        }
    }

    private void Select(int index)
    {
        if (PlayerController.Local == null) return;

        PlayerPrefs.SetInt(PREFS_KEY, index);
        PlayerController.Local.Participant.RequestCharacter(index);
    }

    private void HandleLocalSpawned(PlayerController player)
    {
        StartCoroutine(ApplySavedNextFrame(player));
    }

    private void HandleLocalDespawned()
    {
        panel.SetActive(false);
    }

    private IEnumerator ApplySavedNextFrame(PlayerController player)
    {
        yield return null;

        if (player == null) yield break;

        player.Participant.RequestCharacter(PlayerPrefs.GetInt(PREFS_KEY, 0));
    }
}
