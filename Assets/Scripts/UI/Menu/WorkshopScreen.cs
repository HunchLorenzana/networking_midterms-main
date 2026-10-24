using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkshopScreen : MonoBehaviour
{
    private const int SORT_DEFAULT = 0;
    private const int SORT_OWNED = 1;
    private const int SORT_PRICE = 2;

    private static readonly string[] slotNames = { "PRIMARY", "SECONDARY", "ABILITY 1", "ABILITY 2", "PASSIVE 1", "PASSIVE 2", "PASSIVE 3" };
    private static readonly string[] sortNames = { "Sort: Default", "Sort: Owned", "Sort: Price" };

    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private LoadoutCatalog catalog;
    [SerializeField] private HeroDisplay heroDisplay;
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private Button backButton;
    [SerializeField] private LoadoutOptionButton[] presetTabButtons;
    [SerializeField] private Button addPresetButton;
    [SerializeField] private TMP_Text scrapText;
    [SerializeField] private LoadoutOptionButton[] slotButtons;
    [SerializeField] private StatBar healthBar;
    [SerializeField] private StatBar speedBar;
    [SerializeField] private StatBar damageBar;
    [SerializeField] private StatBar reloadBar;
    [SerializeField] private StatBar rangeBar;
    [SerializeField] private StatBar bulletSpeedBar;
    [SerializeField] private Transform itemListContent;
    [SerializeField] private ItemCardButton itemCardPrefab;
    [SerializeField] private Button sortButton;
    [SerializeField] private TMP_Text sortText;
    [SerializeField] private TMP_Text itemDetailText;
    [SerializeField] private TMP_InputField presetNameInput;
    [SerializeField] private Button copyButton;
    [SerializeField] private TMP_Text copyHintText;
    [SerializeField] private ConfirmPurchasePopup purchasePopup;

    private readonly List<ItemCardButton> cards = new List<ItemCardButton>();
    // A null entry is the "empty" option of passive slots
    private readonly List<ItemData> shownItems = new List<ItemData>();

    private int[] itemIds = new int[LoadoutPreset.SLOT_COUNT];
    private int selectedSlot;
    private int sortIndex;
    private bool isChoosingCopyTarget;

    void Awake()
    {
        backButton.onClick.AddListener(screenManager.ShowHome);
        addPresetButton.onClick.AddListener(() => screenManager.ShowComingSoon("More preset slots"));
        sortButton.onClick.AddListener(CycleSort);
        copyButton.onClick.AddListener(() => SetCopyMode(!isChoosingCopyTarget));
        presetNameInput.characterLimit = PresetService.MAX_NAME_LENGTH;
        presetNameInput.onEndEdit.AddListener(RenameSelectedPreset);

        for (int i = 0; i < presetTabButtons.Length; i++)
        {
            int index = i;

            presetTabButtons[i].Button.onClick.AddListener(() => ClickPresetTab(index));
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;

            slotButtons[i].Button.onClick.AddListener(() => SelectSlot(index));
        }

        healthBar.SetLabel("HP");
        speedBar.SetLabel("Speed");
        damageBar.SetLabel("Damage");
        reloadBar.SetLabel("Reload");
        rangeBar.SetLabel("Range");
        bulletSpeedBar.SetLabel("Bullet speed");
    }

    void OnEnable()
    {
        ProfileService.Changed += Reload;
        PresetService.Changed += Reload;
        SetCopyMode(false);
        purchasePopup.Hide();
        Reload();
    }

    void OnDisable()
    {
        ProfileService.Changed -= Reload;
        PresetService.Changed -= Reload;
    }

    private void Reload()
    {
        itemIds = PresetService.Selected.CopyItemIds();
        RefreshHeader();
        RefreshSlots();
        RefreshItemList();
        ClearPreview();
    }

    private void RefreshHeader()
    {
        scrapText.text = ProfileService.Current.Scrap + " Scrap";
        presetNameInput.SetTextWithoutNotify(PresetService.Selected.Name);

        for (int i = 0; i < presetTabButtons.Length; i++)
        {
            bool hasPreset = i < PresetService.Count;

            presetTabButtons[i].gameObject.SetActive(hasPreset);

            if (!hasPreset) continue;

            presetTabButtons[i].SetLabel(PresetService.Get(i).Name);
            presetTabButtons[i].SetSelected(i == PresetService.SelectedIndex);
        }
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            ItemData item = FindItem(itemIds[i]);

            slotButtons[i].SetLabel(slotNames[i] + "\n" + (item != null ? item.DisplayName : "Empty"));
            slotButtons[i].SetSelected(i == selectedSlot);
        }
    }

    private void RefreshItemList()
    {
        shownItems.Clear();

        if (IsPassiveSlot(selectedSlot))
        {
            shownItems.Add(null);
        }

        shownItems.AddRange(GetSortedOptions(selectedSlot));

        while (cards.Count < shownItems.Count)
        {
            CreateCard();
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool isUsed = i < shownItems.Count;

            cards[i].gameObject.SetActive(isUsed);

            if (!isUsed) continue;

            ItemData item = shownItems[i];
            int id = item != null ? item.Id : 0;
            bool isLocked = item != null && !ProfileService.IsOwned(item);

            cards[i].SetContent(item != null ? item.DisplayName : "Empty", GetCardInfo(item, isLocked));
            cards[i].SetState(id == itemIds[selectedSlot], isLocked);
        }

        sortText.text = sortNames[sortIndex];
    }

    private void CreateCard()
    {
        int index = cards.Count;
        ItemCardButton card = Instantiate(itemCardPrefab, itemListContent);

        card.Button.onClick.AddListener(() => ClickCard(index));
        card.HoverEntered += () => ShowPreview(index);
        card.HoverExited += ClearPreview;
        cards.Add(card);
    }

    private string GetCardInfo(ItemData item, bool isLocked)
    {
        if (item == null) return "No passive";

        if (isLocked) return item.ScrapPrice + " Scrap";

        if (item.Id == itemIds[selectedSlot]) return "Equipped";

        int otherSlot = FindSlotInGroup(itemIds, item.Id, selectedSlot);

        return otherSlot >= 0 ? "In " + slotNames[otherSlot] : "Owned";
    }

    private void SelectSlot(int slot)
    {
        selectedSlot = slot;
        RefreshSlots();
        RefreshItemList();
        ClearPreview();
    }

    private void CycleSort()
    {
        sortIndex = (sortIndex + 1) % sortNames.Length;
        RefreshItemList();
    }

    private void ClickPresetTab(int index)
    {
        if (isChoosingCopyTarget)
        {
            PresetService.TryCopy(PresetService.SelectedIndex, index);
            SetCopyMode(false);
        }

        PresetService.Select(index);
    }

    private void SetCopyMode(bool isOn)
    {
        isChoosingCopyTarget = isOn;
        copyHintText.gameObject.SetActive(isOn);
    }

    private void RenameSelectedPreset(string newName)
    {
        if (!PresetService.TryRename(PresetService.SelectedIndex, newName))
        {
            presetNameInput.SetTextWithoutNotify(PresetService.Selected.Name);
        }
    }

    private void ClickCard(int index)
    {
        ItemData item = shownItems[index];

        if (item == null || ProfileService.IsOwned(item))
        {
            Equip(item != null ? item.Id : 0);

            return;
        }

        int scrap = ProfileService.Current.Scrap;
        bool canAfford = scrap >= item.ScrapPrice;

        purchasePopup.Show(
            "Unlock " + item.DisplayName + "?",
            item.Description + "\n\nCosts " + item.ScrapPrice + " Scrap. You have " + scrap + ".",
            canAfford,
            () => BuyAndEquip(item));
    }

    private void BuyAndEquip(ItemData item)
    {
        if (ProfileService.TryPurchase(item, out _))
        {
            Equip(item.Id);
        }
    }

    private void Equip(int id)
    {
        PresetService.TrySetItems(PresetService.SelectedIndex, WithItem(itemIds, selectedSlot, id));
    }

    // Puts the item in the slot. If it is already in another slot of the same kind, the two swap
    private static int[] WithItem(int[] ids, int slot, int id)
    {
        int[] result = (int[])ids.Clone();
        int otherSlot = id != 0 ? FindSlotInGroup(result, id, slot) : -1;

        if (otherSlot >= 0)
        {
            result[otherSlot] = result[slot];
        }

        result[slot] = id;

        return result;
    }

    private void ShowPreview(int index)
    {
        ItemData item = shownItems[index];
        int[] previewIds = WithItem(itemIds, selectedSlot, item != null ? item.Id : 0);

        ShowStats(PresetStats.Calculate(catalog, itemIds), PresetStats.Calculate(catalog, previewIds));
        itemDetailText.text = GetDetail(item);
        ShowHeroWeapon(previewIds);
    }

    private void ClearPreview()
    {
        StatTotals current = PresetStats.Calculate(catalog, itemIds);

        ShowStats(current, current);
        itemDetailText.text = GetDetail(FindItem(itemIds[selectedSlot]));
        ShowHeroWeapon(itemIds);
    }

    private void ShowHeroWeapon(int[] ids)
    {
        int slot = selectedSlot == LoadoutPreset.SECONDARY_SLOT ? LoadoutPreset.SECONDARY_SLOT : LoadoutPreset.PRIMARY_SLOT;

        heroDisplay.ShowWeapon(catalog.GetWeapon(ids[slot]));
    }

    private void ShowStats(StatTotals current, StatTotals preview)
    {
        healthBar.ShowPreview(StatDisplay.GetHealthFill(current), Mathf.RoundToInt(baseHealth * current.MaxHealth).ToString(),
            StatDisplay.GetHealthFill(preview), Mathf.RoundToInt(baseHealth * preview.MaxHealth).ToString());
        ShowMultiplier(speedBar, current.MoveSpeed, preview.MoveSpeed);
        ShowMultiplier(damageBar, current.DamageDealt, preview.DamageDealt);
        ShowMultiplier(reloadBar, current.ReloadSpeed, preview.ReloadSpeed);
        ShowMultiplier(rangeBar, current.Range, preview.Range);
        ShowMultiplier(bulletSpeedBar, current.ProjectileSpeed, preview.ProjectileSpeed);
    }

    private static void ShowMultiplier(StatBar bar, float current, float preview)
    {
        bar.ShowPreview(StatDisplay.GetMultiplierFill(current), StatDisplay.FormatPercent(current), StatDisplay.GetMultiplierFill(preview), StatDisplay.FormatPercent(preview));
    }

    private static string GetDetail(ItemData item)
    {
        if (item == null) return "";

        string detail = "<b>" + item.DisplayName + "</b>\n" + item.Description;

        if (item is WeaponData weapon)
        {
            detail += "\n" + (weapon.FireMode == WeaponFireMode.HoldToFire ? "Hold to fire" : "Aim and release");
        }
        else if (item is AbilityData ability)
        {
            detail += "\nCooldown: " + ability.Cooldown.ToString("0.#") + "s";
        }

        return detail;
    }

    private List<ItemData> GetSortedOptions(int slot)
    {
        List<ItemData> options = new List<ItemData>();

        foreach (ItemData item in GetOptions(slot))
        {
            if (item != null)
            {
                options.Add(item);
            }
        }

        if (sortIndex == SORT_DEFAULT) return options;

        List<ItemData> original = new List<ItemData>(options);

        options.Sort((a, b) =>
        {
            int compare = sortIndex == SORT_OWNED
                ? ProfileService.IsOwned(b).CompareTo(ProfileService.IsOwned(a))
                : a.ScrapPrice.CompareTo(b.ScrapPrice);

            return compare != 0 ? compare : original.IndexOf(a).CompareTo(original.IndexOf(b));
        });

        return options;
    }

    private IEnumerable<ItemData> GetOptions(int slot)
    {
        if (slot <= LoadoutPreset.SECONDARY_SLOT) return catalog.Weapons;

        if (slot <= LoadoutPreset.SECOND_ABILITY_SLOT) return catalog.Abilities;

        return catalog.Passives;
    }

    private ItemData FindItem(int id)
    {
        if (catalog.TryGetWeapon(id, out WeaponData weapon)) return weapon;

        if (catalog.TryGetAbility(id, out AbilityData ability)) return ability;

        return catalog.GetPassive(id);
    }

    private static bool IsPassiveSlot(int slot)
    {
        return slot >= LoadoutPreset.FIRST_PASSIVE_SLOT;
    }

    private static int FindSlotInGroup(int[] ids, int id, int slot)
    {
        GetGroup(slot, out int start, out int count);

        for (int i = start; i < start + count; i++)
        {
            if (i != slot && ids[i] == id) return i;
        }

        return -1;
    }

    private static void GetGroup(int slot, out int start, out int count)
    {
        if (slot <= LoadoutPreset.SECONDARY_SLOT)
        {
            start = LoadoutPreset.PRIMARY_SLOT;
            count = 2;
        }
        else if (slot <= LoadoutPreset.SECOND_ABILITY_SLOT)
        {
            start = LoadoutPreset.FIRST_ABILITY_SLOT;
            count = 2;
        }
        else
        {
            start = LoadoutPreset.FIRST_PASSIVE_SLOT;
            count = LoadoutPreset.PASSIVE_SLOT_COUNT;
        }
    }
}
