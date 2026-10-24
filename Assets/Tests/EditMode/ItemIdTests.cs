using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Scans every item and map asset in the project. Saves, purchases and presets store these IDs, so they must be set,
// unique and in their type's range
public class ItemIdTests
{
    [Test]
    public void EveryItemAndMap_HasAnId()
    {
        List<string> problems = new List<string>();

        foreach (IdEntry entry in FindAll())
        {
            if (entry.Id == 0)
            {
                problems.Add(entry.Path);
            }
        }

        Assert.IsEmpty(problems, "Assets without a permanent ID:\n" + string.Join("\n", problems));
    }

    [Test]
    public void Ids_AreUnique()
    {
        Dictionary<int, string> seen = new Dictionary<int, string>();
        List<string> problems = new List<string>();

        foreach (IdEntry entry in FindAll())
        {
            if (entry.Id == 0) continue;

            if (seen.TryGetValue(entry.Id, out string other))
            {
                problems.Add(entry.Id + ": " + entry.Path + " and " + other);
            }
            else
            {
                seen[entry.Id] = entry.Path;
            }
        }

        Assert.IsEmpty(problems, "Duplicate IDs:\n" + string.Join("\n", problems));
    }

    [Test]
    public void Ids_AreInTheirTypesRange()
    {
        List<string> problems = new List<string>();

        foreach (IdEntry entry in FindAll())
        {
            if (entry.Id != 0 && (entry.Id < entry.RangeStart || entry.Id > entry.RangeStart + 999))
            {
                problems.Add(entry.Id + " (" + entry.Path + ") should be " + entry.RangeStart + "-" + (entry.RangeStart + 999));
            }
        }

        Assert.IsEmpty(problems, "IDs outside their range (weapons 1xxx, abilities 2xxx, passives 3xxx, maps 4xxx):\n" + string.Join("\n", problems));
    }

    private static List<IdEntry> FindAll()
    {
        List<IdEntry> entries = new List<IdEntry>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            entries.Add(new IdEntry(item.Id, path, GetRangeStart(item)));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:MapDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            entries.Add(new IdEntry(AssetDatabase.LoadAssetAtPath<MapDefinition>(path).Id, path, 4000));
        }

        return entries;
    }

    private static int GetRangeStart(ItemData item)
    {
        if (item is WeaponData) return 1000;

        if (item is AbilityData) return 2000;

        return 3000;
    }

    private readonly struct IdEntry
    {
        public int Id { get; }
        public string Path { get; }
        public int RangeStart { get; }

        public IdEntry(int id, string path, int rangeStart)
        {
            Id = id;
            Path = path;
            RangeStart = rangeStart;
        }
    }
}
