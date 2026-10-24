using System;
using UnityEngine;

public static class PresetService
{
    public const int DEFAULT_SLOT_COUNT = 3;
    public const int MAX_NAME_LENGTH = 16;

    private const int RIFLE_ID = 1001;
    private const int SHOTGUN_ID = 1002;
    private const int SMG_ID = 1005;
    private const int DASH_ID = 2001;
    private const int SHIELD_ID = 2002;
    private const int FOCUS_ID = 2005;
    private const int HEAVY_PLATING_ID = 3004;
    private const int LIGHT_FRAME_ID = 3005;
    private const int OVERCLOCK_ID = 3006;

    private static readonly LoadoutPreset[] defaultPresets =
    {
        new LoadoutPreset("Assault", new[] { RIFLE_ID, SMG_ID, DASH_ID, SHIELD_ID, HEAVY_PLATING_ID, LIGHT_FRAME_ID, OVERCLOCK_ID }),
        new LoadoutPreset("Breacher", new[] { SHOTGUN_ID, SMG_ID, DASH_ID, FOCUS_ID, HEAVY_PLATING_ID, LIGHT_FRAME_ID, OVERCLOCK_ID }),
        new LoadoutPreset("Marksman", new[] { RIFLE_ID, SHOTGUN_ID, SHIELD_ID, FOCUS_ID, HEAVY_PLATING_ID, LIGHT_FRAME_ID, OVERCLOCK_ID })
    };

    private static PlayerProfile checkedProfile;

    public static event Action Changed;

    public static int Count => Profile.PresetCount;
    public static int SelectedIndex => Profile.SelectedPresetIndex;
    public static LoadoutPreset Selected => Profile.GetPreset(Profile.SelectedPresetIndex);

    private static PlayerProfile Profile
    {
        get
        {
            // Checked again when the profile object changes (for example after a reset)
            if (checkedProfile != ProfileService.Current)
            {
                checkedProfile = ProfileService.Current;
                EnsurePresets(checkedProfile);
            }

            return ProfileService.Current;
        }
    }

    public static LoadoutPreset Get(int index)
    {
        return Profile.GetPreset(index);
    }

    public static void Select(int index)
    {
        if (index < 0 || index >= Count || index == SelectedIndex) return;

        Profile.SetSelectedPresetIndex(index);
        SaveAndNotify();
    }

    public static bool TrySetItems(int index, int[] itemIds)
    {
        if (index < 0 || index >= Count || itemIds.Length != LoadoutPreset.SLOT_COUNT || LoadoutPreset.HasDuplicates(itemIds)) return false;

        Profile.GetPreset(index).SetItemIds(itemIds);
        SaveAndNotify();

        return true;
    }

    public static bool TryRename(int index, string newName)
    {
        string cleanedName = (newName ?? "").Trim();

        if (index < 0 || index >= Count || cleanedName.Length == 0) return false;

        if (cleanedName.Length > MAX_NAME_LENGTH)
        {
            cleanedName = cleanedName.Substring(0, MAX_NAME_LENGTH);
        }

        Profile.GetPreset(index).SetName(cleanedName);
        SaveAndNotify();

        return true;
    }

    // Copies the items only, the target keeps its own name
    public static bool TryCopy(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= Count || targetIndex < 0 || targetIndex >= Count || sourceIndex == targetIndex) return false;

        Profile.GetPreset(targetIndex).SetItemIds(Profile.GetPreset(sourceIndex).CopyItemIds());
        SaveAndNotify();

        return true;
    }

    public static void ResetToDefaults()
    {
        for (int i = 0; i < Count; i++)
        {
            Profile.SetPreset(i, GetDefaultPreset(i));
        }

        Profile.SetSelectedPresetIndex(0);
        SaveAndNotify();
    }

    private static void SaveAndNotify()
    {
        ProfileService.Save();
        Changed?.Invoke();
    }

    // Fills in presets for new profiles and old saves made before presets existed
    private static void EnsurePresets(PlayerProfile profile)
    {
        bool isDirty = false;

        if (profile.PresetSlotCount < DEFAULT_SLOT_COUNT)
        {
            profile.SetPresetSlotCount(DEFAULT_SLOT_COUNT);
            isDirty = true;
        }

        for (int i = 0; i < profile.PresetCount; i++)
        {
            if (!profile.GetPreset(i).IsValid)
            {
                profile.SetPreset(i, GetDefaultPreset(i));
                isDirty = true;
            }
        }

        while (profile.PresetCount < profile.PresetSlotCount)
        {
            profile.AddPreset(GetDefaultPreset(profile.PresetCount));
            isDirty = true;
        }

        if (profile.SelectedPresetIndex < 0 || profile.SelectedPresetIndex >= profile.PresetCount)
        {
            profile.SetSelectedPresetIndex(0);
            isDirty = true;
        }

        if (isDirty)
        {
            ProfileService.Save();
        }
    }

    private static LoadoutPreset GetDefaultPreset(int index)
    {
        LoadoutPreset preset = defaultPresets[Mathf.Min(index, defaultPresets.Length - 1)].Clone();

        if (index >= defaultPresets.Length)
        {
            preset.SetName("Preset " + (index + 1));
        }

        return preset;
    }
}
