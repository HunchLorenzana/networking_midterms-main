using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeScreen : MonoBehaviour
{
    private const int PRACTICE_MODE_INDEX = 2;

    private static readonly GameMode[] modes = { GameMode.FreeForAll, GameMode.TeamDeathmatch };
    private static readonly string[] modeNames = { "FREE-FOR-ALL", "TEAM DEATHMATCH", "PRACTICE" };
    private static int selectedModeIndex;

    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private LoadoutCatalog catalog;
    [SerializeField] private EconomyConfig config;
    [SerializeField] private HeroDisplay heroDisplay;
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_Text trophiesText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text trophyRoadText;
    [SerializeField] private Image trophyRoadFillImage;
    [SerializeField] private TMP_Text scrapText;
    [SerializeField] private TMP_Text coresText;
    [SerializeField] private Button workshopButton;
    [SerializeField] private GameObject workshopDot;
    [SerializeField] private LoadoutOptionButton[] presetButtons;
    [SerializeField] private StatBar healthBar;
    [SerializeField] private StatBar speedBar;
    [SerializeField] private StatBar damageBar;
    [SerializeField] private Button modeCardButton;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private Button playButton;
    [SerializeField] private MatchmakingPanel matchmakingPanel;

    void Awake()
    {
        ProfileService.EnsureStartingScrap(config.StartingScrap);
        PresetOwnership.Sanitize(catalog);

        if (string.IsNullOrEmpty(ProfileService.Current.PlayerName))
        {
            ProfileService.SetPlayerName("Player" + Random.Range(1, 100));
        }

        for (int i = 0; i < presetButtons.Length; i++)
        {
            int index = i;

            presetButtons[i].Button.onClick.AddListener(() => PresetService.Select(index));
        }

        playerNameInput.characterLimit = PlayerNames.MAX_LENGTH;
        playerNameInput.onEndEdit.AddListener(RenamePlayer);
        workshopButton.onClick.AddListener(screenManager.ShowWorkshop);
        modeCardButton.onClick.AddListener(CycleMode);
        playButton.onClick.AddListener(Play);
        healthBar.SetLabel("HP");
        speedBar.SetLabel("SPD");
        damageBar.SetLabel("DMG");
    }

    void OnEnable()
    {
        ProfileService.Changed += Refresh;
        PresetService.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        ProfileService.Changed -= Refresh;
        PresetService.Changed -= Refresh;
    }

    private void RenamePlayer(string newName)
    {
        string cleaned = PlayerNames.Clean(newName);

        if (cleaned.Length > 0)
        {
            ProfileService.SetPlayerName(cleaned);
        }

        playerNameInput.SetTextWithoutNotify(ProfileService.Current.PlayerName);
    }

    private void CycleMode()
    {
        selectedModeIndex = (selectedModeIndex + 1) % modeNames.Length;
        Refresh();
    }

    // Practice opens the practice map directly, the other modes search for a match first
    private void Play()
    {
        if (matchmakingPanel.IsSearching) return;

        if (selectedModeIndex == PRACTICE_MODE_INDEX)
        {
            MenuLaunch.LaunchPractice();

            return;
        }

        matchmakingPanel.Begin(modes[selectedModeIndex], modeNames[selectedModeIndex]);
    }

    private void Refresh()
    {
        PlayerProfile profile = ProfileService.Current;
        int nextMilestone = config.GetNextMilestone(profile.Trophies, out int previousMilestone);
        bool isRankedUnlocked = profile.Trophies >= config.RankedUnlockTrophies;
        LoadoutPreset selected = PresetService.Selected;
        int[] itemIds = selected.CopyItemIds();
        StatTotals stats = PresetStats.Calculate(catalog, itemIds);

        playerNameInput.SetTextWithoutNotify(profile.PlayerName);
        trophiesText.text = profile.Trophies + " Trophies";
        rankText.text = isRankedUnlocked ? "Unranked" : "Ranked at " + config.RankedUnlockTrophies;
        trophyRoadText.text = profile.Trophies >= nextMilestone ? "Trophy Road complete" : "Next reward: " + nextMilestone;
        trophyRoadFillImage.fillAmount = nextMilestone <= previousMilestone ? 1f : Mathf.InverseLerp(previousMilestone, nextMilestone, profile.Trophies);
        scrapText.text = profile.Scrap.ToString();
        coresText.text = profile.Cores.ToString();
        workshopDot.SetActive(HasAffordableUnlock(profile.Scrap));

        for (int i = 0; i < presetButtons.Length; i++)
        {
            bool hasPreset = i < PresetService.Count;

            presetButtons[i].gameObject.SetActive(hasPreset);

            if (!hasPreset) continue;

            presetButtons[i].SetLabel(PresetService.Get(i).Name);
            presetButtons[i].SetSelected(i == PresetService.SelectedIndex);
        }

        healthBar.Show(StatDisplay.GetHealthFill(stats), Mathf.RoundToInt(baseHealth * stats.MaxHealth).ToString());
        speedBar.Show(StatDisplay.GetMultiplierFill(stats.MoveSpeed), StatDisplay.FormatPercent(stats.MoveSpeed));
        damageBar.Show(StatDisplay.GetMultiplierFill(stats.DamageDealt), StatDisplay.FormatPercent(stats.DamageDealt));
        modeText.text = modeNames[selectedModeIndex];
        heroDisplay.ShowWeapon(catalog.GetWeapon(itemIds[LoadoutPreset.PRIMARY_SLOT]));
    }

    private bool HasAffordableUnlock(int scrap)
    {
        return HasAffordable(catalog.Weapons, scrap) || HasAffordable(catalog.Abilities, scrap) || HasAffordable(catalog.Passives, scrap);
    }

    private static bool HasAffordable<T>(System.Collections.Generic.IReadOnlyList<T> items, int scrap) where T : ItemData
    {
        foreach (T item in items)
        {
            if (item != null && !ProfileService.IsOwned(item) && item.ScrapPrice <= scrap) return true;
        }

        return false;
    }
}
