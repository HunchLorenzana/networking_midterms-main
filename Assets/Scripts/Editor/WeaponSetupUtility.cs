using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WeaponSetupUtility
{
    public const string HOLD_POINT_NAME = "HoldPoint";
    public const string TORSO_NAME = "torso";

    private const string CHARACTER_MODEL_PATH = "Assets/Plugins/Kenney Blocky Characters/Models/FBX format/character-a.fbx";
    private const string CLIP_FOLDER = "Assets/Art/Animation/Clips";
    private const string MATERIAL_FOLDER = "Assets/Art/Materials/Misc";
    public const string WEAPON_MODEL_FOLDER = "Assets/Prefabs/Weapons";
    private const string BLASTER_MODEL_FOLDER = "Assets/Plugins/Kenney Blaster Kit/Models/FBX format";
    private const string BLASTER_TEXTURE_PATH = BLASTER_MODEL_FOLDER + "/Textures/colormap.png";
    private const string LIT_SHADER = "Universal Render Pipeline/Lit";

    public static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName) return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, childName);

            if (found != null) return found;
        }

        return null;
    }

    public static AnimationClip[] LoadClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CHARACTER_MODEL_PATH))
        {
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;

            AnimationClip editable = AssetDatabase.LoadAssetAtPath<AnimationClip>(CLIP_FOLDER + "/" + clip.name + ".anim");

            clips.Add(editable != null ? editable : clip);
        }

        return clips.ToArray();
    }

    public static GameObject ReplaceWeaponModelPrefab(string weaponName, string blasterName)
    {
        AssetDatabase.DeleteAsset(WEAPON_MODEL_FOLDER + "/Weapon_" + weaponName + ".prefab");

        return GetWeaponModelPrefab(weaponName, blasterName);
    }

    public static GameObject GetWeaponModelPrefab(string weaponName, string blasterName)
    {
        string path = WEAPON_MODEL_FOLDER + "/Weapon_" + weaponName + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing != null) return existing;

        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Weapons");

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BLASTER_MODEL_FOLDER + "/" + blasterName + ".fbx");

        return SaveWeaponPrefab("Weapon_" + weaponName, source, path);
    }

    public static GameObject SaveWeaponPrefab(string prefabName, GameObject source, string path)
    {
        Material material = GetBlasterMaterial();
        GameObject root = new GameObject(prefabName);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);

        model.name = "Model";

        foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>())
        {
            modelRenderer.sharedMaterial = material;
            modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved;
    }

    public static GameObject SaveModelPrefab(string prefabName, string blasterModelName, string folderName, Vector3 modelEuler)
    {
        return SaveModelPrefab(prefabName, blasterModelName, folderName, modelEuler, Vector3.one);
    }

    public static GameObject LoadBlasterModel(string blasterModelName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(BLASTER_MODEL_FOLDER + "/" + blasterModelName + ".fbx");
    }

    public static GameObject SaveModelPrefab(string prefabName, string blasterModelName, string folderName, Vector3 modelEuler, Vector3 rootScale)
    {
        string path = "Assets/Prefabs/" + folderName + "/" + prefabName + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing != null) return existing;

        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", folderName);

        Material material = GetBlasterMaterial();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BLASTER_MODEL_FOLDER + "/" + blasterModelName + ".fbx");
        GameObject root = new GameObject(prefabName);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);

        root.transform.localScale = rootScale;
        model.name = "Model";
        model.transform.localRotation = Quaternion.Euler(modelEuler);

        foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>())
        {
            modelRenderer.sharedMaterial = material;
            modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved;
    }

    public static Vector3 FindMuzzleLocal(GameObject modelPrefab, Vector3 euler, float scale)
    {
        Transform root = modelPrefab.transform;
        Quaternion rotation = Quaternion.Euler(euler) * root.localRotation;
        Vector3 scaleVector = root.localScale * scale;
        List<Vector3> locals = new List<Vector3>();
        List<float> forwards = new List<float>();
        float maxForward = float.MinValue;
        float minForward = float.MaxValue;

        foreach (Vector3 worldPoint in CollectModelPoints(modelPrefab))
        {
            Vector3 local = root.worldToLocalMatrix.MultiplyPoint3x4(worldPoint);
            float forward = (rotation * Vector3.Scale(local, scaleVector)).z;

            locals.Add(local);
            forwards.Add(forward);
            maxForward = Mathf.Max(maxForward, forward);
            minForward = Mathf.Min(minForward, forward);
        }

        float tipRange = (maxForward - minForward) * 0.05f;
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < locals.Count; i++)
        {
            if (forwards[i] <= maxForward - tipRange) continue;

            sum += locals[i];
            count++;
        }

        return sum / count;
    }

    public static List<Vector3> CollectModelPoints(GameObject root)
    {
        List<Vector3> points = new List<Vector3>();

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            Matrix4x4 matrix = filter.transform.localToWorldMatrix;

            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                points.Add(matrix.MultiplyPoint3x4(vertex));
            }
        }

        return points;
    }

    public static void MeasureBarrel(List<Vector3> points, out float length, out float yaw)
    {
        Bounds bounds = new Bounds(points[0], Vector3.zero);

        foreach (Vector3 point in points)
        {
            bounds.Encapsulate(point);
        }

        bool isAlongZ = bounds.size.z >= bounds.size.x;
        float min = isAlongZ ? bounds.min.z : bounds.min.x;
        float max = isAlongZ ? bounds.max.z : bounds.max.x;
        float span = max - min;
        float lowMinY = float.MaxValue;
        float lowMaxY = float.MinValue;
        float highMinY = float.MaxValue;
        float highMaxY = float.MinValue;

        foreach (Vector3 point in points)
        {
            float along = isAlongZ ? point.z : point.x;

            if (along < min + span * 0.2f)
            {
                lowMinY = Mathf.Min(lowMinY, point.y);
                lowMaxY = Mathf.Max(lowMaxY, point.y);
            }

            if (along > max - span * 0.2f)
            {
                highMinY = Mathf.Min(highMinY, point.y);
                highMaxY = Mathf.Max(highMaxY, point.y);
            }
        }

        float lowHeight = lowMaxY - lowMinY;
        float highHeight = highMaxY - highMinY;
        float centerAlong = isAlongZ ? bounds.center.z : bounds.center.x;
        bool isClear = Mathf.Abs(lowHeight - highHeight) > bounds.size.y * 0.05f;
        bool isBarrelPositive = isClear ? highHeight < lowHeight : centerAlong >= 0f;

        length = span;
        yaw = isAlongZ ? (isBarrelPositive ? 0f : 180f) : (isBarrelPositive ? -90f : 90f);
    }

    private static Material GetBlasterMaterial()
    {
        string path = MATERIAL_FOLDER + "/KenneyBlasters.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null) return material;

        string[] found = AssetDatabase.FindAssets("KenneyBlasters t:Material");

        if (found.Length > 0) return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(found[0]));

        material = new Material(Shader.Find(LIT_SHADER));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BLASTER_TEXTURE_PATH));
        material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, path);

        return material;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (AssetDatabase.IsValidFolder(parent + "/" + folderName)) return;

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
