using System.Collections.Generic;
using UnityEngine;

public class StatusVfxEffect : MonoBehaviour
{
    private const float LINGER_SECONDS = 1.5f;

    [SerializeField] private GameObject weakenPrefab;
    [SerializeField] private float weakenScale = 0.6f;
    [SerializeField] private float weakenHeight = 2.1f;
    [SerializeField] private GameObject protectionPrefab;
    [SerializeField] private float protectionScale = 0.4f;
    [SerializeField] private float protectionHeight = 0.9f;

    private readonly Dictionary<Health, GameObject> weakenLoops = new Dictionary<Health, GameObject>();
    private readonly Dictionary<Health, GameObject> protectionLoops = new Dictionary<Health, GameObject>();

    void OnEnable()
    {
        Health.WeakenedChanged += HandleWeakenedChanged;
        Health.ProtectionChanged += HandleProtectionChanged;
    }

    void OnDisable()
    {
        Health.WeakenedChanged -= HandleWeakenedChanged;
        Health.ProtectionChanged -= HandleProtectionChanged;
    }

    private void HandleWeakenedChanged(Health target, bool isActive)
    {
        Apply(weakenLoops, weakenPrefab, weakenScale, weakenHeight, target, isActive);
    }

    private void HandleProtectionChanged(Health target, bool isActive)
    {
        Apply(protectionLoops, protectionPrefab, protectionScale, protectionHeight, target, isActive);
    }

    private static void Apply(Dictionary<Health, GameObject> loops, GameObject prefab, float scale, float height, Health target, bool isActive)
    {
        if (target == null || prefab == null) return;

        if (loops.TryGetValue(target, out GameObject existing))
        {
            loops.Remove(target);

            if (existing != null)
            {
                StopAndDestroy(existing);
            }
        }

        if (!isActive) return;

        GameObject loop = Instantiate(prefab, target.transform);

        loop.transform.localPosition = Vector3.up * height;
        loop.transform.localScale = prefab.transform.localScale * scale;

        foreach (ParticleSystem system in loop.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        loops[target] = loop;
    }

    private static void StopAndDestroy(GameObject loop)
    {
        foreach (ParticleSystem system in loop.GetComponentsInChildren<ParticleSystem>(true))
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(loop, LINGER_SECONDS);
    }
}
