using System.Collections.Generic;
using UnityEngine;

public class RicochetAimLine : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = new Color(0.4f, 0.85f, 1f, 0.55f);
    [SerializeField] private float lineWidth = 0.06f;

    private readonly List<Vector3> points = new List<Vector3>();

    private LineRenderer line;

    void Awake()
    {
        GameObject lineObject = new GameObject("RicochetAimLine");

        lineObject.transform.SetParent(transform, false);
        line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = lineMaterial;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
    }

    void LateUpdate()
    {
        if (!weapon.TryGetRicochetPreview(points))
        {
            line.enabled = false;

            return;
        }

        line.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            line.SetPosition(i, points[i]);
        }

        line.enabled = true;
    }
}
