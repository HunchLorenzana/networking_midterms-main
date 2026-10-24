using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject screenMenu;
    [SerializeField] private GameObject screenLobby;
    [SerializeField] private GameObject screenGame;
    [SerializeField] private GameObject screenResult;

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool hasPlayer = PlayerController.Local != null;
        MatchPhase phase = MatchManager.Instance.Phase;

        SetActive(screenMenu, !hasPlayer);
        SetActive(screenLobby, hasPlayer && phase == MatchPhase.Lobby);
        SetActive(screenGame, hasPlayer);
        SetActive(screenResult, hasPlayer && phase == MatchPhase.MatchOver);
    }

    private static void SetActive(GameObject target, bool isActive)
    {
        if (target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
