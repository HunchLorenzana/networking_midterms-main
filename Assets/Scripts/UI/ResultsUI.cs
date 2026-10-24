using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ResultsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private ScoreboardRow[] rows;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text rewardText;
    [Tooltip("On in the Match scene: the button returns to the main menu instead of the lobby")]
    [SerializeField] private bool returnsToMainMenu;

    private readonly List<MatchParticipant> ranking = new List<MatchParticipant>();

    void Start()
    {
        returnButton.onClick.AddListener(OnReturnClicked);
    }

    void OnEnable()
    {
        bool isHost = NetworkManager.Singleton.IsServer;

        returnButton.gameObject.SetActive(isHost);
        waitingText.gameObject.SetActive(!isHost);
        SetLocalInputBlocked(true);
        Populate(MatchManager.Instance);
        MatchRewardReporter.RewardGranted += ShowReward;
        rewardText.text = MatchRewardReporter.HasReward ? FormatReward(MatchRewardReporter.LastReward) : "";
    }

    void OnDisable()
    {
        MatchRewardReporter.RewardGranted -= ShowReward;
        SetLocalInputBlocked(false);
    }

    private void SetLocalInputBlocked(bool isBlocked)
    {
        if (PlayerController.Local != null)
        {
            PlayerController.Local.InputReader.SetBlocked(isBlocked);
        }
    }

    private void ShowReward(MatchReward reward)
    {
        rewardText.text = FormatReward(reward);
    }

    private static string FormatReward(MatchReward reward)
    {
        string trophies = (reward.Trophies >= 0 ? "+" : "") + reward.Trophies + " Trophies";
        string text = "+" + reward.Scrap + " Scrap   " + trophies;

        if (reward.IsFirstWinOfDay)
        {
            text += "\nFirst win of the day!";
        }

        return text;
    }

    private void OnReturnClicked()
    {
        if (returnsToMainMenu)
        {
            MenuLaunch.ReturnToMainMenu();

            return;
        }

        MatchManager.Instance.ReturnToLobby();
    }

    private void Populate(MatchManager match)
    {
        bool isTeamMode = match.Mode == GameMode.TeamDeathmatch;

        ranking.Clear();

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            ranking.Add(participant);
        }

        ranking.Sort((first, second) =>
        {
            if (isTeamMode && first.TeamId != second.TeamId) return first.TeamId.CompareTo(second.TeamId);

            int killOrder = second.Kills.CompareTo(first.Kills);

            return killOrder != 0 ? killOrder : first.Deaths.CompareTo(second.Deaths);
        });

        titleText.text = GetTitle(match);
        subtitleText.text = isTeamMode ? "BLUE " + match.GetTeamKills(0) + "  -  " + match.GetTeamKills(1) + " RED" : "Free-for-All";

        for (int i = 0; i < rows.Length; i++)
        {
            bool hasPlayer = i < ranking.Count;

            rows[i].gameObject.SetActive(hasPlayer);

            if (hasPlayer)
            {
                rows[i].Show(ranking[i]);
            }
        }
    }

    private static string GetTitle(MatchManager match)
    {
        if (match.IsDraw) return "DRAW";

        return match.Mode == GameMode.TeamDeathmatch ? MatchManager.GetTeamName(match.WinnerTeam) + " TEAM WINS" : match.WinnerParticipant.DisplayName + " WINS";
    }
}
