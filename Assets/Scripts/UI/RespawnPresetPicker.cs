using UnityEngine;
using UnityEngine.InputSystem;

public class RespawnPresetPicker : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private LoadoutOptionButton[] presetCards;
    [SerializeField] private LoadoutCatalog catalog;

    private bool isShown;

    void Awake()
    {
        panel.SetActive(false);
    }

    void Start()
    {
        for (int i = 0; i < presetCards.Length; i++)
        {
            int index = i;

            presetCards[i].Button.onClick.AddListener(() => PresetService.Select(index));
        }

        PresetService.Changed += RefreshCards;
    }

    void OnDestroy()
    {
        PresetService.Changed -= RefreshCards;
    }

    void Update()
    {
        PlayerController local = PlayerController.Local;
        bool shouldShow = local != null && !local.Health.IsAlive && MatchManager.Instance.Phase == MatchPhase.Playing;

        if (shouldShow != isShown)
        {
            isShown = shouldShow;
            panel.SetActive(shouldShow);

            if (shouldShow)
            {
                RefreshCards();
            }
        }

        if (isShown)
        {
            ReadNumberKeys();
        }
    }

    private void ReadNumberKeys()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            PresetService.Select(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            PresetService.Select(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            PresetService.Select(2);
        }
    }

    private void RefreshCards()
    {
        for (int i = 0; i < presetCards.Length; i++)
        {
            bool hasPreset = i < PresetService.Count;

            presetCards[i].gameObject.SetActive(hasPreset);

            if (!hasPreset) continue;

            presetCards[i].SetLabel(BuildCardText(i, PresetService.Get(i)));
            presetCards[i].SetSelected(i == PresetService.SelectedIndex);
        }
    }

    private string BuildCardText(int index, LoadoutPreset preset)
    {
        return (index + 1) + "  " + preset.Name
            + "\n" + GetWeaponName(preset.GetItemId(LoadoutPreset.PRIMARY_SLOT)) + " + " + GetWeaponName(preset.GetItemId(LoadoutPreset.SECONDARY_SLOT))
            + "\n" + GetAbilityName(preset.GetItemId(LoadoutPreset.FIRST_ABILITY_SLOT)) + " + " + GetAbilityName(preset.GetItemId(LoadoutPreset.SECOND_ABILITY_SLOT));
    }

    private string GetWeaponName(int id)
    {
        return catalog.TryGetWeapon(id, out WeaponData weapon) ? weapon.DisplayName : "-";
    }

    private string GetAbilityName(int id)
    {
        return catalog.TryGetAbility(id, out AbilityData ability) ? ability.DisplayName : "-";
    }
}
