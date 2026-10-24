using UnityEngine;

public class WeaponAmmo
{
    private float ammo;
    private float lastTickTime;
    private float lastFireTime = float.NegativeInfinity;
    private float nextFireTime;

    public float Ammo => ammo;

    public float GetReadyFraction(float now)
    {
        float duration = nextFireTime - lastFireTime;

        if (now >= nextFireTime || duration <= 0f) return 1f;

        return Mathf.Clamp01(1f - (nextFireTime - now) / duration);
    }

    public void Reset(WeaponData weapon, float now)
    {
        ammo = weapon.MagazineSize;
        lastTickTime = now;
        lastFireTime = float.NegativeInfinity;
        nextFireTime = 0f;
    }

    public void Tick(WeaponData weapon, float now, float speedMultiplier = 1f)
    {
        if (ammo >= weapon.MagazineSize)
        {
            lastTickTime = now;
            return;
        }

        float regenStart = Mathf.Max(lastTickTime, lastFireTime + weapon.RegenDelay);

        if (now > regenStart)
        {
            ammo = Mathf.Min(weapon.MagazineSize, ammo + (now - regenStart) * weapon.MagazineSize / weapon.RefillTime * speedMultiplier);
        }

        lastTickTime = now;
    }

    public void AddRounds(float rounds, WeaponData weapon)
    {
        ammo = Mathf.Min(weapon.MagazineSize, ammo + rounds);
    }

    public bool TryConsume(float now, float interval, float ammoTolerance, float intervalScale)
    {
        if (ammo < 1f - ammoTolerance || now < nextFireTime) return false;

        ammo = Mathf.Max(0f, ammo - 1f);
        lastFireTime = now;
        nextFireTime = now + interval * intervalScale;

        return true;
    }
}
