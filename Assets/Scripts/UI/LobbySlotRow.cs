using TMPro;
using UnityEngine;

public class LobbySlotRow : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f);

    private MatchParticipant shownParticipant;
    private string shownName;
    private string shownLoadout;
    private bool shownReady;
    private int shownTeam = -2;
    private bool hasShown;
    private bool isShowingEmpty;

    public void Show(MatchParticipant participant, int slotNumber, bool isOpen)
    {
        if (!isOpen)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (participant == null)
        {
            if (isShowingEmpty) return;

            hasShown = true;
            shownParticipant = null;
            isShowingEmpty = true;
            infoText.text = "P" + slotNumber + "  Waiting...";
            infoText.color = emptyColor;
            statusText.text = "";
            return;
        }

        string playerName = participant.DisplayName;
        string loadout = participant.GetLoadoutLabel();
        bool isReady = participant.IsReady;
        int team = participant.TeamId;

        if (hasShown && shownParticipant == participant && shownName == playerName && shownLoadout == loadout && shownReady == isReady && shownTeam == team) return;

        hasShown = true;
        shownParticipant = participant;
        isShowingEmpty = false;
        shownName = playerName;
        shownLoadout = loadout;
        shownReady = isReady;
        shownTeam = team;

        infoText.color = Color.white;
        infoText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(participant.DisplayColor) + ">P" + slotNumber + "</color>  " + playerName + "\n<size=55%>" + loadout + "</size>";

        string teamText = team >= 0 ? MatchManager.GetTeamName(team) + "  " : "";
        string readyText = participant.IsBot ? "BOT" : (participant.IsHostPlayer ? "HOST" : (isReady ? "READY" : "NOT READY"));

        statusText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(team >= 0 ? MatchManager.GetTeamColor(team) : Color.white) + ">" + teamText + "</color>" + readyText;
    }
}
