using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WeaponHoldSetupWindow : EditorWindow
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player.prefab";
    private const string BOT_PREFAB_PATH = "Assets/Prefabs/Bot.prefab";
    private const int HOLD_COUNT = 1;
    private const int MAX_GUNS = 4;

    private static readonly string[] HoldNames = { WeaponSetupUtility.HOLD_POINT_NAME };
    private const float PANEL_WIDTH = 380f;
    private const string SECTION_PREFIX = "WeaponHoldSetup.Section.";
    private const int WIRE_SEGMENTS = 48;
    private const float REAR_GAP = 0.1f;
    private const float TICK_SECONDS = 1f / 60f;

    private static readonly Vector3 shotOriginPoint = new Vector3(0f, 1.1f, 0.3f);
    private static float sphereDiameter;
    private static readonly string[] blasterNames =
    {
        "blaster-a", "blaster-b", "blaster-c", "blaster-d", "blaster-e", "blaster-f", "blaster-g", "blaster-h", "blaster-i",
        "blaster-j", "blaster-k", "blaster-l", "blaster-m", "blaster-n", "blaster-o", "blaster-p", "blaster-q", "blaster-r"
    };

    private PreviewRenderUtility preview;
    private GameObject rigRoot;
    private GameObject character;
    private readonly GameObject[] holdObjects = new GameObject[HOLD_COUNT];
    private AnimationClip[] clips = new AnimationClip[0];
    private string[] clipNames = new string[0];
    private readonly List<Transform> overlayBones = new List<Transform>();
    private readonly List<Vector3> overlayPositions = new List<Vector3>();
    private readonly List<Quaternion> overlayRotations = new List<Quaternion>();
    private int baseClipIndex;
    private int upperClipIndex;
    private bool useUpperOverlay = true;
    private bool isPlaying = true;
    private float animTime;
    private double lastTick;
    private readonly GameObject[] modelContainers = new GameObject[MAX_GUNS];
    private readonly Transform[] muzzleMarkers = new Transform[MAX_GUNS];
    private Material muzzleMaterial;
    private Material markerMaterial;
    private Material aimMaterial;
    private WeaponData[] weapons = new WeaponData[0];
    private string[] weaponNames = new string[0];
    private SerializedObject serialized;
    private Vector2 scroll;
    private readonly Vector3[] holdPositions = { new Vector3(0f, 1.05f, 0.7f) };
    private readonly Vector3[] holdEulers = new Vector3[HOLD_COUNT];
    private bool needsRebuild;
    private bool showBullet = true;
    private bool showIndicator = true;
    private bool showMuzzles = true;
    private bool autoFire;
    private bool showExplosionWire = true;
    private bool showHitWire;
    private readonly GameObject[] wireObjects = new GameObject[2];
    private readonly Mesh[] wireMeshes = new Mesh[2];
    private readonly List<Vector3> wireVertices = new List<Vector3>();
    private readonly List<int> wireIndices = new List<int>();
    private Material explosionWireMaterial;
    private Material hitWireMaterial;
    private float autoFireTimer;
    private int previewShotCounter;
    private double lastSimTime;
    private readonly List<PreviewShot> previewShots = new List<PreviewShot>();
    private readonly List<PreviewBlast> previewBlasts = new List<PreviewBlast>();
    private Material blastMaterial;
    private readonly GameObject[] bulletContainers = new GameObject[MAX_GUNS];
    private readonly GameObject[] bulletSources = new GameObject[MAX_GUNS];
    private readonly Vector3[] bulletSizes = new Vector3[MAX_GUNS];
    private readonly List<Vector3> indicatorVertices = new List<Vector3>();
    private readonly List<Color> indicatorColors = new List<Color>();
    private readonly List<int> indicatorTriangles = new List<int>();
    private GameObject indicatorObject;
    private Mesh indicatorMesh;
    private Material indicatorMaterial;
    private Material bulletMaterial;
    private int weaponIndex;
    private int blasterIndex;
    private float targetLength = 1.4f;
    private float yaw = 25f;
    private float pitch = 25f;
    private float distance = 6f;
    private Vector3 focus = new Vector3(0f, 1.1f, 0.6f);

    [MenuItem("KITBASH/Weapon Hold Setup")]
    public static void Open()
    {
        GetWindow<WeaponHoldSetupWindow>("Weapon Hold Setup").minSize = new Vector2(900f, 600f);
    }

    void OnEnable()
    {
        preview = new PreviewRenderUtility();
        preview.camera.fieldOfView = 30f;
        preview.camera.nearClipPlane = 0.05f;
        preview.camera.farClipPlane = 100f;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.23f, 0.23f, 0.28f);
        preview.ambientColor = new Color(0.55f, 0.55f, 0.6f);
        preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        preview.lights[0].intensity = 1.2f;

        markerMaterial = CreateUnlit(new Color(1f, 0.25f, 0.25f));
        aimMaterial = CreateUnlit(new Color(1f, 0.9f, 0.2f));
        muzzleMaterial = CreateUnlit(new Color(0.2f, 0.9f, 1f));
        bulletMaterial = CreateUnlit(new Color(1f, 0.9f, 0.4f));
        explosionWireMaterial = CreateUnlit(new Color(1f, 0.55f, 0.1f));
        hitWireMaterial = CreateUnlit(new Color(0.3f, 0.9f, 1f));
        wireMeshes[0] = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        wireMeshes[1] = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        blastMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave, color = new Color(1f, 0.5f, 0.15f, 0.45f) };
        lastSimTime = EditorApplication.timeSinceStartup;
        indicatorMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        indicatorMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };

        ReloadWeapons();
        ReloadClips();
        LoadHoldPoint();
        RebuildRig();

        lastTick = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        AssetDatabase.SaveAssets();

        if (rigRoot != null)
        {
            DestroyImmediate(rigRoot);
        }

        if (preview != null)
        {
            preview.Cleanup();
        }

        DestroyImmediate(markerMaterial);
        DestroyImmediate(aimMaterial);
        DestroyImmediate(muzzleMaterial);
        DestroyImmediate(bulletMaterial);
        DestroyImmediate(explosionWireMaterial);
        DestroyImmediate(hitWireMaterial);
        DestroyImmediate(wireMeshes[0]);
        DestroyImmediate(wireMeshes[1]);
        DestroyImmediate(blastMaterial);
        DestroyImmediate(indicatorMaterial);
        DestroyImmediate(indicatorMesh);
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawControls();

        Rect previewRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        DrawPreview(previewRect);
        EditorGUILayout.EndHorizontal();
    }

    private WeaponData CurrentWeapon => weapons.Length == 0 ? null : weapons[weaponIndex];

    private bool BeginSection(string title, bool defaultOpen)
    {
        bool isOpen = EditorPrefs.GetBool(SECTION_PREFIX + title, defaultOpen);
        bool newOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);

        if (newOpen != isOpen)
        {
            EditorPrefs.SetBool(SECTION_PREFIX + title, newOpen);
        }

        return newOpen;
    }

    private void EndSection()
    {
        EditorGUILayout.Space(2f);
    }

    private void Field(string propertyName, string label)
    {
        EditorGUILayout.PropertyField(serialized.FindProperty(propertyName), new GUIContent(label));
    }

    private void DrawModelSection()
    {
        EditorGUI.BeginChangeCheck();
        Field("modelPrefab", "Model");

        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            BuildModel();
        }

        EditorGUILayout.BeginHorizontal();
        blasterIndex = EditorGUILayout.Popup(blasterIndex, blasterNames);

        if (GUILayout.Button("Use this Kenney model", GUILayout.Width(150f)))
        {
            UseBlasterModel();
        }

        EditorGUILayout.EndHorizontal();
        Field("modelOffset", "Offset");
        Field("modelEuler", "Rotation");
        Field("modelScale", "Scale");
        Field("muzzleOffset", "Muzzle (model space)");
        DrawQuickButtons();
    }

    private void DrawGunsSection()
    {
        EditorGUILayout.HelpBox("Gun 1 is the Model section. Add more guns (for example the second pistol of akimbo) with Extra guns. Every gun adds a fire point at its own muzzle.", MessageType.None);
        EditorGUI.BeginChangeCheck();
        Field("hand", "Gun 1 hand");
        Field("mirrorModel", "Gun 1 mirrored");
        EditorGUILayout.PropertyField(serialized.FindProperty("extraGuns"), new GUIContent("Extra guns"), true);

        if (EditorGUI.EndChangeCheck())
        {
            needsRebuild = true;
        }

        EditorGUILayout.PropertyField(serialized.FindProperty("extraFirePoints"), new GUIContent("Extra fire points"), true);
        Field("firePattern", "Fire pattern");
    }

    private void DrawStatsSection()
    {
        Field("fireMode", "Fire mode");
        Field("damagePerPellet", "Damage per pellet");
        Field("pelletCount", "Pellets per shot");
        Field("fireInterval", "Fire interval (s)");
        Field("magazineSize", "Magazine size");
        Field("reloadTime", "Reload time, empty to full (s)");
        Field("regenDelay", "Reload delay after firing (s)");
        Field("spreadAngle", "Spread (degrees)");
        Field("range", "Range (m)");
        Field("falloffStart", "Damage falloff start (m)");
        Field("falloffEnd", "Damage falloff end (m)");
    }

    private void DrawHandlingSection()
    {
        Field("moveSpeedMultiplier", "Move speed multiplier");
        Field("damageTakenMultiplier", "Damage taken multiplier");
        Field("reloadSpeedMultiplier", "Reload speed multiplier");
        Field("spreadGrowthPerShot", "Spread growth per shot");
        Field("maxExtraSpread", "Max extra spread");
        Field("spreadRecoverPerSecond", "Spread recovery per second");
        Field("backstabAngle", "Backstab angle");
        Field("isMelee", "Melee");
    }

    private void DrawBulletSection()
    {
        Field("shotType", "Shot type");

        bool isThrown = serialized.FindProperty("shotType").enumValueIndex == (int)WeaponShotType.Thrown;

        if (isThrown)
        {
            Field("thrownPrefab", "Thrown prefab");
            Field("thrownScale", "Thrown scale");
            Field("throwArcHeight", "Throw arc height");
            Field("projectileSpeed", "Throw speed (m/s)");
        }
        else
        {
            Field("bulletPrefab", "Bullet prefab");
            Field("bulletScale", "Bullet scale (x prefab size)");
            Field("projectileSpeed", "Bullet speed (m/s)");
            Field("projectileRadius", "Bullet hit radius");
            Field("projectileLength", "Line length (no prefab)");
            Field("tracerWidth", "Line width (no prefab)");
            Field("tracerColor", "Bullet color (line fallback)");
            Field("explosive", "Explodes on impact");
        }

        if (isThrown || serialized.FindProperty("explosive").boolValue)
        {
            Field("throwRadius", "Explosion radius");
            Field("explosionPrefab", "Explosion prefab");
            Field("explosionScale", "Explosion scale");
        }
    }

    private void DrawIndicatorSection()
    {
        Field("indicatorShape", "Shape");
        Field("indicatorColor", "Color");
        Field("indicatorWidth", "Width (0 = bullet width)");
        Field("indicatorEndLength", "End marker length");
    }

    private void DrawHoldSection()
    {
        EditorGUI.BeginChangeCheck();
        holdPositions[0] = EditorGUILayout.Vector3Field("Position", holdPositions[0]);
        holdEulers[0] = EditorGUILayout.Vector3Field("Rotation", holdEulers[0]);

        if (EditorGUI.EndChangeCheck())
        {
            ApplyHoldTransform();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save hold point to prefabs"))
        {
            SaveHoldPoint();
        }

        if (GUILayout.Button("Reload from prefab"))
        {
            LoadHoldPoint();
            ApplyHoldTransform();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawViewSection()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Game camera"))
        {
            SetView(0f, 55f, 14f);
        }

        if (GUILayout.Button("Side"))
        {
            SetView(90f, 0f, 5f);
        }

        if (GUILayout.Button("Top"))
        {
            SetView(0f, 89f, 6f);
        }

        if (GUILayout.Button("Front"))
        {
            SetView(180f, 10f, 5f);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Red dot = hold point. Cyan dot = muzzle. Yellow line = where shots travel. The gun barrel should sit on the yellow line. Use the checkboxes at the top right of the preview to show the bullet and the aim indicator. Drag to orbit, scroll to zoom. Changes save to the weapon asset as you edit.", MessageType.None);
    }

    private void DrawControls()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(PANEL_WIDTH));

        if (weapons.Length == 0)
        {
            EditorGUILayout.HelpBox("No WeaponData assets found. Run KITBASH > Build Test Arena first.", MessageType.Info);

            if (GUILayout.Button("Reload"))
            {
                ReloadWeapons();
                RebuildRig();
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        int newIndex = EditorGUILayout.Popup("Weapon asset", weaponIndex, weaponNames);

        if (newIndex != weaponIndex)
        {
            weaponIndex = newIndex;
            serialized = new SerializedObject(CurrentWeapon);
            BuildModel();
        }

        serialized.Update();

        if (BeginSection("Model and muzzle", true))
        {
            DrawModelSection();
        }

        EndSection();

        if (BeginSection("Guns and fire points", false))
        {
            DrawGunsSection();
        }

        EndSection();

        if (BeginSection("Weapon stats", true))
        {
            DrawStatsSection();
        }

        EndSection();

        if (BeginSection("Bullet and explosion", true))
        {
            DrawBulletSection();
        }

        EndSection();

        if (BeginSection("Aim indicator", true))
        {
            DrawIndicatorSection();
        }

        EndSection();

        if (BeginSection("Handling", false))
        {
            DrawHandlingSection();
        }

        EndSection();

        serialized.ApplyModifiedProperties();

        if (needsRebuild)
        {
            needsRebuild = false;
            BuildModel();
        }

        if (BeginSection("Hold point (all weapons, player and bot)", false))
        {
            DrawHoldSection();
        }

        EndSection();

        if (BeginSection("Character animation (preview only)", false))
        {
            DrawAnimationControls();
        }

        EndSection();

        if (BeginSection("View", false))
        {
            DrawViewSection();
        }

        EndSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawAnimationControls()
    {
        if (character == null || clips.Length == 0)
        {
            EditorGUILayout.HelpBox("No animated character found. Run KITBASH > Build Test Arena first.", MessageType.Info);
            return;
        }

        baseClipIndex = EditorGUILayout.Popup("Full / lower body", baseClipIndex, clipNames);
        useUpperOverlay = EditorGUILayout.Toggle("Upper-body overlay", useUpperOverlay);

        using (new EditorGUI.DisabledScope(!useUpperOverlay))
        {
            upperClipIndex = EditorGUILayout.Popup("Upper body", upperClipIndex, clipNames);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Idle"))
        {
            ApplyPreset("idle", "holding-both");
        }

        if (GUILayout.Button("Walk"))
        {
            ApplyPreset("walk", "holding-both");
        }

        if (GUILayout.Button("Sprint"))
        {
            ApplyPreset("sprint", "holding-both");
        }

        if (GUILayout.Button("Shoot"))
        {
            ApplyPreset("idle", "holding-both-shoot");
        }

        if (GUILayout.Button("Die"))
        {
            ApplyPreset("die", null);
        }

        EditorGUILayout.EndHorizontal();

        isPlaying = GUILayout.Toggle(isPlaying, isPlaying ? "Pause" : "Play", "Button");

        if (!isPlaying)
        {
            animTime = EditorGUILayout.Slider("Time", animTime, 0f, Mathf.Max(0.01f, clips[baseClipIndex].length));
        }
    }

    private void ApplyPreset(string baseName, string upperName)
    {
        baseClipIndex = Mathf.Max(0, IndexOfClip(baseName));
        useUpperOverlay = upperName != null;
        animTime = 0f;

        if (upperName != null)
        {
            upperClipIndex = Mathf.Max(0, IndexOfClip(upperName));
        }
    }

    private int IndexOfClip(string clipName)
    {
        return System.Array.IndexOf(clipNames, clipName);
    }

    private void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float simDelta = (float)Mathf.Min(0.1f, (float)(now - lastSimTime));

        lastSimTime = now;

        if (IsSimulationActive())
        {
            TickSimulation(simDelta);
            Repaint();
        }

        if (!isPlaying || clips.Length == 0 || now - lastTick < TICK_SECONDS) return;

        animTime += (float)(now - lastTick);
        lastTick = now;
        Repaint();
    }

    private void ReloadClips()
    {
        clips = WeaponSetupUtility.LoadClips();
        clipNames = new string[clips.Length];

        for (int i = 0; i < clips.Length; i++)
        {
            clipNames[i] = clips[i].name;
        }

        baseClipIndex = Mathf.Max(0, IndexOfClip("idle"));
        upperClipIndex = Mathf.Max(0, IndexOfClip("holding-both"));
    }

    private void SampleAnimation()
    {
        if (character == null || clips.Length == 0) return;

        AnimationClip baseClip = clips[baseClipIndex];
        Transform torso = WeaponSetupUtility.FindDeep(character.transform, WeaponSetupUtility.TORSO_NAME);
        bool isOverlaying = useUpperOverlay && torso != null;

        if (isOverlaying)
        {
            clips[upperClipIndex].SampleAnimation(character, WrapTime(clips[upperClipIndex]));
            CaptureOverlay(torso);
        }

        baseClip.SampleAnimation(character, WrapTime(baseClip));

        if (isOverlaying)
        {
            ApplyOverlay();
        }
    }

    private float WrapTime(AnimationClip clip)
    {
        return clip.length > 0f ? Mathf.Repeat(animTime, clip.length) : 0f;
    }

    private void CaptureOverlay(Transform torso)
    {
        overlayBones.Clear();
        overlayPositions.Clear();
        overlayRotations.Clear();

        foreach (Transform bone in torso.GetComponentsInChildren<Transform>())
        {
            overlayBones.Add(bone);
            overlayPositions.Add(bone.localPosition);
            overlayRotations.Add(bone.localRotation);
        }
    }

    private void ApplyOverlay()
    {
        for (int i = 0; i < overlayBones.Count; i++)
        {
            overlayBones[i].localPosition = overlayPositions[i];
            overlayBones[i].localRotation = overlayRotations[i];
        }
    }

    private void DrawQuickButtons()
    {
        SerializedProperty euler = serialized.FindProperty("modelEuler");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Yaw -90"))
        {
            AddYaw(euler, -90f);
        }

        if (GUILayout.Button("Yaw +90"))
        {
            AddYaw(euler, 90f);
        }

        if (GUILayout.Button("Yaw 180"))
        {
            AddYaw(euler, 180f);
        }

        if (GUILayout.Button("Reset"))
        {
            ResetModel();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        targetLength = EditorGUILayout.FloatField("Target length", targetLength);

        if (GUILayout.Button("Auto orient + scale", GUILayout.Width(140f)))
        {
            AutoOrient();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Center on hold point"))
        {
            AlignModel(false);
        }

        if (GUILayout.Button("Rear at hold point"))
        {
            AlignModel(true);
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Muzzle at barrel tip"))
        {
            FindMuzzle();
        }
    }

    private void DrawPreview(Rect rect)
    {
        Event current = Event.current;
        Rect toggleBox = new Rect(rect.xMax - 176f, rect.y + 6f, 170f, 182f);
        bool isOverToggles = toggleBox.Contains(current.mousePosition);

        if (rect.Contains(current.mousePosition) && !isOverToggles)
        {
            if (current.type == EventType.MouseDrag)
            {
                yaw += current.delta.x * 0.5f;
                pitch = Mathf.Clamp(pitch + current.delta.y * 0.5f, -89f, 89f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance * (1f + current.delta.y * 0.05f), 1f, 40f);
                current.Use();
                Repaint();
            }
        }

        if (current.type == EventType.Repaint)
        {
            SampleAnimation();
            ApplyModelTransform();
            UpdateBullets();
            UpdateIndicator();
            UpdateWires();

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            preview.camera.transform.rotation = rotation;
            preview.camera.transform.position = focus - rotation * Vector3.forward * distance;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.Render();

            Texture result = preview.EndPreview();

            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
        }

        GUI.Box(toggleBox, GUIContent.none);
        showBullet = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 4f, 156f, 20f), showBullet, "Bullet at muzzle");
        showIndicator = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 26f, 156f, 20f), showIndicator, "Aim indicator");
        showMuzzles = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 48f, 156f, 20f), showMuzzles, "Muzzle dots");

        showExplosionWire = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 70f, 156f, 20f), showExplosionWire, "Explosion radius");
        showHitWire = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 92f, 156f, 20f), showHitWire, "Bullet hit radius");

        if (GUI.Button(new Rect(toggleBox.x + 8f, toggleBox.y + 120f, 154f, 24f), "Fire"))
        {
            FirePreview();
        }

        autoFire = GUI.Toggle(new Rect(toggleBox.x + 8f, toggleBox.y + 152f, 156f, 20f), autoFire, "Auto fire");

        if (GUI.changed)
        {
            Repaint();
        }
    }

    private void UpdateBullets()
    {
        WeaponData weapon = CurrentWeapon;

        for (int gun = 0; gun < MAX_GUNS; gun++)
        {
            bool isThrown = weapon != null && weapon.ShotType == WeaponShotType.Thrown;
            GameObject source = weapon == null ? null : isThrown ? weapon.ThrownPrefab : weapon.BulletPrefab;
            bool isWanted = showBullet && rigRoot != null && weapon != null && gun < weapon.GunCount && muzzleMarkers[gun] != null && (!isThrown || source != null);

            if (!isWanted)
            {
                if (bulletContainers[gun] != null)
                {
                    bulletContainers[gun].SetActive(false);
                }

                continue;
            }

            if (bulletContainers[gun] == null || bulletSources[gun] != source)
            {
                BuildBullet(gun, source);
            }

            Vector3 size = bulletSizes[gun];
            Vector3 scale;
            float length;

            if (isThrown)
            {
                scale = source.transform.localScale * weapon.ThrownScale;
                length = size.z * weapon.ThrownScale;
            }
            else if (source != null)
            {
                scale = source.transform.localScale * weapon.BulletScale;
                length = size.z * weapon.BulletScale;
            }
            else
            {
                scale = new Vector3(weapon.TracerWidth, weapon.TracerWidth, weapon.ProjectileLength) * weapon.BulletScale;
                length = weapon.ProjectileLength * weapon.BulletScale;
            }

            bulletMaterial.color = weapon.TracerColor;
            bulletContainers[gun].SetActive(true);
            bulletContainers[gun].transform.SetPositionAndRotation(muzzleMarkers[gun].position + Vector3.forward * (length * 0.5f), Quaternion.identity);
            bulletContainers[gun].transform.localScale = scale;
        }
    }

    private class PreviewShot
    {
        public GameObject Visual { get; set; }
        public bool IsThrown { get; set; }
        public Vector3 Origin { get; set; }
        public Vector3 Target { get; set; }
        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public float Length { get; set; }
        public float Elapsed { get; set; }
        public float Duration { get; set; }
        public bool IsExplosive { get; set; }
        public WeaponData Weapon { get; set; }
    }

    private class PreviewBlast
    {
        public GameObject Visual { get; set; }
        public float Age { get; set; }
        public float Radius { get; set; }
    }

    private void FirePreview()
    {
        WeaponData weapon = CurrentWeapon;

        if (weapon == null || rigRoot == null) return;

        bool isThrown = weapon.ShotType == WeaponShotType.Thrown;
        int pointMask = isThrown ? 1 : weapon.GetFirePointMask(previewShotCounter);

        previewShotCounter++;

        for (int point = 0; point < weapon.FirePointCount; point++)
        {
            if ((pointMask & (1 << point)) == 0) continue;

            int gun = Mathf.Min(weapon.GetFirePointGun(point), MAX_GUNS - 1);

            if (modelContainers[gun] == null) continue;

            Vector3 origin = modelContainers[gun].transform.TransformPoint(weapon.GetFirePointOffset(point));

            if (isThrown)
            {
                SpawnPreviewThrow(weapon, origin);

                break;
            }

            for (int pellet = 0; pellet < weapon.PelletCount; pellet++)
            {
                float yawOffset = weapon.GetFirePointAngle(point) + Random.Range(-weapon.SpreadAngle, weapon.SpreadAngle);

                SpawnPreviewBullet(weapon, origin, Quaternion.AngleAxis(yawOffset, Vector3.up) * Vector3.forward);
            }
        }
    }

    private void SpawnPreviewBullet(WeaponData weapon, Vector3 origin, Vector3 direction)
    {
        GameObject source = weapon.BulletPrefab;
        GameObject visual = CreateBulletObject(source, out Vector3 size);
        Vector3 scale = source != null ? source.transform.localScale * weapon.BulletScale : new Vector3(weapon.TracerWidth, weapon.TracerWidth, weapon.ProjectileLength) * weapon.BulletScale;
        float length = source != null ? size.z * weapon.BulletScale : weapon.ProjectileLength * weapon.BulletScale;

        visual.transform.localScale = scale;
        previewShots.Add(new PreviewShot
        {
            Visual = visual,
            Origin = origin,
            Direction = direction,
            Length = length,
            Speed = weapon.ProjectileSpeed,
            Duration = weapon.Range / Mathf.Max(0.1f, weapon.ProjectileSpeed),
            IsExplosive = weapon.Explosive,
            Weapon = weapon
        });
        PlacePreviewShot(previewShots[previewShots.Count - 1]);
    }

    private void SpawnPreviewThrow(WeaponData weapon, Vector3 origin)
    {
        GameObject source = weapon.ThrownPrefab != null ? weapon.ThrownPrefab : weapon.GetGunModel(0);
        GameObject visual = CreateBulletObject(source, out Vector3 size);
        float distanceForward = Mathf.Min(weapon.Range, 10f);
        Vector3 target = new Vector3(shotOriginPoint.x, 0f, shotOriginPoint.z + distanceForward);

        visual.transform.localScale = source.transform.localScale * (weapon.ThrownPrefab != null ? weapon.ThrownScale : weapon.GetGunScale(0));
        previewShots.Add(new PreviewShot
        {
            Visual = visual,
            IsThrown = true,
            Origin = origin,
            Target = target,
            Duration = Mathf.Max(0.3f, Vector3.Distance(new Vector3(origin.x, 0f, origin.z), target) / Mathf.Max(0.1f, weapon.ProjectileSpeed)),
            Weapon = weapon
        });
        PlacePreviewShot(previewShots[previewShots.Count - 1]);
    }

    private GameObject CreateBulletObject(GameObject source, out Vector3 size)
    {
        GameObject container = new GameObject("PreviewShot") { hideFlags = HideFlags.HideAndDontSave };

        container.transform.SetParent(rigRoot.transform, false);

        if (source == null)
        {
            AddMarker(container.transform, PrimitiveType.Cube, bulletMaterial, Vector3.zero, Vector3.one);
            size = Vector3.one;

            return container;
        }

        AddMeshCopies(source, container.transform, false);

        List<Vector3> points = WeaponSetupUtility.CollectModelPoints(source);
        Bounds bounds = new Bounds(points[0], Vector3.zero);

        foreach (Vector3 point in points)
        {
            bounds.Encapsulate(point);
        }

        size = bounds.size;

        return container;
    }

    private void PlacePreviewShot(PreviewShot shot)
    {
        float progress = Mathf.Clamp01(shot.Elapsed / shot.Duration);

        if (shot.IsThrown)
        {
            Vector3 position = Vector3.Lerp(shot.Origin, shot.Target, progress);

            position.y += 4f * shot.Weapon.ThrowArcHeight * progress * (1f - progress);
            shot.Visual.transform.position = position;
            shot.Visual.transform.rotation = Quaternion.Euler(540f * shot.Elapsed, 0f, 0f);

            return;
        }

        Vector3 head = shot.Origin + shot.Direction * (shot.Speed * shot.Elapsed);
        Vector3 center = head - shot.Direction * (shot.Length * 0.5f);

        shot.Visual.transform.SetPositionAndRotation(center, Quaternion.LookRotation(shot.Direction));
    }

    private void TickSimulation(float deltaTime)
    {
        autoFireTimer += deltaTime;

        WeaponData weapon = CurrentWeapon;

        if (autoFire && weapon != null && autoFireTimer >= Mathf.Max(0.12f, weapon.FireInterval))
        {
            autoFireTimer = 0f;
            FirePreview();
        }

        for (int i = previewShots.Count - 1; i >= 0; i--)
        {
            PreviewShot shot = previewShots[i];

            if (shot.Visual == null)
            {
                previewShots.RemoveAt(i);

                continue;
            }

            shot.Elapsed += deltaTime;
            PlacePreviewShot(shot);

            if (shot.Elapsed < shot.Duration) continue;

            if (shot.IsThrown || shot.IsExplosive)
            {
                SpawnBlast(shot.IsThrown ? shot.Target : shot.Origin + shot.Direction * shot.Weapon.Range, shot.Weapon.ThrowRadius);
            }

            DestroyImmediate(shot.Visual);
            previewShots.RemoveAt(i);
        }

        for (int i = previewBlasts.Count - 1; i >= 0; i--)
        {
            PreviewBlast blast = previewBlasts[i];

            if (blast.Visual == null)
            {
                previewBlasts.RemoveAt(i);

                continue;
            }

            blast.Age += deltaTime;
            blast.Visual.transform.localScale = Vector3.one * (blast.Radius * 2f / GetSphereDiameter() * Mathf.Clamp01(blast.Age / 0.12f));

            if (blast.Age < 0.45f) continue;

            DestroyImmediate(blast.Visual);
            previewBlasts.RemoveAt(i);
        }
    }

    private static float GetSphereDiameter()
    {
        if (sphereDiameter <= 0f)
        {
            sphereDiameter = Resources.GetBuiltinResource<Mesh>("Sphere.fbx").bounds.size.x;
        }

        return sphereDiameter;
    }

    private void SpawnBlast(Vector3 position, float radius)
    {
        GameObject visual = new GameObject("PreviewBlast") { hideFlags = HideFlags.HideAndDontSave };

        visual.transform.SetParent(rigRoot.transform, false);
        visual.transform.position = position;
        AddMarker(visual.transform, PrimitiveType.Sphere, blastMaterial, Vector3.zero, Vector3.one);
        previewBlasts.Add(new PreviewBlast { Visual = visual, Radius = radius });
    }

    private bool IsSimulationActive()
    {
        return autoFire || previewShots.Count > 0 || previewBlasts.Count > 0;
    }

    private void UpdateWires()
    {
        WeaponData weapon = CurrentWeapon;
        bool isThrown = weapon != null && weapon.ShotType == WeaponShotType.Thrown;
        bool hasExplosion = weapon != null && (isThrown || weapon.Explosive);
        float reach = weapon == null ? 0f : isThrown ? Mathf.Min(weapon.Range, 10f) : weapon.Range;
        Vector3 explosionCenter = new Vector3(shotOriginPoint.x, 0f, shotOriginPoint.z + reach);

        if (weapon != null && !isThrown && muzzleMarkers[0] != null)
        {
            explosionCenter = muzzleMarkers[0].position + Vector3.forward * weapon.Range;
        }

        UpdateWire(0, showExplosionWire && hasExplosion, explosionCenter, weapon == null ? 0f : weapon.ThrowRadius, explosionWireMaterial);

        bool hasBullet = weapon != null && !isThrown && muzzleMarkers[0] != null;
        Vector3 hitCenter = hasBullet ? muzzleMarkers[0].position + Vector3.forward * (weapon.ProjectileLength * 0.5f) : Vector3.zero;

        UpdateWire(1, showHitWire && hasBullet, hitCenter, hasBullet ? weapon.ProjectileRadius : 0f, hitWireMaterial);
    }

    private void UpdateWire(int index, bool isWanted, Vector3 center, float radius, Material material)
    {
        if (!isWanted || rigRoot == null)
        {
            if (wireObjects[index] != null)
            {
                wireObjects[index].SetActive(false);
            }

            return;
        }

        if (wireObjects[index] == null)
        {
            wireObjects[index] = new GameObject("Wire" + index) { hideFlags = HideFlags.HideAndDontSave };
            wireObjects[index].transform.SetParent(rigRoot.transform, false);
            wireObjects[index].AddComponent<MeshFilter>().sharedMesh = wireMeshes[index];
            wireObjects[index].AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        wireVertices.Clear();
        wireIndices.Clear();
        AddWireCircle(center, radius, Vector3.right, Vector3.forward);
        AddWireCircle(center, radius, Vector3.right, Vector3.up);
        AddWireCircle(center, radius, Vector3.forward, Vector3.up);

        Mesh mesh = wireMeshes[index];

        mesh.Clear();
        mesh.SetVertices(wireVertices);
        mesh.SetIndices(wireIndices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        wireObjects[index].SetActive(true);
    }

    private void AddWireCircle(Vector3 center, float radius, Vector3 axisA, Vector3 axisB)
    {
        int first = wireVertices.Count;

        for (int i = 0; i < WIRE_SEGMENTS; i++)
        {
            float angle = i / (float)WIRE_SEGMENTS * Mathf.PI * 2f;

            wireVertices.Add(center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius);
            wireIndices.Add(first + i);
            wireIndices.Add(first + (i + 1) % WIRE_SEGMENTS);
        }
    }

    private void BuildBullet(int gun, GameObject source)
    {
        if (bulletContainers[gun] != null)
        {
            DestroyImmediate(bulletContainers[gun]);
        }

        GameObject container = new GameObject("Bullet" + gun) { hideFlags = HideFlags.HideAndDontSave };

        container.transform.SetParent(rigRoot.transform, false);

        if (source != null)
        {
            AddMeshCopies(source, container.transform, false);

            List<Vector3> points = WeaponSetupUtility.CollectModelPoints(source);
            Bounds bounds = new Bounds(points[0], Vector3.zero);

            foreach (Vector3 point in points)
            {
                bounds.Encapsulate(point);
            }

            bulletSizes[gun] = bounds.size;
        }
        else
        {
            AddMarker(container.transform, PrimitiveType.Cube, bulletMaterial, Vector3.zero, Vector3.one);
            bulletSizes[gun] = Vector3.one;
        }

        bulletContainers[gun] = container;
        bulletSources[gun] = source;
    }

    private void UpdateIndicator()
    {
        WeaponData weapon = CurrentWeapon;
        bool isWanted = showIndicator && rigRoot != null && weapon != null && weapon.IndicatorShape != AimIndicatorShape.None;

        if (!isWanted)
        {
            if (indicatorObject != null)
            {
                indicatorObject.SetActive(false);
            }

            return;
        }

        if (indicatorObject == null)
        {
            indicatorObject = new GameObject("Indicator") { hideFlags = HideFlags.HideAndDontSave };
            indicatorObject.transform.SetParent(rigRoot.transform, false);
            indicatorObject.AddComponent<MeshFilter>().sharedMesh = indicatorMesh;

            MeshRenderer meshRenderer = indicatorObject.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial = indicatorMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float range = weapon.IndicatorShape == AimIndicatorShape.Circle ? Mathf.Min(weapon.Range, 10f) : weapon.Range;
        Vector3 origin = new Vector3(shotOriginPoint.x, 0f, shotOriginPoint.z);

        AimIndicatorGeometry.Build(indicatorVertices, indicatorColors, indicatorTriangles, weapon, origin, Vector3.forward, range, weapon.SpreadAngle);
        indicatorMesh.Clear();
        indicatorMesh.SetVertices(indicatorVertices);
        indicatorMesh.SetColors(indicatorColors);
        indicatorMesh.SetTriangles(indicatorTriangles, 0);
        indicatorMesh.RecalculateBounds();
        indicatorObject.SetActive(true);
    }

    private void SetView(float newYaw, float newPitch, float newDistance)
    {
        yaw = newYaw;
        pitch = newPitch;
        distance = newDistance;
    }

    private void ReloadWeapons()
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponData");
        List<WeaponData> found = new List<WeaponData>();

        foreach (string guid in guids)
        {
            found.Add(AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        weapons = found.ToArray();
        weaponNames = new string[weapons.Length];

        for (int i = 0; i < weapons.Length; i++)
        {
            weaponNames[i] = weapons[i].name;
        }

        weaponIndex = Mathf.Clamp(weaponIndex, 0, Mathf.Max(0, weapons.Length - 1));
        serialized = weapons.Length == 0 ? null : new SerializedObject(weapons[weaponIndex]);
    }

    private void LoadHoldPoint()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        for (int i = 0; i < HOLD_COUNT; i++)
        {
            Transform hold = prefab == null ? null : WeaponSetupUtility.FindDeep(prefab.transform, HoldNames[i]);

            if (hold == null) continue;

            holdPositions[i] = hold.localPosition;
            holdEulers[i] = hold.localEulerAngles;
        }
    }

    private void SaveHoldPoint()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        string report = "";

        foreach (string path in new[] { PLAYER_PREFAB_PATH, BOT_PREFAB_PATH })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            bool isOpenInStage = stage != null && stage.assetPath == path;
            GameObject contents = isOpenInStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            bool isSaved = false;

            for (int i = 0; i < HOLD_COUNT; i++)
            {
                Transform hold = WeaponSetupUtility.FindDeep(contents.transform, HoldNames[i]);

                if (hold == null) continue;

                hold.localPosition = holdPositions[i];
                hold.localRotation = Quaternion.Euler(holdEulers[i]);
                isSaved = true;
            }

            if (isSaved)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                report += System.IO.Path.GetFileNameWithoutExtension(path) + " ";
            }
            else
            {
                Debug.LogWarning("Hold point not found in " + path + ". Run KITBASH > Build Test Arena.");
            }

            if (!isOpenInStage)
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Hold points saved to: " + report);
    }

    private void UseBlasterModel()
    {
        WeaponData weapon = CurrentWeapon;
        GameObject model = WeaponSetupUtility.ReplaceWeaponModelPrefab(weapon.DisplayName, blasterNames[blasterIndex]);

        serialized.Update();
        serialized.FindProperty("modelPrefab").objectReferenceValue = model;
        serialized.ApplyModifiedProperties();
        AutoOrient();
        BuildModel();
    }

    private void AutoOrient()
    {
        WeaponData weapon = CurrentWeapon;

        if (weapon.ModelPrefab == null) return;

        WeaponSetupUtility.MeasureBarrel(WeaponSetupUtility.CollectModelPoints(weapon.ModelPrefab), out float length, out float barrelYaw);

        serialized.Update();
        serialized.FindProperty("modelScale").floatValue = targetLength / length;
        serialized.FindProperty("modelEuler").vector3Value = new Vector3(0f, barrelYaw, 0f);
        serialized.FindProperty("modelOffset").vector3Value = Vector3.zero;
        serialized.ApplyModifiedProperties();
    }

    private void AlignModel(bool isRearAtHold)
    {
        WeaponData weapon = CurrentWeapon;

        if (weapon.ModelPrefab == null) return;

        Transform root = weapon.ModelPrefab.transform;
        Quaternion rotation = Quaternion.Euler(weapon.ModelEuler) * root.localRotation;
        Vector3 scale = root.localScale * weapon.ModelScale;
        Bounds bounds = new Bounds();
        bool isFirst = true;

        foreach (Vector3 worldPoint in WeaponSetupUtility.CollectModelPoints(weapon.ModelPrefab))
        {
            Vector3 local = root.worldToLocalMatrix.MultiplyPoint3x4(worldPoint);
            Vector3 placed = rotation * Vector3.Scale(local, scale);

            if (isFirst)
            {
                bounds = new Bounds(placed, Vector3.zero);
                isFirst = false;
            }
            else
            {
                bounds.Encapsulate(placed);
            }
        }

        Vector3 offset = -bounds.center;

        if (isRearAtHold)
        {
            offset.z = -bounds.min.z + REAR_GAP;
        }

        serialized.Update();
        serialized.FindProperty("modelOffset").vector3Value = offset;
        serialized.ApplyModifiedProperties();
    }

    private void FindMuzzle()
    {
        WeaponData weapon = CurrentWeapon;

        if (weapon.ModelPrefab == null) return;

        serialized.Update();
        serialized.FindProperty("muzzleOffset").vector3Value = WeaponSetupUtility.FindMuzzleLocal(weapon.ModelPrefab, weapon.ModelEuler, weapon.ModelScale);
        serialized.ApplyModifiedProperties();
    }

    private void ResetModel()
    {
        serialized.Update();
        serialized.FindProperty("modelOffset").vector3Value = Vector3.zero;
        serialized.FindProperty("modelEuler").vector3Value = Vector3.zero;
        serialized.FindProperty("modelScale").floatValue = 1f;
        serialized.ApplyModifiedProperties();
    }

    private static void AddYaw(SerializedProperty euler, float amount)
    {
        Vector3 value = euler.vector3Value;

        value.y = Mathf.Repeat(value.y + amount, 360f);
        euler.vector3Value = value;
        euler.serializedObject.ApplyModifiedProperties();
    }

    private void RebuildRig()
    {
        if (rigRoot != null)
        {
            DestroyImmediate(rigRoot);
        }

        rigRoot = new GameObject("WeaponHoldRig") { hideFlags = HideFlags.HideAndDontSave };
        preview.AddSingleGO(rigRoot);

        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        Animator source = player == null ? null : player.GetComponentInChildren<Animator>();

        character = null;
        System.Array.Clear(holdObjects, 0, HOLD_COUNT);

        if (source != null)
        {
            character = Instantiate(source.gameObject, rigRoot.transform);
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.GetComponent<Animator>().enabled = false;
            SetHideFlags(character.transform);

            for (int i = 0; i < HOLD_COUNT; i++)
            {
                Transform hold = WeaponSetupUtility.FindDeep(character.transform, HoldNames[i]);

                holdObjects[i] = hold == null ? null : hold.gameObject;
            }
        }

        if (holdObjects[0] == null)
        {
            holdObjects[0] = new GameObject("Hold") { hideFlags = HideFlags.HideAndDontSave };
            holdObjects[0].transform.SetParent(rigRoot.transform, false);
        }

        for (int i = 0; i < HOLD_COUNT; i++)
        {
            if (holdObjects[i] != null)
            {
                AddMarker(holdObjects[i].transform, PrimitiveType.Sphere, markerMaterial, Vector3.zero, Vector3.one * 0.07f);
            }
        }
        AddMarker(rigRoot.transform, PrimitiveType.Cube, aimMaterial, shotOriginPoint + Vector3.forward * 3f, new Vector3(0.02f, 0.02f, 6f));
        ApplyHoldTransform();
        BuildModel();
    }

    private void BuildModel()
    {
        for (int gun = 0; gun < MAX_GUNS; gun++)
        {
            if (modelContainers[gun] != null)
            {
                DestroyImmediate(modelContainers[gun]);
                modelContainers[gun] = null;
            }
        }

        WeaponData weapon = CurrentWeapon;

        if (weapon == null || holdObjects[0] == null) return;

        int gunCount = Mathf.Min(weapon.GunCount, MAX_GUNS);

        for (int gun = 0; gun < gunCount; gun++)
        {
            GameObject prefab = weapon.GetGunModel(gun);

            if (prefab == null) continue;

            GameObject container = new GameObject("Model" + gun) { hideFlags = HideFlags.HideAndDontSave };

            container.transform.SetParent(GetHoldObject().transform, false);
            AddMeshCopies(prefab, container.transform, false);
            AddMarker(container.transform, PrimitiveType.Sphere, muzzleMaterial, Vector3.zero, Vector3.one);
            modelContainers[gun] = container;
            muzzleMarkers[gun] = container.transform.GetChild(container.transform.childCount - 1);
        }
    }

    private GameObject GetHoldObject()
    {
        return holdObjects[0];
    }

    private void ApplyHoldTransform()
    {
        for (int i = 0; i < HOLD_COUNT; i++)
        {
            if (holdObjects[i] == null) continue;

            holdObjects[i].transform.localPosition = holdPositions[i];
            holdObjects[i].transform.localRotation = Quaternion.Euler(holdEulers[i]);
        }
    }

    private void ApplyModelTransform()
    {
        WeaponData weapon = CurrentWeapon;

        if (weapon == null) return;

        for (int gun = 0; gun < MAX_GUNS; gun++)
        {
            GameObject prefab = gun < weapon.GunCount ? weapon.GetGunModel(gun) : null;

            if (modelContainers[gun] == null || prefab == null) continue;

            Transform root = prefab.transform;
            Vector3 scale = root.localScale * weapon.GetGunScale(gun);

            if (weapon.GetGunMirror(gun))
            {
                scale.x = -scale.x;
            }

            modelContainers[gun].transform.localPosition = weapon.GetGunOffset(gun);
            modelContainers[gun].transform.localRotation = Quaternion.Euler(weapon.GetGunEuler(gun)) * root.localRotation;
            modelContainers[gun].transform.localScale = scale;

            if (muzzleMarkers[gun] != null)
            {
                muzzleMarkers[gun].gameObject.SetActive(showMuzzles);
                muzzleMarkers[gun].localPosition = weapon.GetGunMuzzle(gun);
                muzzleMarkers[gun].localScale = Vector3.one * (0.05f / Mathf.Max(0.0001f, root.localScale.x * weapon.GetGunScale(gun)));
            }
        }
    }

    private static void SetHideFlags(Transform root)
    {
        root.gameObject.hideFlags = HideFlags.HideAndDontSave;

        foreach (Transform child in root)
        {
            SetHideFlags(child);
        }
    }

    private static void AddMeshCopies(GameObject source, Transform parent, bool isCharacter)
    {
        Matrix4x4 rootToLocal = source.transform.worldToLocalMatrix;

        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();

            if (sourceRenderer == null || filter.sharedMesh == null || (isCharacter && IsHiddenInPreview(filter.transform))) continue;

            Matrix4x4 matrix = rootToLocal * filter.transform.localToWorldMatrix;
            GameObject copy = new GameObject(filter.name) { hideFlags = HideFlags.HideAndDontSave };

            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = matrix.GetColumn(3);
            copy.transform.localRotation = matrix.rotation;
            copy.transform.localScale = matrix.lossyScale;
            copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            copy.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
        }
    }

    private static bool IsHiddenInPreview(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.name == "HealthBar" || current.name == "ShieldBubble") return true;
        }

        return false;
    }

    private static void AddMarker(Transform parent, PrimitiveType type, Material material, Vector3 localPosition, Vector3 localScale)
    {
        GameObject marker = new GameObject("Marker") { hideFlags = HideFlags.HideAndDontSave };
        string meshName = type == PrimitiveType.Sphere ? "Sphere.fbx" : "Cube.fbx";

        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = localScale;
        marker.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(meshName);
        marker.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Material CreateUnlit(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };

        material.color = color;

        return material;
    }
}
