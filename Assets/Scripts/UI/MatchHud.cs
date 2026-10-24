using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchHud : MonoBehaviour
{
    private const float GO_SECONDS = 0.8f;

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bannerText;

    private int shownSeconds = -1;
    private int shownBanner = -2;
    private int shownScoreA = -1;
    private int shownScoreB = -1;
    private MatchParticipant shownLeader;
    private GameMode shownMode;
    private MatchPhase shownPhase = (MatchPhase)(-1);

    void Update()
    {
        MatchManager match = MatchManager.Instance;
        bool isPlaying = match.Phase == MatchPhase.Playing;

        if (match.Phase != shownPhase)
        {
            shownPhase = match.Phase;
            shownSeconds = -1;
            shownBanner = -2;
            shownScoreA = -1;
            timerText.text = "";
            scoreText.text = "";
            bannerText.text = "";
        }

        if (!isPlaying) return;

        RefreshTimer(match);
        RefreshBanner(match);
        RefreshScore(match);
    }

    private void RefreshTimer(MatchManager match)
    {
        int seconds = Mathf.CeilToInt(match.TimeRemaining);

        if (seconds == shownSeconds) return;

        shownSeconds = seconds;
        timerText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }

    private void RefreshBanner(MatchManager match)
    {
        float sinceStart = (float)(match.Now - match.MatchStartTime);
        int banner = sinceStart < 0f ? Mathf.CeilToInt(-sinceStart) : (sinceStart < GO_SECONDS ? 0 : -1);

        if (banner == shownBanner) return;

        shownBanner = banner;
        bannerText.text = banner > 0 ? banner.ToString() : (banner == 0 ? "GO!" : "");
    }

    private void RefreshScore(MatchManager match)
    {
        if (match.Mode == GameMode.TeamDeathmatch)
        {
            int blue = match.GetTeamKills(0);
            int red = match.GetTeamKills(1);

            if (shownMode == match.Mode && blue == shownScoreA && red == shownScoreB) return;

            shownMode = match.Mode;
            shownScoreA = blue;
            shownScoreB = red;
            scoreText.text = "BLUE " + blue + "  -  " + red + " RED   (first to " + match.KillLimit + ")";
            return;
        }

        MatchParticipant leader = match.GetLeader();
        int leaderKills = leader == null ? 0 : leader.Kills;

        if (shownMode == match.Mode && leader == shownLeader && leaderKills == shownScoreA) return;

        shownMode = match.Mode;
        shownLeader = leader;
        shownScoreA = leaderKills;
        scoreText.text = leader == null ? "" : "Leader: " + leader.DisplayName + " " + leaderKills + (leaderKills == 1 ? " kill" : " kills");
    }
}
