using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    [SerializeField] private string playerName;
    [SerializeField] private int scrap;
    [SerializeField] private int cores;
    [SerializeField] private int trophies;
    [SerializeField] private int bestTrophies;
    [SerializeField] private bool hasReceivedStartingScrap;
    [SerializeField] private string lastWinDate;
    [SerializeField] private List<int> ownedItemIds = new List<int>();
    [SerializeField] private List<LoadoutPreset> presets = new List<LoadoutPreset>();
    [SerializeField] private int selectedPresetIndex;
    [SerializeField] private int presetSlotCount;

    public string PlayerName => playerName;
    public int Scrap => scrap;
    public int Cores => cores;
    public int Trophies => trophies;
    public int BestTrophies => bestTrophies;
    public bool HasReceivedStartingScrap => hasReceivedStartingScrap;
    public string LastWinDate => lastWinDate;
    public int PresetCount => presets.Count;
    public int SelectedPresetIndex => selectedPresetIndex;
    public int PresetSlotCount => presetSlotCount;

    public bool Owns(int itemId)
    {
        return ownedItemIds.Contains(itemId);
    }

    public void SetPlayerName(string newName)
    {
        playerName = newName;
    }

    public void AddScrap(int amount)
    {
        scrap = Mathf.Max(0, scrap + amount);
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || scrap < amount) return false;

        scrap -= amount;

        return true;
    }

    public void AddCores(int amount)
    {
        cores = Mathf.Max(0, cores + amount);
    }

    public void AddTrophies(int amount)
    {
        trophies = Mathf.Max(0, trophies + amount);
        bestTrophies = Mathf.Max(bestTrophies, trophies);
    }

    public void MarkStartingScrapReceived()
    {
        hasReceivedStartingScrap = true;
    }

    public void SetLastWinDate(string date)
    {
        lastWinDate = date;
    }

    public void AddOwned(int itemId)
    {
        if (!ownedItemIds.Contains(itemId))
        {
            ownedItemIds.Add(itemId);
        }
    }

    public void RemoveOwned(int itemId)
    {
        ownedItemIds.Remove(itemId);
    }

    public LoadoutPreset GetPreset(int index)
    {
        return presets[index];
    }

    public void SetPreset(int index, LoadoutPreset preset)
    {
        presets[index] = preset;
    }

    public void AddPreset(LoadoutPreset preset)
    {
        presets.Add(preset);
    }

    public void SetSelectedPresetIndex(int index)
    {
        selectedPresetIndex = index;
    }

    public void SetPresetSlotCount(int count)
    {
        presetSlotCount = count;
    }
}
