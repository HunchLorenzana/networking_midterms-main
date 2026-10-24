public static class PresetStats
{
    public static StatTotals Calculate(LoadoutCatalog catalog, int[] itemIds)
    {
        PassiveData[] passives = new PassiveData[LoadoutPreset.PASSIVE_SLOT_COUNT];

        for (int slot = 0; slot < passives.Length; slot++)
        {
            passives[slot] = catalog.GetPassive(itemIds[LoadoutPreset.FIRST_PASSIVE_SLOT + slot]);
        }

        return StatTotals.Calculate(catalog.GetWeapon(itemIds[LoadoutPreset.PRIMARY_SLOT]), catalog.GetWeapon(itemIds[LoadoutPreset.SECONDARY_SLOT]), passives);
    }
}
