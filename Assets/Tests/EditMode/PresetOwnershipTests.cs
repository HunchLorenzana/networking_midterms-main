using System.IO;
using NUnit.Framework;

// The catalog holds the IDs used by PresetService's default presets, so only the preset a test changes is affected
public class PresetOwnershipTests
{
    private const int RIFLE = 1001;
    private const int SHOTGUN = 1002;
    private const int SNIPER = 1003;
    private const int SMG = 1005;
    private const int DASH = 2001;
    private const int SHIELD = 2002;
    private const int MEND = 2008;
    private const int FOCUS = 2005;
    private const int LIFESTEAL = 3001;
    private const int HEAVY_PLATING = 3004;
    private const int LIGHT_FRAME = 3005;
    private const int OVERCLOCK = 3006;

    private TestData data;
    private LoadoutCatalog catalog;
    private string folder;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "KitbashPresetTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        ProfileService.UseSaveFolderForTests(folder);
        ProfileService.SetEverythingUnlocked(false);
        data = new TestData();
        catalog = data.Catalog(
            new[] { data.Weapon(RIFLE, true), data.Weapon(SHOTGUN, true), data.Weapon(SNIPER, false), data.Weapon(SMG, true) },
            new[] { data.Ability(DASH, true), data.Ability(SHIELD, true), data.Ability(FOCUS, true), data.Ability(MEND, false) },
            new[] { data.Passive(LIFESTEAL, false), data.Passive(HEAVY_PLATING, true), data.Passive(LIGHT_FRAME, true), data.Passive(OVERCLOCK, true) });
    }

    [TearDown]
    public void TearDown()
    {
        data.DestroyAll();
        ProfileService.UseSaveFolderForTests(null);
        Directory.Delete(folder, true);
    }

    [Test]
    public void UnownedWeapon_IsReplacedByTheFirstOwnedUnusedOne()
    {
        PresetService.TrySetItems(0, new[] { SNIPER, RIFLE, DASH, SHIELD, HEAVY_PLATING, 0, 0 });

        PresetOwnership.Sanitize(catalog);

        Assert.AreEqual(SHOTGUN, PresetService.Get(0).GetItemId(LoadoutPreset.PRIMARY_SLOT));
        Assert.AreEqual(RIFLE, PresetService.Get(0).GetItemId(LoadoutPreset.SECONDARY_SLOT));
    }

    [Test]
    public void UnownedAbility_IsReplaced()
    {
        PresetService.TrySetItems(0, new[] { RIFLE, SMG, MEND, SHIELD, 0, 0, 0 });

        PresetOwnership.Sanitize(catalog);

        Assert.AreEqual(DASH, PresetService.Get(0).GetItemId(LoadoutPreset.FIRST_ABILITY_SLOT));
    }

    [Test]
    public void UnownedPassive_IsEmptied()
    {
        PresetService.TrySetItems(0, new[] { RIFLE, SMG, DASH, SHIELD, LIFESTEAL, HEAVY_PLATING, 0 });

        PresetOwnership.Sanitize(catalog);

        Assert.AreEqual(0, PresetService.Get(0).GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT));
        Assert.AreEqual(HEAVY_PLATING, PresetService.Get(0).GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT + 1));
    }

    [Test]
    public void BoughtItems_AreKept()
    {
        ProfileService.SetOwned(catalog.GetWeapon(SNIPER), true);
        PresetService.TrySetItems(0, new[] { SNIPER, RIFLE, DASH, SHIELD, 0, 0, 0 });

        PresetOwnership.Sanitize(catalog);

        Assert.AreEqual(SNIPER, PresetService.Get(0).GetItemId(LoadoutPreset.PRIMARY_SLOT));
    }

    [Test]
    public void EverythingUnlocked_KeepsAllItems()
    {
        ProfileService.SetEverythingUnlocked(true);
        PresetService.TrySetItems(0, new[] { SNIPER, RIFLE, MEND, SHIELD, LIFESTEAL, 0, 0 });

        PresetOwnership.Sanitize(catalog);
        ProfileService.SetEverythingUnlocked(false);

        Assert.AreEqual(SNIPER, PresetService.Get(0).GetItemId(LoadoutPreset.PRIMARY_SLOT));
        Assert.AreEqual(MEND, PresetService.Get(0).GetItemId(LoadoutPreset.FIRST_ABILITY_SLOT));
        Assert.AreEqual(LIFESTEAL, PresetService.Get(0).GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT));
    }
}
