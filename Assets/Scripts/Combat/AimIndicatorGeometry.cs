using System.Collections.Generic;
using UnityEngine;

public static class AimIndicatorGeometry
{
    private const int ARC_POINTS = 14;
    private const float GROUND_LIFT = 0.05f;
    private const float MIN_CONE_ANGLE = 2f;
    private const float CAP_ALPHA_MULTIPLIER = 1.6f;
    private const int CIRCLE_POINTS = 32;
    private const int ARC_SEGMENTS = 16;
    private const float TRAIL_WIDTH = 0.15f;

    public static void Build(List<Vector3> vertices, List<Color> colors, List<int> triangles, WeaponData weapon, Vector3 origin, Vector3 direction, float range, float halfAngle)
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();
        origin.y += GROUND_LIFT;
        direction.y = 0f;
        direction.Normalize();

        Color color = weapon.IndicatorColor;

        if (weapon.IndicatorShape == AimIndicatorShape.Circle)
        {
            AddThrow(vertices, colors, triangles, origin, direction, range, weapon.ThrowRadius, weapon.ThrowArcHeight, color);
        }
        else if (weapon.IndicatorShape == AimIndicatorShape.Cone && halfAngle >= MIN_CONE_ANGLE)
        {
            AddCone(vertices, colors, triangles, origin, direction, range, halfAngle, color);
        }
        else
        {
            AddLine(vertices, colors, triangles, origin, direction, range, weapon.IndicatorWidth, weapon.IndicatorEndLength, color);
        }
    }

    private static void AddLine(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 origin, Vector3 direction, float range, float width, float endLength, Color color)
    {
        AddQuad(vertices, colors, triangles, origin, origin + direction * range, direction, width, color);

        if (endLength <= 0f) return;

        Color capColor = color;
        float capLength = Mathf.Min(endLength, range);

        capColor.a = Mathf.Clamp01(color.a * CAP_ALPHA_MULTIPLIER);
        AddQuad(vertices, colors, triangles, origin + direction * (range - capLength), origin + direction * range, direction, width, capColor);
    }

    private static void AddQuad(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 start, Vector3 end, Vector3 direction, float width, Color color)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (width * 0.5f);
        int first = vertices.Count;

        vertices.Add(start - side);
        vertices.Add(start + side);
        vertices.Add(end + side);
        vertices.Add(end - side);

        for (int i = 0; i < 4; i++)
        {
            colors.Add(color);
        }

        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 1);
        triangles.Add(first);
        triangles.Add(first + 3);
        triangles.Add(first + 2);
    }

    private static void AddThrow(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 origin, Vector3 direction, float distance, float radius, float arcHeight, Color color)
    {
        Vector3 target = origin + direction * distance;
        Color trailColor = color;

        trailColor.a = color.a * 0.8f;
        AddArc(vertices, colors, triangles, origin, target, direction, arcHeight, trailColor);

        int center = vertices.Count;

        vertices.Add(target);
        colors.Add(color);

        for (int i = 0; i < CIRCLE_POINTS; i++)
        {
            float angle = i / (float)CIRCLE_POINTS * Mathf.PI * 2f;

            vertices.Add(target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            colors.Add(color);
        }

        for (int i = 0; i < CIRCLE_POINTS; i++)
        {
            triangles.Add(center);
            triangles.Add(center + 1 + (i + 1) % CIRCLE_POINTS);
            triangles.Add(center + 1 + i);
        }
    }

    private static void AddArc(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 origin, Vector3 target, Vector3 direction, float arcHeight, Color color)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (TRAIL_WIDTH * 0.5f);
        int first = vertices.Count;

        for (int i = 0; i <= ARC_SEGMENTS; i++)
        {
            float t = i / (float)ARC_SEGMENTS;
            Vector3 point = Vector3.Lerp(origin, target, t);

            point.y += 4f * arcHeight * t * (1f - t);
            vertices.Add(point - side);
            vertices.Add(point + side);
            colors.Add(color);
            colors.Add(color);
        }

        for (int i = 0; i < ARC_SEGMENTS; i++)
        {
            int a = first + i * 2;

            triangles.Add(a);
            triangles.Add(a + 2);
            triangles.Add(a + 1);
            triangles.Add(a + 1);
            triangles.Add(a + 2);
            triangles.Add(a + 3);
        }
    }

    private static void AddCone(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 origin, Vector3 direction, float range, float halfAngle, Color color)
    {
        int first = vertices.Count;

        vertices.Add(origin);
        colors.Add(color);

        for (int i = 0; i < ARC_POINTS; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(ARC_POINTS - 1));

            vertices.Add(origin + Quaternion.AngleAxis(angle, Vector3.up) * direction * range);
            colors.Add(color);
        }

        for (int i = 0; i < ARC_POINTS - 1; i++)
        {
            triangles.Add(first);
            triangles.Add(first + i + 2);
            triangles.Add(first + i + 1);
        }
    }
}
