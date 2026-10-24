using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds in-memory data assets for tests and destroys them afterwards. Private serialized fields are set through SerializedObject
public class TestData
{
    private readonly List<Object> created = new List<Object>();

    public T Create<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();

        created.Add(asset);

        return asset;
    }

    public WeaponData Weapon(int id, bool isStarter, float maxHealthMultiplier = 1f, float moveSpeedMultiplier = 1f)
    {
        WeaponData weapon = Item<WeaponData>(id, isStarter);

        Set(weapon, "maxHealthMultiplier", maxHealthMultiplier);
        Set(weapon, "moveSpeedMultiplier", moveSpeedMultiplier);

        return weapon;
    }

    public AbilityData Ability(int id, bool isStarter)
    {
        return Item<AbilityData>(id, isStarter);
    }

    public PassiveData Passive(int id, bool isStarter, float maxHealthPercent = 0f, float moveSpeedPercent = 0f)
    {
        PassiveData passive = Item<PassiveData>(id, isStarter);

        Set(passive, "maxHealthPercent", maxHealthPercent);
        Set(passive, "moveSpeedPercent", moveSpeedPercent);

        return passive;
    }

    public LoadoutCatalog Catalog(WeaponData[] weapons, AbilityData[] abilities, PassiveData[] passives)
    {
        LoadoutCatalog catalog = Create<LoadoutCatalog>();

        SetArray(catalog, "weapons", weapons);
        SetArray(catalog, "abilities", abilities);
        SetArray(catalog, "passives", passives);

        return catalog;
    }

    public MapDefinition Map(int id, bool isInRotation, params GameMode[] modes)
    {
        MapDefinition map = Create<MapDefinition>();
        SerializedObject serialized = new SerializedObject(map);
        SerializedProperty modeArray = serialized.FindProperty("supportedModes");

        serialized.FindProperty("id").intValue = id;
        serialized.FindProperty("sceneName").stringValue = "Map" + id;
        serialized.FindProperty("isInRotation").boolValue = isInRotation;
        modeArray.arraySize = modes.Length;

        for (int i = 0; i < modes.Length; i++)
        {
            modeArray.GetArrayElementAtIndex(i).enumValueIndex = (int)modes[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        return map;
    }

    public MapCatalog MapCatalog(params MapDefinition[] maps)
    {
        MapCatalog catalog = Create<MapCatalog>();

        SetArray(catalog, "maps", maps);

        return catalog;
    }

    public void DestroyAll()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    private T Item<T>(int id, bool isStarter) where T : ItemData
    {
        T item = Create<T>();
        SerializedObject serialized = new SerializedObject(item);

        serialized.FindProperty("id").intValue = id;
        serialized.FindProperty("displayName").stringValue = typeof(T).Name + " " + id;
        serialized.FindProperty("isStarter").boolValue = isStarter;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static void Set(Object target, string field, float value)
    {
        SerializedObject serialized = new SerializedObject(target);

        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
