using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LoadoutMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private WeaponData[] weaponOptions;
    [SerializeField] private AbilityData[] abilityOptions;
    [SerializeField] private LoadoutOptionButton[] primaryButtons;
    [SerializeField] private LoadoutOptionButton[] secondaryButtons;
    [SerializeField] private LoadoutOptionButton[] abilityButtons;
    [SerializeField] private PassiveData[] passiveOptions = new PassiveData[0];
    [SerializeField] private LoadoutOptionButton[] passiveButtons = new LoadoutOptionButton[0];
    [SerializeField] private LoadoutOptionButton[] presetTabButtons;
    [SerializeField] private TMP_InputField presetNameInput;
    [SerializeField] private Button copyButton;
    [SerializeField] private TMP_Text copyHintText;
    [SerializeField] private Button closeButton;
    [SerializeField] private CharacterMenu characterMenu;
    [SerializeField] private GameObject abilityTooltip;
    [SerializeField] private TMP_Text tooltipTitle;
    [SerializeField] private TMP_Text tooltipBody;

    private readonly List<int> passiveIds = new List<int>();

    private int[] itemIds = new int[LoadoutPreset.SLOT_COUNT];
    private bool isChoosingCopyTarget;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        for (int i = 0; i < weaponOptions.Length; i++)
        {
            int index = i;

            primaryButtons[i].SetLabel(weaponOptions[i].DisplayName);
            secondaryButtons[i].SetLabel(weaponOptions[i].DisplayName);
            primaryButtons[i].Button.onClick.AddListener(() => SelectPrimary(index));
            secondaryButtons[i].Button.onClick.AddListener(() => SelectSecondary(index));
        }

        for (int i = 0; i < abilityOptions.Length; i++)
        {
            int index = i;

            abilityButtons[i].SetLabel(abilityOptions[i].DisplayName);
            abilityButtons[i].Button.onClick.AddListener(() => ToggleAbility(index));
            abilityButtons[i].HoverEntered += () => ShowAbilityTooltip(index);
            abilityButtons[i].HoverExited += HideAbilityTooltip;
        }

        for (int i = 0; i < passiveOptions.Length; i++)
        {
            int index = i;

            passiveButtons[i].SetLabel(passiveOptions[i].DisplayName);
            passiveButtons[i].Button.onClick.AddListener(() => TogglePassive(index));
            passiveButtons[i].HoverEntered += () => ShowPassiveTooltip(index);
            passiveButtons[i].HoverExited += HideAbilityTooltip;
        }

        for (int i = 0; i < presetTabButtons.Length; i++)
        {
            int index = i;

            presetTabButtons[i].Button.onClick.AddListener(() => ClickPresetTab(index));
        }

        presetNameInput.characterLimit = PresetService.MAX_NAME_LENGTH;
        presetNameInput.onEndEdit.AddListener(RenameSelectedPreset);
        copyButton.onClick.AddListener(ToggleCopyMode);
        closeButton.onClick.AddListener(Close);
        HideAbilityTooltip();
        SetCopyMode(false);
        LoadSelectedPreset();
        panel.SetActive(false);
        PlayerController.LocalDespawned += HandleLocalDespawned;
        PresetService.Changed += LoadSelectedPreset;
    }

    void OnDestroy()
    {
        PlayerController.LocalDespawned -= HandleLocalDespawned;
        PresetService.Changed -= LoadSelectedPreset;
    }

    void Update()
    {
        if (PlayerController.Local == null) return;

        if (MatchManager.Instance.Phase != MatchPhase.Lobby)
        {
            if (panel.activeSelf)
            {
                SetOpen(false);
            }

            return;
        }

        if (presetNameInput.isFocused) return;

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (panel.activeSelf)
            {
                SetOpen(false);
            }
            else
            {
                Open();
            }
        }
    }

    public void Open()
    {
        if (PlayerController.Local == null) return;

        characterMenu.Close();
        SetCopyMode(false);
        LoadSelectedPreset();
        SetOpen(true);
    }

    public void Close()
    {
        if (PlayerController.Local == null || !panel.activeSelf) return;

        SetOpen(false);
    }

    private void HandleLocalDespawned()
    {
        panel.SetActive(false);
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

    private void ToggleCopyMode()
    {
        SetCopyMode(!isChoosingCopyTarget);
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

    private void SelectPrimary(int index)
    {
        int chosen = weaponOptions[index].Id;

        if (chosen == itemIds[LoadoutPreset.SECONDARY_SLOT])
        {
            itemIds[LoadoutPreset.SECONDARY_SLOT] = itemIds[LoadoutPreset.PRIMARY_SLOT];
        }

        itemIds[LoadoutPreset.PRIMARY_SLOT] = chosen;
        SaveSelection();
    }

    private void SelectSecondary(int index)
    {
        int chosen = weaponOptions[index].Id;

        if (chosen == itemIds[LoadoutPreset.PRIMARY_SLOT])
        {
            itemIds[LoadoutPreset.PRIMARY_SLOT] = itemIds[LoadoutPreset.SECONDARY_SLOT];
        }

        itemIds[LoadoutPreset.SECONDARY_SLOT] = chosen;
        SaveSelection();
    }

    // The oldest ability is replaced, like before presets
    private void ToggleAbility(int index)
    {
        int chosen = abilityOptions[index].Id;

        if (chosen == itemIds[LoadoutPreset.FIRST_ABILITY_SLOT] || chosen == itemIds[LoadoutPreset.SECOND_ABILITY_SLOT]) return;

        itemIds[LoadoutPreset.FIRST_ABILITY_SLOT] = itemIds[LoadoutPreset.SECOND_ABILITY_SLOT];
        itemIds[LoadoutPreset.SECOND_ABILITY_SLOT] = chosen;
        SaveSelection();
    }

    private void TogglePassive(int index)
    {
        int chosen = passiveOptions[index].Id;

        if (passiveIds.Contains(chosen))
        {
            passiveIds.Remove(chosen);
        }
        else
        {
            if (passiveIds.Count >= LoadoutPreset.PASSIVE_SLOT_COUNT)
            {
                passiveIds.RemoveAt(0);
            }

            passiveIds.Add(chosen);
        }

        for (int slot = 0; slot < LoadoutPreset.PASSIVE_SLOT_COUNT; slot++)
        {
            itemIds[LoadoutPreset.FIRST_PASSIVE_SLOT + slot] = slot < passiveIds.Count ? passiveIds[slot] : 0;
        }

        SaveSelection();
    }

    // Saving fires PresetService.Changed, which reloads this menu and lets LocalPresetApplier equip it
    private void SaveSelection()
    {
        if (!PresetService.TrySetItems(PresetService.SelectedIndex, itemIds))
        {
            LoadSelectedPreset();
        }
    }

    private void LoadSelectedPreset()
    {
        LoadoutPreset preset = PresetService.Selected;

        itemIds = preset.CopyItemIds();
        passiveIds.Clear();

        for (int slot = 0; slot < LoadoutPreset.PASSIVE_SLOT_COUNT; slot++)
        {
            int id = itemIds[LoadoutPreset.FIRST_PASSIVE_SLOT + slot];

            if (id != 0)
            {
                passiveIds.Add(id);
            }
        }

        presetNameInput.SetTextWithoutNotify(preset.Name);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < weaponOptions.Length; i++)
        {
            bool isWeaponLocked = !ProfileService.IsOwned(weaponOptions[i]);

            primaryButtons[i].SetLocked(isWeaponLocked);
            secondaryButtons[i].SetLocked(isWeaponLocked);
            primaryButtons[i].SetSelected(weaponOptions[i].Id == itemIds[LoadoutPreset.PRIMARY_SLOT]);
            secondaryButtons[i].SetSelected(weaponOptions[i].Id == itemIds[LoadoutPreset.SECONDARY_SLOT]);
        }

        for (int i = 0; i < abilityOptions.Length; i++)
        {
            int id = abilityOptions[i].Id;

            abilityButtons[i].SetLocked(!ProfileService.IsOwned(abilityOptions[i]));
            abilityButtons[i].SetSelected(id == itemIds[LoadoutPreset.FIRST_ABILITY_SLOT] || id == itemIds[LoadoutPreset.SECOND_ABILITY_SLOT]);
        }

        for (int i = 0; i < passiveOptions.Length; i++)
        {
            passiveButtons[i].SetLocked(!ProfileService.IsOwned(passiveOptions[i]));
            passiveButtons[i].SetSelected(passiveIds.Contains(passiveOptions[i].Id));
        }

        for (int i = 0; i < presetTabButtons.Length; i++)
        {
            bool hasPreset = i < PresetService.Count;

            presetTabButtons[i].gameObject.SetActive(hasPreset);

            if (!hasPreset) continue;

            presetTabButtons[i].SetLabel(PresetService.Get(i).Name);
            presetTabButtons[i].SetSelected(i == PresetService.SelectedIndex);
        }
    }

    private void ShowPassiveTooltip(int index)
    {
        PassiveData passive = passiveOptions[index];

        tooltipTitle.text = passive.DisplayName;
        tooltipBody.text = passive.Description;
        abilityTooltip.SetActive(true);
    }

    private void ShowAbilityTooltip(int index)
    {
        AbilityData ability = abilityOptions[index];

        tooltipTitle.text = ability.DisplayName;
        tooltipBody.text = ability.Description + "\n\nCooldown: " + ability.Cooldown.ToString("0.#") + "s";
        abilityTooltip.SetActive(true);
    }

    private void HideAbilityTooltip()
    {
        abilityTooltip.SetActive(false);
    }

    private void SetOpen(bool isOpen)
    {
        HideAbilityTooltip();
        panel.SetActive(isOpen);
        PlayerController.Local.InputReader.SetBlocked(isOpen);
    }
}
