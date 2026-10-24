using System;
using System.IO;
using UnityEngine;

public static class ProfileService
{
    private const string FILE_NAME = "kitbash_profile.json";
    private const string DATE_FORMAT = "yyyy-MM-dd";
    private const string TEMP_SUFFIX = ".tmp";
    private const string BACKUP_SUFFIX = ".bak";
    private const string CORRUPT_SUFFIX = ".corrupt";

    private static PlayerProfile current;
    private static bool isEverythingUnlocked;
    private static string saveFolderOverride;

    public static event Action Changed;

    public static PlayerProfile Current
    {
        get
        {
            current ??= Load();

            return current;
        }
    }

    public static bool HasWonToday => Current.LastWinDate == GetToday();

    public static bool IsOwned(ItemData item)
    {
        return isEverythingUnlocked || item.IsStarter || Current.Owns(item.Id);
    }

    // Test scenes unlock every item without touching the saved profile
    public static void SetEverythingUnlocked(bool isUnlocked)
    {
        isEverythingUnlocked = isUnlocked;
    }

    public static bool TryPurchase(ItemData item, out string failReason)
    {
        failReason = "";

        if (IsOwned(item))
        {
            failReason = "Already owned";

            return false;
        }

        if (!Current.TrySpend(item.ScrapPrice))
        {
            failReason = "Not enough Scrap";

            return false;
        }

        Current.AddOwned(item.Id);
        SaveAndNotify();

        return true;
    }

    public static void AddScrap(int amount)
    {
        Current.AddScrap(amount);
        SaveAndNotify();
    }

    public static void AddCores(int amount)
    {
        Current.AddCores(amount);
        SaveAndNotify();
    }

    public static void AddTrophies(int amount)
    {
        Current.AddTrophies(amount);
        SaveAndNotify();
    }

    // Starter items are always owned, so only non-starters change
    public static void SetOwned(ItemData item, bool isOwned)
    {
        if (isOwned)
        {
            Current.AddOwned(item.Id);
        }
        else
        {
            Current.RemoveOwned(item.Id);
        }

        SaveAndNotify();
    }

    // Lets the first-win-of-the-day bonus be earned again today
    public static void ClearLastWinDate()
    {
        Current.SetLastWinDate("");
        SaveAndNotify();
    }

    // Drops the cached profile so the next read loads the file again (after editing it by hand)
    public static void Reload()
    {
        current = null;
        Changed?.Invoke();
    }

    public static string GetSavePath()
    {
        return GetPath();
    }

    public static void SetPlayerName(string newName)
    {
        if (newName == Current.PlayerName) return;

        Current.SetPlayerName(newName);
        SaveAndNotify();
    }

    // New profiles get the starting Scrap once (stand-in for the tutorial reward)
    public static void EnsureStartingScrap(int amount)
    {
        if (Current.HasReceivedStartingScrap) return;

        Current.MarkStartingScrapReceived();
        Current.AddScrap(amount);
        SaveAndNotify();
    }

    public static void ApplyMatchReward(MatchReward reward)
    {
        Current.AddScrap(reward.Scrap);
        Current.AddTrophies(reward.Trophies);

        if (reward.IsFirstWinOfDay)
        {
            Current.SetLastWinDate(GetToday());
        }

        SaveAndNotify();
    }

    // Writes a temp file first and swaps it in, so a crash mid-save never leaves a half-written profile.
    // The previous save is kept as the backup
    public static void Save()
    {
        string path = GetPath();
        string tempPath = path + TEMP_SUFFIX;
        string backupPath = path + BACKUP_SUFFIX;

        File.WriteAllText(tempPath, JsonUtility.ToJson(Current, true));

        if (!File.Exists(path))
        {
            File.Move(tempPath, path);

            return;
        }

        try
        {
            File.Replace(tempPath, path, backupPath);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException || exception is IOException)
        {
            // Some platforms have no atomic replace. Load also checks the temp and backup files, which covers the short gap here
            File.Copy(path, backupPath, true);
            File.Delete(path);
            File.Move(tempPath, path);
        }
    }

    public static void DeleteSave()
    {
        string path = GetPath();

        foreach (string file in new[] { path, path + TEMP_SUFFIX, path + BACKUP_SUFFIX })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        current = null;
        Changed?.Invoke();
    }

    private static void SaveAndNotify()
    {
        Save();
        Changed?.Invoke();
    }

    // Reads the main file first. If it is missing or unreadable, it tries a finished temp file (crash during the swap), then the backup.
    // An unreadable main file is kept aside as .corrupt and the recovered profile is saved over it
    private static PlayerProfile Load()
    {
        string path = GetPath();

        if (TryRead(path, out PlayerProfile profile)) return profile;

        bool hasMainFile = File.Exists(path);

        foreach (string fallbackPath in new[] { path + TEMP_SUFFIX, path + BACKUP_SUFFIX })
        {
            if (!TryRead(fallbackPath, out profile)) continue;

            Debug.LogWarning("[ProfileService] The profile could not be read; recovered it from " + Path.GetFileName(fallbackPath) + ".");

            // Moved aside (not left in place) so the save below does not push it into the backup slot
            if (hasMainFile)
            {
                File.Copy(path, path + CORRUPT_SUFFIX, true);
                File.Delete(path);
            }

            current = profile;
            Save();

            return profile;
        }

        if (hasMainFile)
        {
            Debug.LogError("[ProfileService] The profile and its backup could not be read; starting a new profile. The unreadable file was kept as " + Path.GetFileName(path + CORRUPT_SUFFIX) + ".");
            File.Copy(path, path + CORRUPT_SUFFIX, true);
        }

        return new PlayerProfile();
    }

    private static bool TryRead(string filePath, out PlayerProfile profile)
    {
        profile = null;

        if (!File.Exists(filePath)) return false;

        try
        {
            profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(filePath));
        }
        catch (Exception exception) when (exception is ArgumentException || exception is IOException)
        {
            return false;
        }

        return profile != null;
    }

    private static string GetToday()
    {
        return DateTime.Now.ToString(DATE_FORMAT);
    }

    // Tests point the profile at a temporary folder so they never touch the real save. Null goes back to the real one
    internal static void UseSaveFolderForTests(string folder)
    {
        saveFolderOverride = folder;
        current = null;
    }

    private static string GetPath()
    {
        return Path.Combine(saveFolderOverride ?? Application.persistentDataPath, FILE_NAME);
    }
}
