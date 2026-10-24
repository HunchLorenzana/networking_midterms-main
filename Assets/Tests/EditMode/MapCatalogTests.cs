using NUnit.Framework;

public class MapCatalogTests
{
    private const int PICKS = 100;

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
    public void PickRandom_OnlyPicksMapsInRotationThatSupportTheMode()
    {
        MapDefinition both = data.Map(4001, true, GameMode.FreeForAll, GameMode.TeamDeathmatch);
        MapDefinition teamOnly = data.Map(4002, true, GameMode.TeamDeathmatch);
        MapDefinition outOfRotation = data.Map(4003, false, GameMode.FreeForAll);
        MapCatalog catalog = data.MapCatalog(both, teamOnly, outOfRotation);

        for (int i = 0; i < PICKS; i++)
        {
            Assert.AreSame(both, catalog.PickRandom(GameMode.FreeForAll));
        }
    }

    [Test]
    public void PickRandom_CanPickEveryValidMap()
    {
        MapDefinition first = data.Map(4001, true, GameMode.TeamDeathmatch);
        MapDefinition second = data.Map(4002, true, GameMode.TeamDeathmatch);
        MapCatalog catalog = data.MapCatalog(first, second);
        bool hasFirst = false;
        bool hasSecond = false;

        for (int i = 0; i < PICKS; i++)
        {
            MapDefinition picked = catalog.PickRandom(GameMode.TeamDeathmatch);

            hasFirst |= picked == first;
            hasSecond |= picked == second;
        }

        Assert.IsTrue(hasFirst && hasSecond);
    }

    [Test]
    public void PickRandom_ReturnsNullWhenNoMapFits()
    {
        MapCatalog catalog = data.MapCatalog(data.Map(4001, false, GameMode.FreeForAll), data.Map(4002, true, GameMode.TeamDeathmatch));

        Assert.IsNull(catalog.PickRandom(GameMode.FreeForAll));
    }
}
