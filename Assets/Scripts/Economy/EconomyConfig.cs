using UnityEngine;

[CreateAssetMenu(fileName = "EconomyConfig", menuName = "KITBASH/Economy Config")]
public class EconomyConfig : ScriptableObject
{
    [System.Serializable]
    public struct TrophyBracket
    {
        [Tooltip("Trophies needed to be in this bracket")]
        [SerializeField] private int minTrophies;
        [SerializeField] private int winTrophies;
        [Tooltip("Trophies lost on a loss, as a positive number")]
        [SerializeField] private int lossTrophies;

        public int MinTrophies => minTrophies;
        public int WinTrophies => winTrophies;
        public int LossTrophies => lossTrophies;

        public TrophyBracket(int minTrophies, int winTrophies, int lossTrophies)
        {
            this.minTrophies = minTrophies;
            this.winTrophies = winTrophies;
            this.lossTrophies = lossTrophies;
        }
    }

    [Header("Scrap")]
    [Tooltip("Given once to a new profile (stand-in for the tutorial reward)")]
    [SerializeField] private int startingScrap = 400;
    [SerializeField] private int participationScrap = 30;
    [SerializeField] private int winScrap = 20;
    [SerializeField] private int killScrap = 2;
    [SerializeField] private int killScrapCap = 20;
    [SerializeField] private int assistScrap = 1;
    [SerializeField] private int firstWinOfDayScrap = 50;

    [Header("Trophies")]
    [SerializeField] private TrophyBracket[] trophyBrackets =
    {
        new TrophyBracket(0, 12, 0),
        new TrophyBracket(100, 12, 3),
        new TrophyBracket(300, 11, 5),
        new TrophyBracket(700, 10, 7),
        new TrophyBracket(1200, 9, 8),
        new TrophyBracket(2000, 8, 8)
    };
    [Tooltip("Free-for-all trophies by placement (1st first). Negative values are scaled by the bracket's loss")]
    [SerializeField] private int[] placementTrophies = { 12, 9, 6, 4, 2, 0, -2, -4, -6, -8 };
    [Tooltip("The placement table's biggest loss. Negative placement values are scaled by bracket loss / this")]
    [SerializeField] private int placementLossReference = 8;
    [Tooltip("Trophy Road milestones shown on the main menu")]
    [SerializeField] private int[] trophyRoadMilestones = { 50, 100, 200, 350, 500, 750, 1000, 1500, 2000, 3000 };
    [Tooltip("Trophies needed to unlock Ranked")]
    [SerializeField] private int rankedUnlockTrophies = 300;

    public int StartingScrap => startingScrap;
    public int ParticipationScrap => participationScrap;
    public int WinScrap => winScrap;
    public int KillScrap => killScrap;
    public int KillScrapCap => killScrapCap;
    public int AssistScrap => assistScrap;
    public int FirstWinOfDayScrap => firstWinOfDayScrap;
    public int PlacementLossReference => placementLossReference;
    public int RankedUnlockTrophies => rankedUnlockTrophies;

    public TrophyBracket GetBracket(int trophies)
    {
        TrophyBracket found = trophyBrackets[0];

        foreach (TrophyBracket bracket in trophyBrackets)
        {
            if (trophies >= bracket.MinTrophies)
            {
                found = bracket;
            }
        }

        return found;
    }

    public int GetPlacementTrophies(int placement)
    {
        return placementTrophies[Mathf.Clamp(placement - 1, 0, placementTrophies.Length - 1)];
    }

    // Returns the first milestone above the given trophies, or the last one when all are passed
    public int GetNextMilestone(int trophies, out int previousMilestone)
    {
        previousMilestone = 0;

        foreach (int milestone in trophyRoadMilestones)
        {
            if (trophies < milestone) return milestone;

            previousMilestone = milestone;
        }

        return previousMilestone;
    }
}
