using System;
using Unity.Netcode;
using UnityEngine;

public struct LoadoutIds : INetworkSerializable, IEquatable<LoadoutIds>
{
    public const int PASSIVE_SLOT_COUNT = 3;

    private int primary;
    private int secondary;
    private int firstAbility;
    private int secondAbility;
    private int firstPassive;
    private int secondPassive;
    private int thirdPassive;

    public int Primary => primary;
    public int Secondary => secondary;
    public int FirstAbility => firstAbility;
    public int SecondAbility => secondAbility;

    public LoadoutIds(int primary, int secondary, int firstAbility, int secondAbility, int firstPassive, int secondPassive, int thirdPassive)
    {
        this.primary = primary;
        this.secondary = secondary;
        this.firstAbility = firstAbility;
        this.secondAbility = secondAbility;
        this.firstPassive = firstPassive;
        this.secondPassive = secondPassive;
        this.thirdPassive = thirdPassive;
    }

    public int GetPassive(int slot)
    {
        return slot == 0 ? firstPassive : slot == 1 ? secondPassive : thirdPassive;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref primary);
        serializer.SerializeValue(ref secondary);
        serializer.SerializeValue(ref firstAbility);
        serializer.SerializeValue(ref secondAbility);
        serializer.SerializeValue(ref firstPassive);
        serializer.SerializeValue(ref secondPassive);
        serializer.SerializeValue(ref thirdPassive);
    }

    public bool Equals(LoadoutIds other)
    {
        return primary == other.primary && secondary == other.secondary && firstAbility == other.firstAbility && secondAbility == other.secondAbility
            && firstPassive == other.firstPassive && secondPassive == other.secondPassive && thirdPassive == other.thirdPassive;
    }
}

public class PlayerLoadout : NetworkBehaviour
{
    private const int DEFAULT_PRIMARY_ID = 1001;
    private const int DEFAULT_SECONDARY_ID = 1002;
    private const int DEFAULT_FIRST_ABILITY_ID = 2001;
    private const int DEFAULT_SECOND_ABILITY_ID = 2002;

    [SerializeField] private LoadoutCatalog catalog;
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private AbilityController abilities;
    [SerializeField] private Health health;

    private string label = "";
    private bool hasReceivedLoadout;
    private bool hasPendingLoadout;
    private LoadoutIds pendingLoadout;

    public string Label => label;
    public LoadoutIds Current => loadout.Value;

    private readonly NetworkVariable<LoadoutIds> loadout = new NetworkVariable<LoadoutIds>(
        new LoadoutIds(DEFAULT_PRIMARY_ID, DEFAULT_SECONDARY_ID, DEFAULT_FIRST_ABILITY_ID, DEFAULT_SECOND_ABILITY_ID, 0, 0, 0), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Revived += HandleRevived;
        }

        loadout.OnValueChanged += HandleLoadoutChanged;
        Apply();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            health.Revived -= HandleRevived;
        }

        loadout.OnValueChanged -= HandleLoadoutChanged;
    }

    public int GetPassiveId(int slot)
    {
        return loadout.Value.GetPassive(slot);
    }

    public void RequestLoadout(LoadoutIds ids)
    {
        if (!IsOwner) return;

        SetLoadoutRpc(ids);
    }

    // In the lobby, or for the first loadout of a life, the change applies right away. Otherwise it waits for the next respawn
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetLoadoutRpc(LoadoutIds ids)
    {
        if (!IsLoadoutValid(ids)) return;

        bool canApplyNow = !hasReceivedLoadout || MatchManager.Instance.Phase == MatchPhase.Lobby;

        hasReceivedLoadout = true;

        if (canApplyNow)
        {
            hasPendingLoadout = false;
            loadout.Value = ids;

            return;
        }

        pendingLoadout = ids;
        hasPendingLoadout = !pendingLoadout.Equals(loadout.Value);
    }

    private bool IsLoadoutValid(LoadoutIds ids)
    {
        bool isWeaponValid = catalog.TryGetWeapon(ids.Primary, out _) && catalog.TryGetWeapon(ids.Secondary, out _) && ids.Primary != ids.Secondary;
        bool isAbilityValid = catalog.TryGetAbility(ids.FirstAbility, out _) && catalog.TryGetAbility(ids.SecondAbility, out _) && ids.FirstAbility != ids.SecondAbility;
        int firstPassive = ids.GetPassive(0);
        int secondPassive = ids.GetPassive(1);
        int thirdPassive = ids.GetPassive(2);
        bool isPassiveValid = IsPassiveSlotValid(firstPassive) && IsPassiveSlotValid(secondPassive) && IsPassiveSlotValid(thirdPassive)
            && !IsDuplicate(firstPassive, secondPassive) && !IsDuplicate(firstPassive, thirdPassive) && !IsDuplicate(secondPassive, thirdPassive);

        return isWeaponValid && isAbilityValid && isPassiveValid;
    }

    // Health is already reset when this runs, and Health.SetMaxHealthMultiplier keeps a full bar full
    private void HandleRevived()
    {
        if (!hasPendingLoadout) return;

        hasPendingLoadout = false;
        loadout.Value = pendingLoadout;
    }

    private bool IsPassiveSlotValid(int id)
    {
        return id == 0 || catalog.TryGetPassive(id, out _);
    }

    private static bool IsDuplicate(int first, int second)
    {
        return first != 0 && first == second;
    }

    private void HandleLoadoutChanged(LoadoutIds previousValue, LoadoutIds newValue)
    {
        Apply();
    }

    private WeaponData ResolveWeapon(int id, int fallbackIndex)
    {
        return catalog.TryGetWeapon(id, out WeaponData found) ? found : catalog.GetWeaponAt(fallbackIndex);
    }

    private AbilityData ResolveAbility(int id, int fallbackIndex)
    {
        return catalog.TryGetAbility(id, out AbilityData found) ? found : catalog.GetAbilityAt(fallbackIndex);
    }

    private void Apply()
    {
        LoadoutIds ids = loadout.Value;
        WeaponData primary = ResolveWeapon(ids.Primary, 0);
        WeaponData secondary = ResolveWeapon(ids.Secondary, 1);
        AbilityData firstAbility = ResolveAbility(ids.FirstAbility, 0);
        AbilityData secondAbility = ResolveAbility(ids.SecondAbility, 1);
        PassiveData[] equipped = new PassiveData[LoadoutIds.PASSIVE_SLOT_COUNT];

        for (int i = 0; i < equipped.Length; i++)
        {
            catalog.TryGetPassive(ids.GetPassive(i), out equipped[i]);
        }

        weapon.SetLoadout(primary, secondary);
        weapon.SetPassives(equipped);
        abilities.SetAbilities(firstAbility, secondAbility);

        label = primary.DisplayName + " + " + secondary.DisplayName + "  |  " + firstAbility.DisplayName + " + " + secondAbility.DisplayName;
    }
}
