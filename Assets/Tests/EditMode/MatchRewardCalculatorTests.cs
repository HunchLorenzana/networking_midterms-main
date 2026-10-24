using NUnit.Framework;

// Uses EconomyConfig's default numbers (design sections 6 and 7)
public class MatchRewardCalculatorTests
{
    private TestData data;
    private EconomyConfig config;

    [SetUp]
    public void SetUp()
    {
        data = new TestData();
        config = data.Create<EconomyConfig>();
    }

    [TearDown]
    public void TearDown()
    {
        data.DestroyAll();
    }

    [Test]
    public void TeamWin_GivesParticipationWinKillsAssistsAndFirstWin()
    {
        MatchReward reward = MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, true, 0, 5, 3), 0, false);

        Assert.AreEqual(30 + 20 + 5 * 2 + 3 * 1 + 50, reward.Scrap);
        Assert.AreEqual(12, reward.Trophies);
        Assert.IsTrue(reward.IsFirstWinOfDay);
    }

    [Test]
    public void SecondWinOfTheDay_HasNoFirstWinBonus()
    {
        MatchReward reward = MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, true, 0, 0, 0), 0, true);

        Assert.AreEqual(30 + 20, reward.Scrap);
        Assert.IsFalse(reward.IsFirstWinOfDay);
    }

    [Test]
    public void KillScrap_IsCapped()
    {
        MatchReward reward = MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 0, 30, 0), 0, false);

        Assert.AreEqual(30 + 20 * 2, reward.Scrap);
    }

    [Test]
    public void Draw_GivesParticipationOnly_AndNoTrophies()
    {
        MatchReward reward = MatchRewardCalculator.Calculate(config, new MatchResultInfo(true, false, 0, 0, 0), 500, false);

        Assert.AreEqual(30, reward.Scrap);
        Assert.AreEqual(0, reward.Trophies);
        Assert.IsFalse(reward.IsFirstWinOfDay);
    }

    [Test]
    public void TeamLoss_UsesTheBracketLoss()
    {
        Assert.AreEqual(0, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 0, 0, 0), 50, false).Trophies);
        Assert.AreEqual(-3, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 0, 0, 0), 150, false).Trophies);
        Assert.AreEqual(-8, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 0, 0, 0), 2500, false).Trophies);
    }

    [Test]
    public void FreeForAll_UsesThePlacementTable()
    {
        Assert.AreEqual(12, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, true, 1, 0, 0), 0, false).Trophies);
        Assert.AreEqual(6, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 3, 0, 0), 0, false).Trophies);
    }

    [Test]
    public void FreeForAllLoss_IsScaledByTheBracketLoss()
    {
        // 10th place is -8 in the table. At 150 trophies the bracket loss is 3, so -8 * 3 / 8 = -3
        Assert.AreEqual(-3, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 10, 0, 0), 150, false).Trophies);
        Assert.AreEqual(0, MatchRewardCalculator.Calculate(config, new MatchResultInfo(false, false, 10, 0, 0), 50, false).Trophies);
    }
}
