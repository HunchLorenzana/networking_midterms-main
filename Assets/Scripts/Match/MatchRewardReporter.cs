using System;
using UnityEngine;

public class MatchRewardReporter : MonoBehaviour
{
    [SerializeField] private EconomyConfig config;

    private MatchPhase lastPhase;

    public static event Action<MatchReward> RewardGranted;

    public static bool HasReward { get; private set; }
    public static MatchReward LastReward { get; private set; }

    void Start()
    {
        HasReward = false;
        lastPhase = MatchManager.Instance.Phase;
        ProfileService.EnsureStartingScrap(config.StartingScrap);
    }

    void Update()
    {
        MatchPhase phase = MatchManager.Instance.Phase;

        if (phase == lastPhase) return;

        if (lastPhase == MatchPhase.Playing && phase == MatchPhase.MatchOver && PlayerController.Local != null)
        {
            Grant(BuildResult(MatchManager.Instance, PlayerController.Local.Participant));
        }
        else if (phase == MatchPhase.Lobby)
        {
            HasReward = false;
        }

        lastPhase = phase;
    }

    private void Grant(MatchResultInfo result)
    {
        MatchReward reward = MatchRewardCalculator.Calculate(config, result, ProfileService.Current.Trophies, ProfileService.HasWonToday);

        ProfileService.ApplyMatchReward(reward);
        LastReward = reward;
        HasReward = true;
        RewardGranted?.Invoke(reward);
    }

    private MatchResultInfo BuildResult(MatchManager match, MatchParticipant local)
    {
        int placement = 1;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (participant != local && participant.Kills > local.Kills)
            {
                placement++;
            }
        }

        bool isTeamMode = match.Mode == GameMode.TeamDeathmatch;
        bool isWin = !match.IsDraw && (isTeamMode ? match.WinnerTeam == local.TeamId : match.WinnerParticipant == local);

        return new MatchResultInfo(match.IsDraw, isWin, isTeamMode ? 0 : placement, local.Kills, local.Assists);
    }
}
