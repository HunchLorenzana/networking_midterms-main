using UnityEngine;

public static class ShotBuilder
{
    public static int CountPoints(int pointMask)
    {
        int count = 0;

        for (int mask = pointMask; mask != 0; mask >>= 1)
        {
            count += mask & 1;
        }

        return count;
    }

    public static void Build(WeaponData weapon, int slot, int pointMask, float spread, HeldWeaponVisual visual, Vector3 fallbackOrigin, Vector3[] origins, float[] yawOffsets)
    {
        int index = 0;

        for (int point = 0; point < weapon.FirePointCount; point++)
        {
            if ((pointMask & (1 << point)) == 0) continue;

            Vector3 origin = visual.TryGetMuzzle(slot, point, out Vector3 muzzle) ? muzzle : fallbackOrigin;
            float angle = weapon.GetFirePointAngle(point);

            for (int pellet = 0; pellet < weapon.PelletCount; pellet++)
            {
                origins[index] = origin;
                yawOffsets[index] = angle + Random.Range(-spread, spread);
                index++;
            }
        }
    }
}
