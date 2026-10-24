using TMPro;
using UnityEngine;

public class CombatHud : MonoBehaviour
{
    private static readonly string[] SlotPrefixes = { "LMB ", "RMB " };
    private static readonly string[] AbilityKeys = { "[Q] ", "[E] " };

    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text[] weaponTexts;
    [SerializeField] private UIBar[] ammoFills;
    [SerializeField] private Color ammoColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color emptyColor = new Color(0.91f, 0.33f, 0.18f);
    [SerializeField] private TMP_Text[] abilityTexts;
    [SerializeField] private UIBar healthFill;
    [SerializeField] private GameObject healthGroup;
    [SerializeField] private RectTransform abilitiesGroup;
    [SerializeField] private float lobbyDrop = 88f;
    [SerializeField] private UIBar[] fireCooldownFills;
    [SerializeField] private UIBar[] abilityFills;
    [SerializeField] private Color healthColor = new Color(0.45f, 0.85f, 0.35f);
    [SerializeField] private Color cooldownColor = new Color(0.75f, 0.85f, 1f);
    [SerializeField] private Color abilityChargingColor = new Color(0.6f, 0.45f, 0.9f);
    [SerializeField] private Color abilityReadyColor = new Color(1f, 0.6f, 0.12f);

    private readonly WeaponData[] shownWeapons = new WeaponData[PlayerWeapon.SLOT_COUNT];
    private readonly int[] shownAmmo = { -1, -1 };
    private readonly AbilityData[] shownAbilities = new AbilityData[AbilityController.SLOT_COUNT];
    private readonly int[] shownTenths = { -1, -1 };

    private Health health;
    private PlayerWeapon weapon;
    private AbilityController abilities;
    private int shownHealth = -1;
    private int shownActiveSlot = -1;

    void Update()
    {
        PlayerController local = PlayerController.Local;

        if (local == null) return;

        if (local.Health != health)
        {
            Bind(local);
        }

        bool isLobby = MatchManager.Instance.Phase == MatchPhase.Lobby;

        if (healthGroup.activeSelf == isLobby)
        {
            healthGroup.SetActive(!isLobby);
            abilitiesGroup.anchoredPosition = new Vector2(0f, isLobby ? -lobbyDrop : 0f);
        }

        if (health.CurrentHealth != shownHealth)
        {
            shownHealth = health.CurrentHealth;
            healthText.text = "HP " + shownHealth;
        }

        healthFill.SetFraction(health.Fraction);
        healthFill.SetColor(healthColor);

        for (int slot = 0; slot < PlayerWeapon.SLOT_COUNT; slot++)
        {
            RefreshWeapon(slot);
        }

        shownActiveSlot = weapon.ActiveSlot;

        for (int slot = 0; slot < AbilityController.SLOT_COUNT; slot++)
        {
            RefreshAbility(slot);
        }
    }

    private void Bind(PlayerController player)
    {
        health = player.Health;
        weapon = player.Weapon;
        abilities = player.Abilities;
        shownHealth = -1;
        shownActiveSlot = -1;
        System.Array.Clear(shownWeapons, 0, shownWeapons.Length);
        System.Array.Clear(shownAbilities, 0, shownAbilities.Length);
        shownTenths[0] = -1;
        shownTenths[1] = -1;
    }

    private void RefreshWeapon(int slot)
    {
        ammoFills[slot].SetFraction(weapon.GetAmmoFraction(slot));
        ammoFills[slot].SetColor(weapon.GetAmmoCount(slot) < 1 ? emptyColor : ammoColor);
        fireCooldownFills[slot].SetFraction(weapon.GetFireReadyFraction(slot));
        fireCooldownFills[slot].SetColor(cooldownColor);

        WeaponData data = weapon.GetWeapon(slot);
        int ammo = weapon.GetAmmoCount(slot);

        if (data == shownWeapons[slot] && ammo == shownAmmo[slot] && shownActiveSlot == weapon.ActiveSlot) return;

        shownWeapons[slot] = data;
        shownAmmo[slot] = ammo;

        string marker = weapon.ActiveSlot == slot ? "> " : "  ";

        weaponTexts[slot].text = marker + SlotPrefixes[slot] + data.DisplayName + "  " + ammo + " / " + data.MagazineSize;
    }

    private void RefreshAbility(int slot)
    {
        AbilityData data = abilities.GetAbility(slot);
        float readyFraction = abilities.GetReadyFraction(slot);

        abilityFills[slot].SetFraction(readyFraction);
        abilityFills[slot].SetColor(readyFraction >= 1f ? abilityReadyColor : abilityChargingColor);

        int tenths = Mathf.CeilToInt(abilities.GetCooldownRemaining(slot) * 10f);

        if (data == shownAbilities[slot] && tenths == shownTenths[slot]) return;

        shownAbilities[slot] = data;
        shownTenths[slot] = tenths;

        string status = tenths == 0 ? "READY" : (tenths / 10f).ToString("0.0") + "s";

        abilityTexts[slot].text = AbilityKeys[slot] + data.DisplayName + "  " + status;
    }
}
