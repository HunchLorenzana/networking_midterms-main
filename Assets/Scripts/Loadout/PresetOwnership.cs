using System.Collections.Generic;

public static class PresetOwnership
{
    // Replaces items the player does not own (or that no longer exist): weapons and abilities with the first owned one not already used, passives with an empty slot
    public static void Sanitize(LoadoutCatalog catalog)
    {
        for (int i = 0; i < PresetService.Count; i++)
        {
            int[] ids = PresetService.Get(i).CopyItemIds();
            bool isChanged = false;

            isChanged |= FixSlot(ids, LoadoutPreset.PRIMARY_SLOT, catalog.Weapons);
            isChanged |= FixSlot(ids, LoadoutPreset.SECONDARY_SLOT, catalog.Weapons);
            isChanged |= FixSlot(ids, LoadoutPreset.FIRST_ABILITY_SLOT, catalog.Abilities);
            isChanged |= FixSlot(ids, LoadoutPreset.SECOND_ABILITY_SLOT, catalog.Abilities);

            for (int slot = 0; slot < LoadoutPreset.PASSIVE_SLOT_COUNT; slot++)
            {
                int index = LoadoutPreset.FIRST_PASSIVE_SLOT + slot;

                if (ids[index] != 0 && !IsOwnedId(ids[index], catalog.Passives))
                {
                    ids[index] = 0;
                    isChanged = true;
                }
            }

            if (isChanged)
            {
                PresetService.TrySetItems(i, ids);
            }
        }
    }

    private static bool FixSlot<T>(int[] ids, int slot, IReadOnlyList<T> options) where T : ItemData
    {
        if (IsOwnedId(ids[slot], options)) return false;

        foreach (T option in options)
        {
            if (option != null && ProfileService.IsOwned(option) && System.Array.IndexOf(ids, option.Id) < 0)
            {
                ids[slot] = option.Id;

                return true;
            }
        }

        return false;
    }

    private static bool IsOwnedId<T>(int id, IReadOnlyList<T> options) where T : ItemData
    {
        foreach (T option in options)
        {
            if (option != null && option.Id == id) return ProfileService.IsOwned(option);
        }

        return false;
    }
}
