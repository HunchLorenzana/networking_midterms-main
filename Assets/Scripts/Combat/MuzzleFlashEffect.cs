using UnityEngine;

public class MuzzleFlashEffect : MonoBehaviour
{
    private const float FLASH_LIFETIME_SECONDS = 1.5f;

    void OnEnable()
    {
        PlayerWeapon.AnyShotFired += Spawn;
    }

    void OnDisable()
    {
        PlayerWeapon.AnyShotFired -= Spawn;
    }

    private void Spawn(Vector3 position, Vector3 direction, WeaponData weapon)
    {
        GameObject prefab = weapon.MuzzleFlashPrefab;

        if (weapon.IsMelee || prefab == null || direction.sqrMagnitude < 0.0001f) return;

        Quaternion rotation = Quaternion.LookRotation(direction) * prefab.transform.rotation;
        GameObject flash = Instantiate(prefab, position, rotation, RuntimeRoot.Instance.transform);

        flash.transform.localScale = prefab.transform.localScale * weapon.MuzzleFlashScale;

        foreach (ParticleSystem system in flash.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        Destroy(flash, FLASH_LIFETIME_SECONDS);
    }
}
