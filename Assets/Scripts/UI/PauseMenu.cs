using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private LoadoutMenu loadoutMenu;
    [SerializeField] private UnityTransport transport;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;

    private int shownPlayers = -1;
    private int shownPing = -2;
    private bool shownIsHost;

    void Start()
    {
        resumeButton.onClick.AddListener(Close);
        menuButton.onClick.AddListener(LeaveToMenu);
        panel.SetActive(false);
    }

    void Update()
    {
        PlayerController local = PlayerController.Local;

        if (local == null)
        {
            panel.SetActive(false);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (panel.activeSelf)
            {
                Close();
            }
            else
            {
                Open(local);
            }
        }

        if (panel.activeSelf)
        {
            RefreshInfo();
        }
    }

    private void Open(PlayerController local)
    {
        panel.SetActive(true);
        local.InputReader.SetBlocked(true);
        shownPlayers = -1;
    }

    private void Close()
    {
        panel.SetActive(false);

        if (PlayerController.Local == null) return;

        bool keepBlocked = loadoutMenu.IsOpen || MatchManager.Instance.Phase == MatchPhase.MatchOver;

        PlayerController.Local.InputReader.SetBlocked(keepBlocked);
    }

    private void LeaveToMenu()
    {
        panel.SetActive(false);
        MenuLaunch.ReturnToMainMenu();
    }

    private void RefreshInfo()
    {
        NetworkManager network = NetworkManager.Singleton;
        int players = MatchParticipant.All.Count;
        int ping = network.IsHost ? -1 : (int)transport.GetCurrentRtt(NetworkManager.ServerClientId);

        if (players == shownPlayers && ping == shownPing && network.IsHost == shownIsHost) return;

        shownPlayers = players;
        shownPing = ping;
        shownIsHost = network.IsHost;
        infoText.text = (network.IsHost ? "HOST" : "CLIENT") + "   players: " + players + "   ping: " + (ping < 0 ? "-" : ping + " ms");
    }
}
