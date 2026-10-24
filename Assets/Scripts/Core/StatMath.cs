using UnityEngine;

public static class StatMath
{
    public static float SumModifiers(float first, float second, float min, float max)
    {
        return Mathf.Clamp(1f + (first - 1f) + (second - 1f), min, max);
    }
}
