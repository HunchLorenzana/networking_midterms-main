using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private LoadoutOptionButton copyButton;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private LobbySlotRow[] rows;
    [SerializeField] private LoadoutOptionButton freeForAllButton;
    [SerializeField] private LoadoutOptionButton teamDeathmatchButton;
    [SerializeField] private LoadoutOptionButton maxPlayersButton;
    [SerializeField] private LoadoutOptionButton botsButton;
    [SerializeField] private LoadoutOptionButton startButton;
    [SerializeField] private LoadoutOptionButton readyButton;
    [SerializeField] private LoadoutOptionButton teamButton;
    [SerializeField] private LoadoutOptionButton loadoutButton;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private LoadoutMenu loadoutMenu;
    [SerializeField] private LoadoutOptionButton characterButton;
    [SerializeField] private CharacterMenu characterMenu;

    private int shownMaxPlayers = -1;
    private int shownBotsState = -1;
    private int shownReadyState = -1;
    private string shownJoinInfo;

    void Start()
    {
        freeForAllButton.SetLabel("FREE-FOR-ALL");
        teamDeathmatchButton.SetLabel("TEAM DEATHMATCH");
        startButton.SetLabel("START MATCH");
        teamButton.SetLabel("SWITCH TEAM");
        loadoutButton.SetLabel("LOADOUT");
        copyButton.SetLabel("COPY");
        copyButton.Button.onClick.AddListener(() => GUIUtility.systemCopyBuffer = MatchManager.Instance.JoinCode);
        freeForAllButton.Button.onClick.AddListener(() => MatchManager.Instance.SetMode(GameMode.FreeForAll));
        teamDeathmatchButton.Button.onClick.AddListener(() => MatchManager.Instance.SetMode(GameMode.TeamDeathmatch));
        maxPlayersButton.Button.onClick.AddListener(() => MatchManager.Instance.CycleMaxPlayers());
        botsButton.Button.onClick.AddListener(() => MatchManager.Instance.SetFillWithBots(!MatchManager.Instance.FillWithBots));
        startButton.Button.onClick.AddListener(() => MatchManager.Instance.StartMatch());
        readyButton.Button.onClick.AddListener(() => PlayerController.Local.Participant.RequestToggleReady());
        teamButton.Button.onClick.AddListener(() => PlayerController.Local.Participant.RequestSwitchTeam());
        loadoutButton.Button.onClick.AddListener(() => loadoutMenu.Open());
        characterButton.SetLabel("CHARACTER");
        characterButton.Button.onClick.AddListener(() => characterMenu.Open());
        nameInput.characterLimit = PlayerNames.MAX_LENGTH;
        nameInput.onEndEdit.AddListener(OnNameSubmitted);
        hintText.text = "";
    }

    void Update()
    {
        MatchManager match = MatchManager.Instance;

        if (PlayerController.Local == null) return;

        bool isHost = NetworkManager.Singleton.IsServer;
        MatchParticipant local = PlayerController.Local.Participant;

        RefreshJoinInfo(match);
        RefreshHostControls(match, isHost);
        RefreshPlayerControls(match, local, isHost);
        RefreshRows(match);
        RefreshHint(match, isHost);

        if (!nameInput.isFocused && nameInput.text != local.DisplayName)
        {
            nameInput.text = local.DisplayName;
        }
    }

    private void OnNameSubmitted(string newName)
    {
        if (PlayerController.Local == null) return;

        PlayerController.Local.Participant.RequestRename(newName);
    }

    private void RefreshJoinInfo(MatchManager match)
    {
        string joinInfo = match.JoinCode.Length == 0 ? "" : "JOIN CODE: " + match.JoinCode;

        if (joinInfo == shownJoinInfo) return;

        shownJoinInfo = joinInfo;
        joinCodeText.text = joinInfo;
        copyButton.gameObject.SetActive(joinInfo.Length > 0);
    }

    private void RefreshHostControls(MatchManager match, bool isHost)
    {
        freeForAllButton.gameObject.SetActive(isHost);
        teamDeathmatchButton.gameObject.SetActive(isHost);
        maxPlayersButton.gameObject.SetActive(isHost);
        botsButton.gameObject.SetActive(isHost);
        startButton.gameObject.SetActive(isHost);

        if (!isHost) return;

        freeForAllButton.SetSelected(match.Mode == GameMode.FreeForAll);
        teamDeathmatchButton.SetSelected(match.Mode == GameMode.TeamDeathmatch);
        startButton.Button.interactable = match.CanStartMatch;

        if (match.MaxPlayers != shownMaxPlayers)
        {
            shownMaxPlayers = match.MaxPlayers;
            maxPlayersButton.SetLabel("MAX PLAYERS: " + shownMaxPlayers);
        }

        int botsState = match.FillWithBots ? 1 : 0;

        if (botsState != shownBotsState)
        {
            shownBotsState = botsState;
            botsButton.SetLabel(match.FillWithBots ? "BOTS: ON" : "BOTS: OFF");
            botsButton.SetSelected(match.FillWithBots);
        }
    }

    private void RefreshPlayerControls(MatchManager match, MatchParticipant local, bool isHost)
    {
        readyButton.gameObject.SetActive(!isHost);
        teamButton.gameObject.SetActive(match.Mode == GameMode.TeamDeathmatch);

        if (isHost) return;

        int readyState = local.IsReady ? 1 : 0;

        if (readyState != shownReadyState)
        {
            shownReadyState = readyState;
            readyButton.SetLabel(local.IsReady ? "CANCEL READY" : "READY");
            readyButton.SetSelected(local.IsReady);
        }
    }

    private void RefreshRows(MatchManager match)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].Show(MatchParticipant.FindBySlot(i + 1), i + 1, i < match.MaxPlayers);
        }
    }

    private void RefreshHint(MatchManager match, bool isHost)
    {
        string hint;

        if (!isHost)
        {
            hint = "Ready up and wait for the host to start.";
        }
        else if (match.CanStartMatch)
        {
            hint = "Ready to start.";
        }
        else
        {
            hint = match.FillWithBots ? "Waiting for players to ready up." : "Need 2+ players, all ready, or turn bots on.";
        }

        if (hintText.text != hint)
        {
            hintText.text = hint;
        }
    }
}
