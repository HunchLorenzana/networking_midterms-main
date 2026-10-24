using UnityEngine;

public static class MatchRewardCalculator
{
    public static MatchReward Calculate(EconomyConfig config, MatchResultInfo result, int currentTrophies, bool hasWonToday)
    {
        bool isFirstWinOfDay = result.IsWin && !result.IsDraw && !hasWonToday;
        int scrap = config.ParticipationScrap
            + Mathf.Min(result.Kills, config.KillScrapCap) * config.KillScrap
            + result.Assists * config.AssistScrap;

        if (result.IsWin && !result.IsDraw)
        {
            scrap += config.WinScrap;
        }

        if (isFirstWinOfDay)
        {
            scrap += config.FirstWinOfDayScrap;
        }

        int trophies = result.IsDraw ? 0 : CalculateTrophies(config, result, currentTrophies);

        // Trophies never go below zero
        trophies = Mathf.Max(trophies, -currentTrophies);

        return new MatchReward(scrap, trophies, isFirstWinOfDay);
    }

    private static int CalculateTrophies(EconomyConfig config, MatchResultInfo result, int currentTrophies)
    {
        EconomyConfig.TrophyBracket bracket = config.GetBracket(currentTrophies);

        if (result.Placement <= 0)
        {
            return result.IsWin ? bracket.WinTrophies : -bracket.LossTrophies;
        }

        int placementTrophies = config.GetPlacementTrophies(result.Placement);

        if (placementTrophies >= 0) return placementTrophies;

        return Mathf.RoundToInt(placementTrophies * bracket.LossTrophies / (float)Mathf.Max(1, config.PlacementLossReference));
    }
}
