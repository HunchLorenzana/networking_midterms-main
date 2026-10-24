using NUnit.Framework;

public class StatTotalsTests
{
    private const float TOLERANCE = 0.0001f;

    private TestData data;

    [SetUp]
    public void SetUp()
    {
        data = new TestData();
    }

    [TearDown]
    public void TearDown()
    {
        data.DestroyAll();
    }

    [Test]
    public void NoModifiers_AreAllOne()
    {
        StatTotals stats = StatTotals.Calculate(data.Weapon(1, true), data.Weapon(2, true), null);

        Assert.AreEqual(1f, stats.MaxHealth, TOLERANCE);
        Assert.AreEqual(1f, stats.MoveSpeed, TOLERANCE);
        Assert.AreEqual(1f, stats.DamageDealt, TOLERANCE);
    }

    [Test]
    public void WeaponAndPassiveModifiers_AreAddedTogether()
    {
        WeaponData heavy = data.Weapon(1, true, 1.1f, 0.95f);
        PassiveData plating = data.Passive(3, true, 0.2f, -0.08f);
        StatTotals stats = StatTotals.Calculate(heavy, data.Weapon(2, true), new[] { plating });

        Assert.AreEqual(1.3f, stats.MaxHealth, TOLERANCE);
        Assert.AreEqual(1f - 0.05f - 0.08f, stats.MoveSpeed, TOLERANCE);
    }

    [Test]
    public void BothWeapons_Count()
    {
        StatTotals stats = StatTotals.Calculate(data.Weapon(1, true, 1f, 0.9f), data.Weapon(2, true, 1f, 0.9f), null);

        Assert.AreEqual(0.8f, stats.MoveSpeed, TOLERANCE);
    }

    [Test]
    public void Totals_AreClamped()
    {
        PassiveData tank = data.Passive(3, true, 0.5f, -0.2f);
        PassiveData moreTank = data.Passive(4, true, 0.5f, -0.2f);
        StatTotals stats = StatTotals.Calculate(data.Weapon(1, true, 1.1f), data.Weapon(2, true), new[] { tank, moreTank, null });

        Assert.AreEqual(1.6f, stats.MaxHealth, TOLERANCE);
        Assert.AreEqual(0.7f, stats.MoveSpeed, TOLERANCE);
    }
}
