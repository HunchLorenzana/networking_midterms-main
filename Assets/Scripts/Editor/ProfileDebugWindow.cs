using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Inspects and edits the local profile, in and out of Play mode. The running game listens to the profile's change events
public class ProfileDebugWindow : EditorWindow
{
    private const string SECTION_PREFIX = "KitbashProfileDebug.";
    private const int RESULT_WIN = 0;
    private const int RESULT_LOSS = 1;
    private const int RESULT_DRAW = 2;

    private static readonly string[] itemTabs = { "Weapons", "Abilities", "Passives" };
    private static readonly string[] resultNames = { "Win", "Loss", "Draw" };
    private static readonly int[] currencySteps = { -1000, -100, 100, 1000, 10000 };
    private static readonly int[] trophySteps = { -100, -10, 10, 100 };

    private readonly Dictionary<string, bool> sectionStates = new Dictionary<string, bool>();

    private LoadoutCatalog catalog;
    private EconomyConfig config;
    private Vector2 scroll;
    private string nameDraft = "";
    private int scrapTarget;
    private int coresTarget;
    private int trophiesTarget;
    private int itemTab;
    private int simulatedResult;
    private bool isSimulatedTeamMode;
    private int simulatedPlacement = 1;
    private int simulatedKills = 5;
    private int simulatedAssists = 2;

    [MenuItem("KITBASH/Profile Debugger")]
    public static void Open()
    {
        GetWindow<ProfileDebugWindow>("Profile Debugger");
    }

    void OnEnable()
    {
        LoadAssets();
        ProfileService.Changed += HandleProfileChanged;
        PresetService.Changed += Repaint;
        SyncDrafts();
    }

    void OnDisable()
    {
        ProfileService.Changed -= HandleProfileChanged;
        PresetService.Changed -= Repaint;
    }

    void OnFocus()
    {
        LoadAssets();
    }

    private void HandleProfileChanged()
    {
        SyncDrafts();
        Repaint();
    }

    private void SyncDrafts()
    {
        PlayerProfile profile = ProfileService.Current;

        nameDraft = profile.PlayerName ?? "";
        scrapTarget = profile.Scrap;
        coresTarget = profile.Cores;
        trophiesTarget = profile.Trophies;
    }

    private void LoadAssets()
    {
        string[] catalogs = AssetDatabase.FindAssets("t:LoadoutCatalog");

        catalog = catalogs.Length > 0 ? AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(AssetDatabase.GUIDToAssetPath(catalogs[0])) : null;
        string[] configs = AssetDatabase.FindAssets("t:EconomyConfig");

        config = configs.Length > 0 ? AssetDatabase.LoadAssetAtPath<EconomyConfig>(AssetDatabase.GUIDToAssetPath(configs[0])) : null;
    }

    void OnGUI()
    {
        PlayerProfile profile = ProfileService.Current;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawFileSection();

        if (BeginSection("Player", true))
        {
            DrawPlayer(profile);
        }

        if (BeginSection("Currencies", true))
        {
            DrawCurrency("Scrap", profile.Scrap, ref scrapTarget, ProfileService.AddScrap);
            DrawCurrency("Cores", profile.Cores, ref coresTarget, ProfileService.AddCores);
        }

        if (BeginSection("Trophies", true))
        {
            DrawTrophies(profile);
        }

        if (BeginSection("Owned Items", true))
        {
            DrawItems();
        }

        if (BeginSection("Presets", true))
        {
            DrawPresets();
        }

        if (BeginSection("Match Reward Simulator", false))
        {
            DrawSimulator(profile);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFileSection()
    {
        string path = ProfileService.GetSavePath();

        EditorGUILayout.LabelField("Save file", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reveal File"))
            {
                EditorUtility.RevealInFinder(System.IO.File.Exists(path) ? path : System.IO.Path.GetDirectoryName(path));
            }

            if (GUILayout.Button(new GUIContent("Reload From Disk", "Use after editing the JSON by hand")))
            {
                ProfileService.Reload();
            }

            if (GUILayout.Button("Reset Profile") && EditorUtility.DisplayDialog("Reset Profile", "Delete the local profile (Scrap, Cores, trophies, owned items, presets, name)?", "Delete", "Cancel"))
            {
                ProfileService.DeleteSave();
            }
        }

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play mode: changes apply to the running game right away.", MessageType.None);
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawPlayer(PlayerProfile profile)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            nameDraft = EditorGUILayout.TextField("Name", nameDraft);

            if (GUILayout.Button("Apply", GUILayout.Width(60f)))
            {
                string cleaned = PlayerNames.Clean(nameDraft);

                if (cleaned.Length > 0)
                {
                    ProfileService.SetPlayerName(cleaned);
                }

                nameDraft = ProfileService.Current.PlayerName;
            }
        }

        EditorGUILayout.LabelField("Starting Scrap received", profile.HasReceivedStartingScrap ? "Yes" : "No");
        EditorGUILayout.Space(2f);
    }

    private static void DrawCurrency(string label, int current, ref int target, System.Action<int> add)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, current.ToString(), EditorStyles.boldLabel);
            target = EditorGUILayout.IntField(target, GUILayout.Width(80f));

            if (GUILayout.Button("Set", GUILayout.Width(40f)))
            {
                add(Mathf.Max(0, target) - current);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int step in currencySteps)
            {
                if (GUILayout.Button((step > 0 ? "+" : "") + step))
                {
                    add(step);
                }
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawTrophies(PlayerProfile profile)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Trophies", profile.Trophies + "   (best " + profile.BestTrophies + ")", EditorStyles.boldLabel);
            trophiesTarget = EditorGUILayout.IntField(trophiesTarget, GUILayout.Width(80f));

            if (GUILayout.Button("Set", GUILayout.Width(40f)))
            {
                ProfileService.AddTrophies(Mathf.Max(0, trophiesTarget) - profile.Trophies);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int step in trophySteps)
            {
                if (GUILayout.Button((step > 0 ? "+" : "") + step))
                {
                    ProfileService.AddTrophies(step);
                }
            }
        }

        if (config != null)
        {
            EconomyConfig.TrophyBracket bracket = config.GetBracket(profile.Trophies);
            int nextMilestone = config.GetNextMilestone(profile.Trophies, out _);

            EditorGUILayout.LabelField("Bracket", "from " + bracket.MinTrophies + ": win +" + bracket.WinTrophies + ", loss -" + bracket.LossTrophies);
            EditorGUILayout.LabelField("Next Trophy Road milestone", profile.Trophies >= nextMilestone ? "All reached" : nextMilestone.ToString());
            EditorGUILayout.LabelField("Ranked", profile.Trophies >= config.RankedUnlockTrophies ? "Unlocked" : "Unlocks at " + config.RankedUnlockTrophies);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("First win of the day", ProfileService.HasWonToday ? "Already earned today" : "Available");

            using (new EditorGUI.DisabledScope(!ProfileService.HasWonToday))
            {
                if (GUILayout.Button("Allow Again", GUILayout.Width(90f)))
                {
                    ProfileService.ClearLastWinDate();
                }
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawItems()
    {
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("No LoadoutCatalog asset found.", MessageType.Warning);

            return;
        }

        itemTab = GUILayout.Toolbar(itemTab, itemTabs);

        IReadOnlyList<ItemData> items = GetTabItems();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Unlock All"))
            {
                SetAllOwned(items, true);
            }

            if (GUILayout.Button("Lock All (not starters)"))
            {
                SetAllOwned(items, false);
            }
        }

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool isOwned = item.IsStarter || ProfileService.Current.Owns(item.Id);

                EditorGUILayout.LabelField(item.DisplayName + "  (" + item.Id + ")", GUILayout.MinWidth(160f));
                EditorGUILayout.LabelField(item.IsStarter ? "Starter" : item.ScrapPrice + " Scrap", GUILayout.Width(80f));

                using (new EditorGUI.DisabledScope(item.IsStarter))
                {
                    bool newOwned = EditorGUILayout.ToggleLeft("Owned", isOwned, GUILayout.Width(70f));

                    if (newOwned != isOwned)
                    {
                        ProfileService.SetOwned(item, newOwned);
                    }
                }
            }
        }

        EditorGUILayout.Space(2f);
    }

    private IReadOnlyList<ItemData> GetTabItems()
    {
        if (itemTab == 0) return catalog.Weapons;

        if (itemTab == 1) return catalog.Abilities;

        return catalog.Passives;
    }

    private static void SetAllOwned(IReadOnlyList<ItemData> items, bool isOwned)
    {
        foreach (ItemData item in items)
        {
            if (item != null && !item.IsStarter)
            {
                ProfileService.SetOwned(item, isOwned);
            }
        }
    }

    private void DrawPresets()
    {
        for (int i = 0; i < PresetService.Count; i++)
        {
            LoadoutPreset preset = PresetService.Get(i);
            bool isSelected = i == PresetService.SelectedIndex;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField((isSelected ? "> " : "   ") + (i + 1) + ". " + preset.Name, isSelected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(150f));
                EditorGUILayout.LabelField(DescribePreset(preset), EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(isSelected))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        PresetService.Select(i);
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Presets To Defaults"))
            {
                PresetService.ResetToDefaults();
            }

            using (new EditorGUI.DisabledScope(catalog == null))
            {
                if (GUILayout.Button(new GUIContent("Remove Unowned Items", "What the main menu does on load: unowned weapons and abilities are swapped for owned ones, unowned passives emptied")))
                {
                    PresetOwnership.Sanitize(catalog);
                }
            }
        }

        EditorGUILayout.Space(2f);
    }

    private string DescribePreset(LoadoutPreset preset)
    {
        if (catalog == null) return "";

        List<string> names = new List<string>();

        for (int slot = 0; slot < LoadoutPreset.SLOT_COUNT; slot++)
        {
            int id = preset.GetItemId(slot);
            ItemData item = (ItemData)catalog.GetWeapon(id) ?? (ItemData)catalog.GetAbility(id) ?? catalog.GetPassive(id);

            names.Add(item != null ? item.DisplayName : (id == 0 ? "-" : "?" + id));
        }

        return names[0] + " + " + names[1] + "  |  " + names[2] + " + " + names[3] + "  |  " + names[4] + ", " + names[5] + ", " + names[6];
    }

    private void DrawSimulator(PlayerProfile profile)
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("No EconomyConfig asset found.", MessageType.Warning);

            return;
        }

        simulatedResult = GUILayout.Toolbar(simulatedResult, resultNames);
        isSimulatedTeamMode = EditorGUILayout.Toggle("Team mode", isSimulatedTeamMode);

        using (new EditorGUI.DisabledScope(isSimulatedTeamMode))
        {
            simulatedPlacement = EditorGUILayout.IntSlider("Placement (free-for-all)", simulatedPlacement, 1, 10);
        }

        simulatedKills = EditorGUILayout.IntSlider("Kills", simulatedKills, 0, 40);
        simulatedAssists = EditorGUILayout.IntSlider("Assists", simulatedAssists, 0, 40);

        bool isDraw = simulatedResult == RESULT_DRAW;
        bool isWin = simulatedResult == RESULT_WIN;
        int placement = isSimulatedTeamMode ? 0 : (isWin ? 1 : Mathf.Max(2, simulatedPlacement));
        MatchResultInfo result = new MatchResultInfo(isDraw, isWin, placement, simulatedKills, simulatedAssists);
        MatchReward reward = MatchRewardCalculator.Calculate(config, result, profile.Trophies, ProfileService.HasWonToday);

        EditorGUILayout.HelpBox("+" + reward.Scrap + " Scrap   " + (reward.Trophies >= 0 ? "+" : "") + reward.Trophies + " Trophies" + (reward.IsFirstWinOfDay ? "   (includes first win of the day)" : "") + (!isSimulatedTeamMode && isWin ? "\nA free-for-all win counts as 1st place." : ""), MessageType.Info);

        if (GUILayout.Button("Apply To Profile"))
        {
            ProfileService.ApplyMatchReward(reward);
        }

        EditorGUILayout.Space(2f);
    }

    // Open states are kept in memory. EditorPrefs is read once per section and written only on change
    private bool BeginSection(string title, bool defaultOpen)
    {
        if (!sectionStates.TryGetValue(title, out bool isOpen))
        {
            isOpen = EditorPrefs.GetBool(SECTION_PREFIX + title, defaultOpen);
            sectionStates[title] = isOpen;
        }

        bool newOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);

        if (newOpen != isOpen)
        {
            sectionStates[title] = newOpen;
            EditorPrefs.SetBool(SECTION_PREFIX + title, newOpen);
        }

        return newOpen;
    }
}
