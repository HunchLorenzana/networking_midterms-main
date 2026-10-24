using System.Collections.Generic;
using UnityEngine;

public class StatTotals
{
    private const float MAX_HEALTH_MIN = 0.6f;
    private const float MAX_HEALTH_MAX = 1.6f;
    private const float MOVE_SPEED_MIN = 0.7f;
    private const float MOVE_SPEED_MAX = 1.3f;
    private const float DAMAGE_DEALT_MIN = 0.7f;
    private const float DAMAGE_DEALT_MAX = 1.5f;
    private const float DAMAGE_TAKEN_MIN = 0.5f;
    private const float DAMAGE_TAKEN_MAX = 1.5f;
    private const float RELOAD_SPEED_MIN = 0.6f;
    private const float RELOAD_SPEED_MAX = 1.6f;
    private const float PROJECTILE_SPEED_MIN = 0.7f;
    private const float PROJECTILE_SPEED_MAX = 1.5f;
    private const float RANGE_MIN = 0.7f;
    private const float RANGE_MAX = 1.4f;

    public static StatTotals Default { get; } = new StatTotals(1f, 1f, 1f, 1f, 1f, 1f, 1f);

    public float MaxHealth { get; }
    public float MoveSpeed { get; }
    public float DamageDealt { get; }
    public float DamageTaken { get; }
    public float ReloadSpeed { get; }
    public float ProjectileSpeed { get; }
    public float Range { get; }

    private StatTotals(float maxHealth, float moveSpeed, float damageDealt, float damageTaken, float reloadSpeed, float projectileSpeed, float range)
    {
        MaxHealth = maxHealth;
        MoveSpeed = moveSpeed;
        DamageDealt = damageDealt;
        DamageTaken = damageTaken;
        ReloadSpeed = reloadSpeed;
        ProjectileSpeed = projectileSpeed;
        Range = range;
    }

    public static StatTotals Calculate(WeaponData primary, WeaponData secondary, IReadOnlyList<PassiveData> passives)
    {
        float maxHealth = 0f;
        float moveSpeed = 0f;
        float damageDealt = 0f;
        float damageTaken = 0f;
        float reloadSpeed = 0f;
        float projectileSpeed = 0f;
        float range = 0f;

        foreach (WeaponData weapon in new[] { primary, secondary })
        {
            if (weapon == null) continue;

            maxHealth += weapon.MaxHealthMultiplier - 1f;
            moveSpeed += weapon.MoveSpeedMultiplier - 1f;
            damageTaken += weapon.DamageTakenMultiplier - 1f;
        }

        if (passives != null)
        {
            foreach (PassiveData passive in passives)
            {
                if (passive == null) continue;

                maxHealth += passive.MaxHealthPercent;
                moveSpeed += passive.MoveSpeedPercent;
                damageDealt += passive.DamageDealtPercent;
                damageTaken += passive.DamageTakenPercent;
                reloadSpeed += passive.ReloadSpeedPercent;
                projectileSpeed += passive.ProjectileSpeedPercent;
                range += passive.RangePercent;
            }
        }

        return new StatTotals(
            Mathf.Clamp(1f + maxHealth, MAX_HEALTH_MIN, MAX_HEALTH_MAX),
            Mathf.Clamp(1f + moveSpeed, MOVE_SPEED_MIN, MOVE_SPEED_MAX),
            Mathf.Clamp(1f + damageDealt, DAMAGE_DEALT_MIN, DAMAGE_DEALT_MAX),
            Mathf.Clamp(1f + damageTaken, DAMAGE_TAKEN_MIN, DAMAGE_TAKEN_MAX),
            Mathf.Clamp(1f + reloadSpeed, RELOAD_SPEED_MIN, RELOAD_SPEED_MAX),
            Mathf.Clamp(1f + projectileSpeed, PROJECTILE_SPEED_MIN, PROJECTILE_SPEED_MAX),
            Mathf.Clamp(1f + range, RANGE_MIN, RANGE_MAX));
    }
}
