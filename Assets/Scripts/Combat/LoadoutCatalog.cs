using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LoadoutCatalog", menuName = "KITBASH/Loadout Catalog")]
public class LoadoutCatalog : ScriptableObject
{
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private AbilityData[] abilities;
    [SerializeField] private PassiveData[] passives = new PassiveData[0];

    [System.NonSerialized] private Dictionary<int, WeaponData> weaponsById;
    [System.NonSerialized] private Dictionary<int, AbilityData> abilitiesById;
    [System.NonSerialized] private Dictionary<int, PassiveData> passivesById;

    public int WeaponCount => weapons.Length;
    public int AbilityCount => abilities.Length;
    public int PassiveCount => passives.Length;
    public IReadOnlyList<WeaponData> Weapons => weapons;
    public IReadOnlyList<AbilityData> Abilities => abilities;
    public IReadOnlyList<PassiveData> Passives => passives;

    void OnValidate()
    {
        weaponsById = null;
        abilitiesById = null;
        passivesById = null;
        WarnAboutBadIds();
    }

    public WeaponData GetWeaponAt(int index)
    {
        return weapons[index];
    }

    public AbilityData GetAbilityAt(int index)
    {
        return abilities[index];
    }

    public bool TryGetWeapon(int id, out WeaponData weapon)
    {
        weaponsById ??= BuildLookup(weapons);

        return weaponsById.TryGetValue(id, out weapon);
    }

    public bool TryGetAbility(int id, out AbilityData ability)
    {
        abilitiesById ??= BuildLookup(abilities);

        return abilitiesById.TryGetValue(id, out ability);
    }

    public bool TryGetPassive(int id, out PassiveData passive)
    {
        passivesById ??= BuildLookup(passives);

        return passivesById.TryGetValue(id, out passive);
    }

    public WeaponData GetWeapon(int id)
    {
        return TryGetWeapon(id, out WeaponData weapon) ? weapon : null;
    }

    public AbilityData GetAbility(int id)
    {
        return TryGetAbility(id, out AbilityData ability) ? ability : null;
    }

    public PassiveData GetPassive(int id)
    {
        return TryGetPassive(id, out PassiveData passive) ? passive : null;
    }

    private static Dictionary<int, T> BuildLookup<T>(T[] items) where T : ItemData
    {
        Dictionary<int, T> lookup = new Dictionary<int, T>();

        foreach (T item in items)
        {
            if (item != null && item.Id != 0)
            {
                lookup[item.Id] = item;
            }
        }

        return lookup;
    }

    private void WarnAboutBadIds()
    {
        HashSet<int> seen = new HashSet<int>();

        CheckIds(weapons, seen);
        CheckIds(abilities, seen);
        CheckIds(passives, seen);
    }

    private void CheckIds(ItemData[] items, HashSet<int> seen)
    {
        if (items == null) return;

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            if (item.Id == 0)
            {
                Debug.LogWarning("Item has no permanent ID: " + item.name, item);
            }
            else if (!seen.Add(item.Id))
            {
                Debug.LogWarning("Duplicate item ID " + item.Id + " on " + item.name, item);
            }
        }
    }
}
