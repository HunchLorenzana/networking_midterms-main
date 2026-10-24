public readonly struct MatchResultInfo
{
    public bool IsDraw { get; }
    public bool IsWin { get; }
    // 1-based place in free-for-all, 0 in team modes
    public int Placement { get; }
    public int Kills { get; }
    public int Assists { get; }

    public MatchResultInfo(bool isDraw, bool isWin, int placement, int kills, int assists)
    {
        IsDraw = isDraw;
        IsWin = isWin;
        Placement = placement;
        Kills = kills;
        Assists = assists;
    }
}
