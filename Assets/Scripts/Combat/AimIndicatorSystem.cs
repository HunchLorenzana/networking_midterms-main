using System.Collections.Generic;
using UnityEngine;

public class AimIndicatorSystem : MonoBehaviour
{
    private readonly Mesh[] meshes = new Mesh[PlayerWeapon.SLOT_COUNT];
    private readonly MeshRenderer[] renderers = new MeshRenderer[PlayerWeapon.SLOT_COUNT];
    private readonly List<Vector3> vertexList = new List<Vector3>();
    private readonly List<Color> colorList = new List<Color>();
    private readonly List<int> triangleList = new List<int>();

    private static AimIndicatorSystem instance;

    public static void Initialize()
    {
        if (instance != null) return;

        GameObject systemObject = new GameObject("AimIndicatorSystem");

        systemObject.transform.SetParent(RuntimeRoot.Instance.transform, false);
        systemObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        systemObject.AddComponent<AimIndicatorSystem>().Build();
    }

    public static void Show(int slot, Vector3 origin, Vector3 direction, WeaponData weapon, float range, float halfAngle)
    {
        if (instance == null) return;

        instance.Draw(slot, origin, direction, weapon, range, halfAngle);
    }

    public static void Hide(int slot)
    {
        if (instance == null) return;

        instance.renderers[slot].enabled = false;
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Build()
    {
        Material material = new Material(Shader.Find("Sprites/Default"));

        for (int slot = 0; slot < PlayerWeapon.SLOT_COUNT; slot++)
        {
            GameObject meshObject = new GameObject("Indicator" + slot);
            MeshFilter filter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();

            meshObject.transform.SetParent(transform, false);
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
            meshes[slot] = mesh;
            renderers[slot] = meshRenderer;
        }
    }

    private void Draw(int slot, Vector3 origin, Vector3 direction, WeaponData weapon, float range, float halfAngle)
    {
        if (weapon.IndicatorShape == AimIndicatorShape.None)
        {
            Hide(slot);

            return;
        }

        AimIndicatorGeometry.Build(vertexList, colorList, triangleList, weapon, origin, direction, range, halfAngle);

        Mesh mesh = meshes[slot];

        mesh.Clear();
        mesh.SetVertices(vertexList);
        mesh.SetColors(colorList);
        mesh.SetTriangles(triangleList, 0);
        mesh.RecalculateBounds();
        renderers[slot].enabled = true;
    }
}
