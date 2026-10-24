using System.Collections.Generic;
using UnityEngine;

public static class Explosion
{
    private const float EFFECT_LIFETIME_SECONDS = 3f;
    private const int OVERLAP_BUFFER_SIZE = 32;

    private static readonly Collider[] overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];
    private static readonly HashSet<Health> damaged = new HashSet<Health>();

    public static void Detonate(WeaponData weapon, Vector3 point, LayerMask hitMask, Health owner, IProjectileListener listener, Transform effectParent)
    {
        if (weapon.ExplosionPrefab != null)
        {
            GameObject effect = Object.Instantiate(weapon.ExplosionPrefab, point, Quaternion.identity, effectParent);

            effect.transform.localScale = weapon.ExplosionPrefab.transform.localScale * weapon.ExplosionScale;
            Object.Destroy(effect, EFFECT_LIFETIME_SECONDS);
        }

        if (listener == null || (listener as Object) == null) return;

        int count = Physics.OverlapSphereNonAlloc(point, weapon.ThrowRadius, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

        damaged.Clear();

        for (int i = 0; i < count; i++)
        {
            ExplosiveBarrel barrel = overlapBuffer[i].GetComponentInParent<ExplosiveBarrel>();

            if (barrel != null)
            {
                barrel.ServerDetonate(owner, 0f);

                continue;
            }

            Health health = overlapBuffer[i].GetComponentInParent<Health>();

            if (health == null || !damaged.Add(health) || !IsValidTarget(owner, health)) continue;

            listener.HandleProjectileHit(new ProjectileHit(health, weapon, 0f, 1f, false));
        }
    }

    private static bool IsValidTarget(Health owner, Health target)
    {
        if (target == owner || !target.IsAlive) return false;

        return owner == null || owner.TeamId < 0 || owner.TeamId != target.TeamId;
    }
}
