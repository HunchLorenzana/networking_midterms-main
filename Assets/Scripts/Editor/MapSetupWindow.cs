using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Map counterpart of the Weapon Hold Setup window: edit a map's definition, open its scene,
// lay out spawn points, center the map and run checks, with spawn gizmos in the Scene view
public class MapSetupWindow : EditorWindow
{
    private const string SECTION_PREFIX = "KitbashMapSetup.";
    private const string GIZMOS_PREF = SECTION_PREFIX + "ShowGizmos";
    private const string ENVIRONMENT_ROOT = "--- ENVIRONMENT ---";
    private const string GAMEPLAY_ROOT = "--- GAMEPLAY ---";
    private const int RECOMMENDED_SPAWNS = 10;
    private const float GROUND_RAY_HEIGHT = 30f;
    private const float GROUND_RAY_LENGTH = 60f;
    private const float BODY_CHECK_HEIGHT = 1f;
    private const float BODY_CHECK_RADIUS = 0.4f;
    private const float GIZMO_RADIUS = 0.6f;
    private const string PREVIEW_FOLDER = "Assets/Art/Sprites/MapPreviews";
    private const int PREVIEW_WIDTH = 1280;
    private const int PREVIEW_HEIGHT = 720;
    private const float PREVIEW_FOV = 35f;
    private const float PREVIEW_MARGIN = 1.05f;
    private const float MIN_PREVIEW_ZOOM = 0.5f;
    private const float MAX_PREVIEW_ZOOM = 4f;

    private enum PreviewAngle
    {
        TopDown,
        Angled
    }

    private static readonly Color spawnColor = new Color(0.3f, 0.8f, 1f);
    private static readonly Color practiceSpawnColor = new Color(0.4f, 1f, 0.4f);
    private static readonly Color dummyColor = new Color(1f, 0.6f, 0.2f);

    private readonly List<MapDefinition> maps = new List<MapDefinition>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly Dictionary<string, bool> sectionStates = new Dictionary<string, bool>();

    private MapCatalog catalog;
    private int mapIndex;
    private float circleRadius = 10f;
    private Vector2 scroll;

    // Looked up only when something changes: searching assets and scenes on every GUI event made the window lag
    private string cachedScenePath;
    private bool isSceneLoaded;
    private MapMatchHandler cachedHandler;
    private PracticeArea cachedPractice;
    private bool isChecksDirty = true;
    private int missingBuildScenes;
    private bool showGizmos = true;
    private PreviewAngle previewAngle = PreviewAngle.Angled;
    private float previewPitch = 55f;
    private float previewZoom = 1f;

    [MenuItem("KITBASH/Map Setup")]
    public static void Open()
    {
        GetWindow<MapSetupWindow>("Map Setup");
    }

    void OnEnable()
    {
        showGizmos = EditorPrefs.GetBool(GIZMOS_PREF, true);
        RefreshMaps();
        RefreshCache();
        SceneView.duringSceneGui += DrawSceneGizmos;
        EditorApplication.hierarchyChanged += RefreshSceneObjects;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorSceneManager.sceneClosed += HandleSceneClosed;
        Undo.undoRedoPerformed += RefreshSceneObjects;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneGizmos;
        EditorApplication.hierarchyChanged -= RefreshSceneObjects;
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneClosed -= HandleSceneClosed;
        Undo.undoRedoPerformed -= RefreshSceneObjects;
    }

    void OnFocus()
    {
        RefreshMaps();
        RefreshCache();
    }

    private void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RefreshCache();
    }

    private void HandleSceneClosed(Scene scene)
    {
        RefreshCache();
    }

    // The scene path needs an asset search, so it is refreshed on map, focus and definition changes only
    private void RefreshCache()
    {
        MapDefinition map = CurrentMap;

        cachedScenePath = map != null ? FindScenePath(map.SceneName) : null;
        missingBuildScenes = CountMissingBuildScenes();
        RefreshSceneObjects();
    }

    private void RefreshSceneObjects()
    {
        MapDefinition map = CurrentMap;

        cachedHandler = null;
        cachedPractice = null;

        Scene scene = map != null ? SceneManager.GetSceneByName(map.SceneName) : default;

        isSceneLoaded = scene.IsValid() && scene.isLoaded;

        if (isSceneLoaded)
        {
            cachedHandler = FindInScene<MapMatchHandler>(scene);
            cachedPractice = FindInScene<PracticeArea>(scene);
        }

        isChecksDirty = true;
        Repaint();
        SceneView.RepaintAll();
    }

    private MapDefinition CurrentMap => maps.Count == 0 ? null : maps[Mathf.Clamp(mapIndex, 0, maps.Count - 1)];

    private bool IsPracticeMap => catalog != null && CurrentMap == catalog.PracticeMap;

    void OnGUI()
    {
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("No MapCatalog at " + MapsSetup.CATALOG_PATH + ".", MessageType.Warning);

            if (GUILayout.Button("Create Map Catalog"))
            {
                catalog = MapsSetup.GetOrCreateCatalog();
                RefreshMaps();
            }

            return;
        }

        DrawToolbar();

        MapDefinition map = CurrentMap;

        if (map == null)
        {
            EditorGUILayout.HelpBox("The catalog has no maps. Use New Map...", MessageType.Info);

            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (BeginSection("Definition", true))
        {
            DrawDefinition(map);
        }

        if (BeginSection("Scene", true))
        {
            DrawSceneSection();
        }

        if (BeginSection("Layout", true))
        {
            if (isSceneLoaded)
            {
                DrawLayout();
            }
            else
            {
                EditorGUILayout.HelpBox("Open the map scene to edit its layout.", MessageType.Info);
            }
        }

        if (BeginSection("Checks", true))
        {
            DrawChecks(map);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        string[] names = new string[maps.Count];

        for (int i = 0; i < maps.Count; i++)
        {
            names[i] = maps[i].DisplayName + " (" + maps[i].Id + ")" + (maps[i] == catalog.PracticeMap ? " - practice" : "");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int newIndex = EditorGUILayout.Popup(mapIndex, names);

            if (newIndex != mapIndex)
            {
                mapIndex = newIndex;
                RefreshCache();
            }

            if (GUILayout.Button("New Map...", GUILayout.Width(90f)))
            {
                CreateMapWindow.Open();
            }

            if (GUILayout.Button("Catalog", GUILayout.Width(70f)))
            {
                Selection.activeObject = catalog;
            }
        }

        bool newShowGizmos = EditorGUILayout.Toggle("Show spawn gizmos", showGizmos);

        if (newShowGizmos != showGizmos)
        {
            showGizmos = newShowGizmos;
            EditorPrefs.SetBool(GIZMOS_PREF, showGizmos);
            SceneView.RepaintAll();
        }
    }

    private void DrawDefinition(MapDefinition map)
    {
        SerializedObject serialized = new SerializedObject(map);

        serialized.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serialized.FindProperty("id"), new GUIContent("ID (permanent)"));
        }

        EditorGUILayout.PropertyField(serialized.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serialized.FindProperty("sceneName"));
        EditorGUILayout.PropertyField(serialized.FindProperty("supportedModes"), true);
        EditorGUILayout.PropertyField(serialized.FindProperty("previewImage"));

        using (new EditorGUILayout.HorizontalScope())
        {
            previewAngle = (PreviewAngle)EditorGUILayout.EnumPopup("Capture angle", previewAngle);

            using (new EditorGUI.DisabledScope(!isSceneLoaded))
            {
                if (GUILayout.Button(new GUIContent("Capture Preview", isSceneLoaded ? "Renders the open map scene and assigns the picture as the preview" : "Open the map scene first"), GUILayout.Width(120f)))
                {
                    serialized.ApplyModifiedProperties();
                    CapturePreview(map, SceneManager.GetSceneByName(map.SceneName));
                    serialized.Update();
                }
            }
        }

        if (previewAngle == PreviewAngle.Angled)
        {
            previewPitch = EditorGUILayout.Slider("Capture pitch", previewPitch, 30f, 89f);
        }

        previewZoom = EditorGUILayout.Slider(new GUIContent("Capture zoom", "1 fits the whole map; higher zooms in on the center"), previewZoom, MIN_PREVIEW_ZOOM, MAX_PREVIEW_ZOOM);
        EditorGUILayout.PropertyField(serialized.FindProperty("isInRotation"));

        if (serialized.ApplyModifiedProperties())
        {
            RefreshCache();
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawSceneSection()
    {
        string path = cachedScenePath;

        EditorGUILayout.LabelField("Path", path ?? "(scene not found)");
        EditorGUILayout.LabelField("Loaded", isSceneLoaded ? "Yes" : "No");

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(path == null))
            {
                if (GUILayout.Button("Open Scene") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }

                if (path != null && !IsInBuildSettings(path) && GUILayout.Button("Add To Build Settings"))
                {
                    MapsSetup.AddToBuildSettings(path);
                    isChecksDirty = true;
                }
            }
        }

        if (missingBuildScenes > 0 && GUILayout.Button("Remove " + missingBuildScenes + " Missing Scene(s) From Build Settings"))
        {
            RemoveMissingBuildScenes();
        }

        EditorGUILayout.Space(6f);

        if (GUILayout.Button(new GUIContent("Delete Map...", "Removes this map from the catalog and Build Settings and moves its scene, definition, baked data and captured preview to the recycle bin")))
        {
            DeleteMap(CurrentMap);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space(2f);
    }

    // Shared assets the map uses (props, prefabs, materials) are kept: other maps may use them.
    // Files go to the system recycle bin, so a mistake can be undone there
    private void DeleteMap(MapDefinition map)
    {
        // Read now: once the definition is in the recycle bin the object is destroyed
        string mapLabel = map.DisplayName + " (" + map.name + ")";
        string definitionPath = AssetDatabase.GetAssetPath(map);
        string scenePath = cachedScenePath;
        string bakedDataPath = scenePath != null ? scenePath.Substring(0, scenePath.Length - ".unity".Length) : null;
        string previewPath = map.PreviewImage != null ? AssetDatabase.GetAssetPath(map.PreviewImage) : null;
        bool isPracticeMap = map == catalog.PracticeMap;
        List<string> toTrash = new List<string> { definitionPath };

        if (scenePath != null)
        {
            toTrash.Add(scenePath);
        }

        if (bakedDataPath != null && AssetDatabase.IsValidFolder(bakedDataPath))
        {
            toTrash.Add(bakedDataPath);
        }

        // Only a picture made by Capture Preview belongs to the map. A sprite picked by hand may be used elsewhere
        if (previewPath != null && previewPath.StartsWith(PREVIEW_FOLDER + "/"))
        {
            toTrash.Add(previewPath);
        }

        string message = "Delete \"" + map.DisplayName + "\" (ID " + map.Id + ")?\n\n"
            + "Removed from the Map Catalog" + (isPracticeMap ? " (it is the PRACTICE map: Practice will stop working until another map is set)" : "") + " and from Build Settings.\n\n"
            + "Moved to the recycle bin:\n- " + string.Join("\n- ", toTrash) + "\n\n"
            + "Props, prefabs and materials the map uses are kept.";

        if (!EditorUtility.DisplayDialog("Delete Map", message, "Delete", "Cancel")) return;

        Scene scene = SceneManager.GetSceneByName(map.SceneName);

        if (scene.IsValid() && scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }

        RemoveFromCatalog(map);

        List<string> failed = new List<string>();

        AssetDatabase.MoveAssetsToTrash(toTrash.ToArray(), failed);
        RemoveMissingBuildScenes();

        if (failed.Count > 0)
        {
            Debug.LogWarning("Could not move these to the recycle bin: " + string.Join(", ", failed));
        }

        Debug.Log("Map " + mapLabel + " deleted.");
        mapIndex = 0;
        RefreshMaps();
        RefreshCache();
    }

    // Drops Build Settings entries whose scene file no longer exists, and saves the change to the project settings
    private void RemoveMissingBuildScenes()
    {
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        buildScenes.RemoveAll(buildScene => !System.IO.File.Exists(buildScene.path));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        missingBuildScenes = 0;
    }

    private static int CountMissingBuildScenes()
    {
        int count = 0;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!System.IO.File.Exists(buildScene.path))
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveFromCatalog(MapDefinition map)
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty maps = serialized.FindProperty("maps");
        SerializedProperty practiceMap = serialized.FindProperty("practiceMap");
        List<Object> kept = new List<Object>();

        for (int i = 0; i < maps.arraySize; i++)
        {
            Object entry = maps.GetArrayElementAtIndex(i).objectReferenceValue;

            if (entry != map)
            {
                kept.Add(entry);
            }
        }

        maps.arraySize = kept.Count;

        for (int i = 0; i < kept.Count; i++)
        {
            maps.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
        }

        if (practiceMap.objectReferenceValue == map)
        {
            practiceMap.objectReferenceValue = null;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private void DrawLayout()
    {
        Scene scene = SceneManager.GetSceneByName(CurrentMap.SceneName);
        MapMatchHandler handler = cachedHandler;
        PracticeArea practice = cachedPractice;

        if (GUILayout.Button(new GUIContent("Center Map On Origin", "Moves everything under the scene's root folders so the environment is centered on (0, 0); the folders stay at the origin")))
        {
            CenterOnOrigin(scene);
            isChecksDirty = true;
        }

        EditorGUILayout.Space(4f);

        if (handler == null)
        {
            EditorGUILayout.HelpBox("No MapMatchHandler in this scene.", MessageType.Error);

            if (GUILayout.Button("Add MapMatchHandler"))
            {
                AddHandler(scene);
            }

            return;
        }

        SerializedObject serialized = new SerializedObject(handler);
        SerializedProperty spawns = serialized.FindProperty("_spawnPoints");

        EditorGUILayout.LabelField("Spawn points", spawns.arraySize + " (recommended " + RECOMMENDED_SPAWNS + ")", EditorStyles.boldLabel);
        DrawPointList(spawns, "S");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add At View Center"))
            {
                AddSpawnPoint(handler, serialized, spawns);
                isChecksDirty = true;
            }

            if (GUILayout.Button("Snap All To Ground"))
            {
                SnapToGround(handler.SpawnPoints);
                isChecksDirty = true;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            circleRadius = EditorGUILayout.FloatField("Circle radius", circleRadius);

            if (GUILayout.Button("Arrange In Circle", GUILayout.Width(130f)))
            {
                ArrangeInCircle(handler.SpawnPoints, circleRadius);
                isChecksDirty = true;
            }
        }

        EditorGUILayout.PropertyField(serialized.FindProperty("_trainController"));
        if (serialized.ApplyModifiedProperties())
        {
            isChecksDirty = true;
        }

        if (practice == null) return;

        SerializedObject practiceSerialized = new SerializedObject(practice);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Practice area", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Practice spawn points", EditorStyles.miniBoldLabel);
        DrawPointList(practiceSerialized.FindProperty("spawnPoints"), "P");
        EditorGUILayout.LabelField("Training dummy points", EditorStyles.miniBoldLabel);
        DrawPointList(practiceSerialized.FindProperty("trainingDummyPoints"), "D");
        EditorGUILayout.LabelField("Walking dummy points (pairs)", EditorStyles.miniBoldLabel);
        DrawPointList(practiceSerialized.FindProperty("walkingDummyPoints"), "W");

        if (GUILayout.Button("Snap Practice Points To Ground"))
        {
            SnapToGround(practice.SpawnPoints);
            SnapToGround(practice.TrainingDummyPoints);
            SnapToGround(practice.WalkingDummyPoints);
            isChecksDirty = true;
        }

        if (practiceSerialized.ApplyModifiedProperties())
        {
            isChecksDirty = true;
        }
    }

    private static void DrawPointList(SerializedProperty points, string prefix)
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                SerializedProperty element = points.GetArrayElementAtIndex(i);

                EditorGUILayout.LabelField(prefix + (i + 1), GUILayout.Width(36f));
                EditorGUILayout.PropertyField(element, GUIContent.none);

                using (new EditorGUI.DisabledScope(element.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        Selection.activeObject = element.objectReferenceValue;
                        if (SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }
                    }
                }
            }
        }
    }

    // Checks (raycasts, asset searches) run only after a change or on request, and only in the Layout event so Layout and Repaint draw the same boxes
    private void DrawChecks(MapDefinition map)
    {
        if (GUILayout.Button("Run Checks"))
        {
            isChecksDirty = true;
        }

        if (isChecksDirty && Event.current.type == EventType.Layout)
        {
            RunChecks(map);
            isChecksDirty = false;
        }

        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        foreach (string warning in warnings)
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        if (errors.Count == 0 && warnings.Count == 0)
        {
            EditorGUILayout.HelpBox(isSceneLoaded ? "No problems found." : "Definition looks fine. Open the scene to check its layout too.", MessageType.Info);
        }
    }

    private void RunChecks(MapDefinition map)
    {
        errors.Clear();
        warnings.Clear();

        string path = cachedScenePath;

        if (map.Id == 0)
        {
            errors.Add("The map has no ID.");
        }

        foreach (MapDefinition other in maps)
        {
            if (other != map && other.Id == map.Id && map.Id != 0)
            {
                errors.Add("ID " + map.Id + " is also used by " + other.name + ".");
            }
        }

        if (string.IsNullOrEmpty(map.DisplayName))
        {
            warnings.Add("The map has no display name.");
        }

        if (path == null)
        {
            errors.Add("No scene named \"" + map.SceneName + "\" exists.");
        }
        else if (!IsInBuildSettings(path))
        {
            errors.Add("The scene is not in Build Settings, so it cannot load in play.");
        }

        if (!IsPracticeMap && !map.Supports(GameMode.FreeForAll) && !map.Supports(GameMode.TeamDeathmatch))
        {
            errors.Add("The map supports no game mode.");
        }

        if (map.PreviewImage == null)
        {
            warnings.Add("No preview image: the loading screen shows only the name.");
        }

        if (!IsPracticeMap && !map.IsInRotation)
        {
            warnings.Add("Not in rotation: matches never pick this map.");
        }

        if (!isSceneLoaded) return;

        MapMatchHandler handler = cachedHandler;
        PracticeArea practice = cachedPractice;

        if (handler == null)
        {
            errors.Add("The scene has no MapMatchHandler: the match never starts.");

            return;
        }

        Transform[] spawns = handler.SpawnPoints;

        if (spawns == null || spawns.Length == 0)
        {
            errors.Add("The MapMatchHandler has no spawn points.");
        }
        else if (spawns.Length < RECOMMENDED_SPAWNS)
        {
            warnings.Add("Only " + spawns.Length + " spawn points; " + RECOMMENDED_SPAWNS + " are recommended.");
        }

        CheckPoints(spawns, "Spawn point");

        if (IsPracticeMap && practice == null)
        {
            errors.Add("The practice map has no PracticeArea (practice spawns and dummies).");
        }

        if (!IsPracticeMap && practice != null)
        {
            warnings.Add("A match map has a PracticeArea: players would wait there and dummies would spawn in matches.");
        }

        if (practice != null)
        {
            CheckPoints(practice.SpawnPoints, "Practice spawn point");

            if (practice.WalkingDummyPoints.Length % 2 != 0)
            {
                warnings.Add("Walking dummy points should come in pairs (A and B).");
            }
        }
    }

    private void CheckPoints(Transform[] points, string label)
    {
        if (points == null) return;

        Physics.SyncTransforms();

        for (int i = 0; i < points.Length; i++)
        {
            Transform point = points[i];

            if (point == null)
            {
                errors.Add(label + " " + (i + 1) + " is empty.");

                continue;
            }

            if (!Physics.Raycast(point.position + Vector3.up * GROUND_RAY_HEIGHT, Vector3.down, GROUND_RAY_LENGTH, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                warnings.Add(label + " " + (i + 1) + " (" + point.name + ") has no ground below it.");
            }
            else if (Physics.CheckSphere(point.position + Vector3.up * BODY_CHECK_HEIGHT, BODY_CHECK_RADIUS, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                warnings.Add(label + " " + (i + 1) + " (" + point.name + ") is inside a wall or prop.");
            }
        }
    }

    private static bool TryGetEnvironmentBounds(Scene scene, out Bounds bounds)
    {
        bounds = new Bounds();

        bool hasBounds = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != ENVIRONMENT_ROOT) continue;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds;
    }

    // Renders the environment with a temporary camera, saves a PNG sprite and assigns it as the map's preview
    private void CapturePreview(MapDefinition map, Scene scene)
    {
        if (!TryGetEnvironmentBounds(scene, out Bounds bounds))
        {
            Debug.LogWarning("Nothing under " + ENVIRONMENT_ROOT + " to capture.");

            return;
        }

        GameObject cameraObject = new GameObject("MapPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = cameraObject.AddComponent<Camera>();
        RenderTexture target = new RenderTexture(PREVIEW_WIDTH, PREVIEW_HEIGHT, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D image = new Texture2D(PREVIEW_WIDTH, PREVIEW_HEIGHT, TextureFormat.RGB24, false);
        float aspect = PREVIEW_WIDTH / (float)PREVIEW_HEIGHT;
        float radius = bounds.extents.magnitude;

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.targetTexture = target;
        camera.aspect = aspect;

        if (previewAngle == PreviewAngle.TopDown)
        {
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / aspect) * PREVIEW_MARGIN / previewZoom;
            cameraObject.transform.SetPositionAndRotation(bounds.center + Vector3.up * (bounds.extents.y + radius), Quaternion.Euler(90f, 0f, 0f));
        }
        else
        {
            camera.fieldOfView = PREVIEW_FOV;

            float distance = radius * PREVIEW_MARGIN / Mathf.Sin(PREVIEW_FOV * 0.5f * Mathf.Deg2Rad) / previewZoom;
            Quaternion rotation = Quaternion.Euler(previewPitch, 0f, 0f);

            cameraObject.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
        }

        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = radius * 6f + 100f;
        camera.Render();

        RenderTexture previous = RenderTexture.active;

        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, PREVIEW_WIDTH, PREVIEW_HEIGHT), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        MapsSetup.EnsureFolder("Assets/Art", "Sprites");
        MapsSetup.EnsureFolder("Assets/Art/Sprites", "MapPreviews");

        string path = PREVIEW_FOLDER + "/Spr_MapPreview_" + map.name + ".png";

        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        DestroyImmediate(cameraObject);
        DestroyImmediate(target);
        DestroyImmediate(image);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        SerializedObject serialized = new SerializedObject(map);

        serialized.FindProperty("previewImage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        serialized.ApplyModifiedProperties();
        isChecksDirty = true;
        Debug.Log("Map preview saved to " + path + " and assigned to " + map.name + ".");
    }

    private static void CenterOnOrigin(Scene scene)
    {
        if (!TryGetEnvironmentBounds(scene, out Bounds bounds))
        {
            Debug.LogWarning("Nothing under " + ENVIRONMENT_ROOT + " to center.");

            return;
        }

        Vector3 offset = new Vector3(-bounds.center.x, 0f, -bounds.center.z);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.transform)
            {
                Undo.RecordObject(child, "Center map");
                child.position += offset;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Map moved by " + offset + " so its environment is centered on the origin. Save the scene.");
    }

    private static void AddHandler(Scene scene)
    {
        GameObject handlerObject = new GameObject("MapHandler");

        Undo.RegisterCreatedObjectUndo(handlerObject, "Add map handler");
        SceneManager.MoveGameObjectToScene(handlerObject, scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == GAMEPLAY_ROOT)
            {
                handlerObject.transform.SetParent(root.transform, false);
            }
        }

        Undo.AddComponent<MapMatchHandler>(handlerObject);
        Selection.activeObject = handlerObject;
    }

    private static void AddSpawnPoint(MapMatchHandler handler, SerializedObject serialized, SerializedProperty spawns)
    {
        Transform parent = handler.transform;

        if (spawns.arraySize > 0 && spawns.GetArrayElementAtIndex(0).objectReferenceValue is Transform first)
        {
            parent = first.parent;
        }

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : handler.transform.position;
        GameObject point = new GameObject("Spawn_" + (spawns.arraySize + 1));

        Undo.RegisterCreatedObjectUndo(point, "Add spawn point");
        point.transform.SetParent(parent, false);
        point.transform.position = new Vector3(position.x, 0f, position.z);
        SnapToGround(new[] { point.transform });
        spawns.arraySize++;
        spawns.GetArrayElementAtIndex(spawns.arraySize - 1).objectReferenceValue = point.transform;
        serialized.ApplyModifiedProperties();
        Selection.activeObject = point;
    }

    private static void SnapToGround(Transform[] points)
    {
        Physics.SyncTransforms();

        foreach (Transform point in points)
        {
            if (point == null) continue;

            if (!Physics.Raycast(point.position + Vector3.up * GROUND_RAY_HEIGHT, Vector3.down, out RaycastHit hit, GROUND_RAY_LENGTH, Physics.AllLayers, QueryTriggerInteraction.Ignore)) continue;

            Undo.RecordObject(point, "Snap to ground");
            point.position = new Vector3(point.position.x, hit.point.y, point.position.z);
        }
    }

    // Spreads the points evenly around their current middle, each facing the center
    private static void ArrangeInCircle(Transform[] points, float radius)
    {
        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (Transform point in points)
        {
            if (point == null) continue;

            center += point.position;
            count++;
        }

        if (count == 0) return;

        center /= count;

        int index = 0;

        foreach (Transform point in points)
        {
            if (point == null) continue;

            float angle = index * Mathf.PI * 2f / count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            Undo.RecordObject(point, "Arrange spawn points");
            point.position = center + offset;
            point.rotation = Quaternion.LookRotation(-offset.normalized);
            index++;
        }

        SnapToGround(points);
    }

    private void DrawSceneGizmos(SceneView view)
    {
        if (!showGizmos || !isSceneLoaded) return;

        MapMatchHandler handler = cachedHandler;
        PracticeArea practice = cachedPractice;

        if (handler != null)
        {
            DrawPoints(handler.SpawnPoints, spawnColor, "S");
        }

        if (practice == null) return;

        DrawPoints(practice.SpawnPoints, practiceSpawnColor, "P");
        DrawPoints(practice.TrainingDummyPoints, dummyColor, "D");
        DrawPoints(practice.WalkingDummyPoints, dummyColor, "W");

        Transform[] walkers = practice.WalkingDummyPoints;

        for (int i = 0; i + 1 < walkers.Length; i += 2)
        {
            if (walkers[i] != null && walkers[i + 1] != null)
            {
                Handles.DrawDottedLine(walkers[i].position, walkers[i + 1].position, 4f);
            }
        }
    }

    private static void DrawPoints(Transform[] points, Color color, string prefix)
    {
        if (points == null) return;

        Handles.color = color;

        for (int i = 0; i < points.Length; i++)
        {
            Transform point = points[i];

            if (point == null) continue;

            Handles.DrawWireDisc(point.position, Vector3.up, GIZMO_RADIUS);
            Handles.ArrowHandleCap(0, point.position, Quaternion.LookRotation(point.forward), GIZMO_RADIUS * 1.6f, EventType.Repaint);
            Handles.Label(point.position + Vector3.up * 1.2f, prefix + (i + 1), EditorStyles.boldLabel);
        }
    }

    // Called after Create New Map so the open Map Setup window jumps to the new map
    public static void SelectMapIfOpen(MapDefinition map)
    {
        if (!HasOpenInstances<MapSetupWindow>()) return;

        MapSetupWindow window = GetWindow<MapSetupWindow>("Map Setup");

        window.RefreshMaps();
        window.mapIndex = Mathf.Max(0, window.maps.IndexOf(map));
        window.RefreshCache();
    }

    private void RefreshMaps()
    {
        catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(MapsSetup.CATALOG_PATH);
        maps.Clear();

        if (catalog == null) return;

        foreach (MapDefinition map in catalog.Maps)
        {
            if (map != null)
            {
                maps.Add(map);
            }
        }

        if (catalog.PracticeMap != null && !maps.Contains(catalog.PracticeMap))
        {
            maps.Add(catalog.PracticeMap);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);

            if (found != null) return found;
        }

        return null;
    }

    private static string FindScenePath(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;

        foreach (string guid in AssetDatabase.FindAssets(sceneName + " t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }

        return null;
    }

    private static bool IsInBuildSettings(string path)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == path && scene.enabled) return true;
        }

        return false;
    }

    // Open states are kept in memory. EditorPrefs is read once per section and written only on change
    private bool BeginSection(string title, bool defaultOpen)
    {
        if (!sectionStates.TryGetValue(title, out bool isOpen))
        {
            isOpen = EditorPrefs.GetBool(SECTION_PREFIX + title, defaultOpen);
            sectionStates[title] = isOpen;
        }

        bool newOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);

        if (newOpen != isOpen)
        {
            sectionStates[title] = newOpen;
            EditorPrefs.SetBool(SECTION_PREFIX + title, newOpen);
        }

        return newOpen;
    }
}
