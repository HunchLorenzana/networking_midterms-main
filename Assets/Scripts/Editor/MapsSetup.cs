using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shared helpers for the map tools (Create New Map, Map Setup)
public static class MapsSetup
{
    public const string MAP_SCENE_FOLDER = "Assets/Scenes/Maps";
    public const string MAP_DATA_FOLDER = "Assets/Data/Maps";
    public const string CATALOG_FOLDER = "Assets/Data/Catalogs";
    public const string CATALOG_PATH = CATALOG_FOLDER + "/MapCatalog.asset";
    public const int FIRST_MAP_ID = 4001;

    public static MapDefinition CreateMapDefinition(string assetName, int id, string displayName, string sceneName, bool isInRotation)
    {
        string path = MAP_DATA_FOLDER + "/" + assetName + ".asset";
        MapDefinition map = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);

        if (map == null)
        {
            map = ScriptableObject.CreateInstance<MapDefinition>();
            AssetDatabase.CreateAsset(map, path);
        }

        SerializedObject serialized = new SerializedObject(map);

        serialized.FindProperty("id").intValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("sceneName").stringValue = sceneName;
        serialized.FindProperty("isInRotation").boolValue = isInRotation;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        return map;
    }

    public static MapCatalog GetOrCreateCatalog()
    {
        MapCatalog catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_PATH);

        if (catalog != null) return catalog;

        catalog = ScriptableObject.CreateInstance<MapCatalog>();
        AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        AssetDatabase.SaveAssets();

        return catalog;
    }

    public static void AddToCatalog(MapCatalog catalog, MapDefinition map)
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty maps = serialized.FindProperty("maps");

        for (int i = 0; i < maps.arraySize; i++)
        {
            if (maps.GetArrayElementAtIndex(i).objectReferenceValue == map) return;
        }

        maps.arraySize++;
        maps.GetArrayElementAtIndex(maps.arraySize - 1).objectReferenceValue = map;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    public static int GetNextMapId()
    {
        int highest = FIRST_MAP_ID - 1;

        foreach (string guid in AssetDatabase.FindAssets("t:MapDefinition"))
        {
            MapDefinition map = AssetDatabase.LoadAssetAtPath<MapDefinition>(AssetDatabase.GUIDToAssetPath(guid));

            highest = Mathf.Max(highest, map.Id);
        }

        return highest + 1;
    }

    public static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public static Transform CreateRoot(Scene scene, string name)
    {
        GameObject root = new GameObject(name);

        SceneManager.MoveGameObjectToScene(root, scene);

        return root.transform;
    }

    public static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        if (scenes.Exists(scene => scene.path == path)) return;

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
