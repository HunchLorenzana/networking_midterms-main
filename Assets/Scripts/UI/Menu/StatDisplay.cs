using UnityEngine;

public static class StatDisplay
{
    private const float MULTIPLIER_BAR_MIN = 0.5f;
    private const float MULTIPLIER_BAR_MAX = 1.5f;
    private const float HEALTH_BAR_MIN = 0.5f;
    private const float HEALTH_BAR_MAX = 1.7f;

    public static float GetHealthFill(StatTotals stats)
    {
        return Mathf.InverseLerp(HEALTH_BAR_MIN, HEALTH_BAR_MAX, stats.MaxHealth);
    }

    public static float GetMultiplierFill(float multiplier)
    {
        return Mathf.InverseLerp(MULTIPLIER_BAR_MIN, MULTIPLIER_BAR_MAX, multiplier);
    }

    public static string FormatPercent(float multiplier)
    {
        return Mathf.RoundToInt(multiplier * 100f) + "%";
    }
}
