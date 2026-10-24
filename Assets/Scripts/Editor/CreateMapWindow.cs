using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates a ready-to-build map: scene with the standard roots, a ground, a sun, 10 spawn points and a MapMatchHandler,
// plus its MapDefinition (next free ID, out of rotation) in the MapCatalog and Build Settings
public class CreateMapWindow : EditorWindow
{
    private const int SPAWN_POINT_COUNT = 10;
    private const float SPAWN_RADIUS = 10f;
    private const float GROUND_SCALE = 4f;

    private string mapName = "";

    [MenuItem("KITBASH/Maps/Create New Map...")]
    public static void Open()
    {
        CreateMapWindow window = GetWindow<CreateMapWindow>(true, "Create New Map");

        window.minSize = new Vector2(380f, 120f);
    }

    void OnGUI()
    {
        mapName = EditorGUILayout.TextField("Map name", mapName);

        string key = ToKey(mapName);

        EditorGUILayout.LabelField("Scene", key.Length > 0 ? "Map" + key : "-");

        using (new EditorGUI.DisabledScope(key.Length == 0))
        {
            if (GUILayout.Button("Create"))
            {
                Create(mapName.Trim(), key);
            }
        }
    }

    private void Create(string displayName, string key)
    {
        string sceneName = "Map" + key;
        string scenePath = MapsSetup.MAP_SCENE_FOLDER + "/" + sceneName + ".unity";

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
        {
            EditorUtility.DisplayDialog("Create New Map", scenePath + " already exists.", "OK");

            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        MapsSetup.EnsureFolder("Assets/Scenes", "Maps");
        MapsSetup.EnsureFolder("Assets/Data", "Maps");
        MapsSetup.EnsureFolder("Assets/Data", "Catalogs");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Transform environment = MapsSetup.CreateRoot(scene, "--- ENVIRONMENT ---");
        Transform gameplay = MapsSetup.CreateRoot(scene, "--- GAMEPLAY ---");
        Transform lighting = MapsSetup.CreateRoot(scene, "--- LIGHTING ---");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GameObject sun = new GameObject("Sun", typeof(Light));
        Transform spawnRoot = new GameObject("SpawnPoints").transform;
        Transform[] spawnPoints = new Transform[SPAWN_POINT_COUNT];

        ground.name = "Ground";
        ground.transform.SetParent(environment, false);
        ground.transform.localScale = Vector3.one * GROUND_SCALE;
        sun.transform.SetParent(lighting, false);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.GetComponent<Light>().type = LightType.Directional;
        spawnRoot.SetParent(gameplay, false);

        for (int i = 0; i < SPAWN_POINT_COUNT; i++)
        {
            float angle = i * Mathf.PI * 2f / SPAWN_POINT_COUNT;
            Transform point = new GameObject("Spawn_" + (i + 1)).transform;

            point.SetParent(spawnRoot, false);
            point.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SPAWN_RADIUS;
            point.rotation = Quaternion.LookRotation(-point.localPosition.normalized);
            spawnPoints[i] = point;
        }

        GameObject handlerObject = new GameObject("MapHandler");
        MapMatchHandler handler = handlerObject.AddComponent<MapMatchHandler>();
        SerializedObject serialized = new SerializedObject(handler);
        SerializedProperty spawns = serialized.FindProperty("_spawnPoints");

        handlerObject.transform.SetParent(gameplay, false);
        spawns.arraySize = spawnPoints.Length;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spawns.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, scenePath);

        MapDefinition map = MapsSetup.CreateMapDefinition(key, MapsSetup.GetNextMapId(), displayName, sceneName, false);

        MapsSetup.AddToCatalog(MapsSetup.GetOrCreateCatalog(), map);
        MapsSetup.AddToBuildSettings(scenePath);
        Selection.activeObject = map;
        MapSetupWindow.SelectMapIfOpen(map);
        Debug.Log("Map created: " + scenePath + " with MapDefinition " + map.name + " (ID " + map.Id + ", not in rotation yet). Build the map, then tick In Rotation on its MapDefinition.");
        Close();
    }

    private static string ToKey(string name)
    {
        System.Text.StringBuilder key = new System.Text.StringBuilder();
        bool isWordStart = true;

        foreach (char character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                key.Append(isWordStart ? char.ToUpperInvariant(character) : character);
                isWordStart = false;
            }
            else
            {
                isWordStart = true;
            }
        }

        string result = key.ToString();

        // "Map" is added to the scene name already, so a typed "Map Foundry" must not become MapMapFoundry
        if (result.Length > 3 && result.StartsWith("Map") && char.IsUpper(result[3]))
        {
            result = result.Substring(3);
        }

        return result;
    }
}
