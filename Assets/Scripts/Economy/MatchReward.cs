public readonly struct MatchReward
{
    public int Scrap { get; }
    public int Trophies { get; }
    public bool IsFirstWinOfDay { get; }

    public MatchReward(int scrap, int trophies, bool isFirstWinOfDay)
    {
        Scrap = scrap;
        Trophies = trophies;
        IsFirstWinOfDay = isFirstWinOfDay;
    }
}
