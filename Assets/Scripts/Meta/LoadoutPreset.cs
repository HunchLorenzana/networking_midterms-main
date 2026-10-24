using System;
using UnityEngine;

[Serializable]
public class LoadoutPreset
{
    public const int SLOT_COUNT = 7;
    public const int PRIMARY_SLOT = 0;
    public const int SECONDARY_SLOT = 1;
    public const int FIRST_ABILITY_SLOT = 2;
    public const int SECOND_ABILITY_SLOT = 3;
    public const int FIRST_PASSIVE_SLOT = 4;
    public const int PASSIVE_SLOT_COUNT = 3;

    [SerializeField] private string name;
    [SerializeField] private int[] itemIds = new int[SLOT_COUNT];

    public string Name => name;
    public bool IsValid => itemIds != null && itemIds.Length == SLOT_COUNT;

    public LoadoutPreset()
    {
    }

    public LoadoutPreset(string name, int[] itemIds)
    {
        this.name = name;
        this.itemIds = (int[])itemIds.Clone();
    }

    public int GetItemId(int slot)
    {
        return itemIds[slot];
    }

    public int[] CopyItemIds()
    {
        return (int[])itemIds.Clone();
    }

    public LoadoutPreset Clone()
    {
        return new LoadoutPreset(name, itemIds);
    }

    public void SetName(string newName)
    {
        name = newName;
    }

    public void SetItemIds(int[] newItemIds)
    {
        itemIds = (int[])newItemIds.Clone();
    }

    public static bool HasDuplicates(int[] ids)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == 0) continue;

            for (int j = i + 1; j < ids.Length; j++)
            {
                if (ids[i] == ids[j]) return true;
            }
        }

        return false;
    }
}
