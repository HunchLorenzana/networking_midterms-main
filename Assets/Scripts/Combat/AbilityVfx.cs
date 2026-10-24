using System.Collections;
using DG.Tweening;
using UnityEngine;

public class AbilityVfx : MonoBehaviour
{
    private const float GHOST_INTERVAL_SECONDS = 0.03f;
    private const float GHOST_FADE_SECONDS = 0.4f;
    private const float GHOST_EXTRA_SECONDS = 0.05f;
    private const float BURST_LIFETIME_SECONDS = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Health health;
    [SerializeField] private CharacterVisual characterVisual;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color ghostColor = new Color(0.45f, 0.9f, 1f, 0.55f);
    [SerializeField] private GameObject adrenalineAura;
    [SerializeField] private GameObject ricochetGlow;
    [SerializeField] private GameObject dashBurstPrefab;
    [SerializeField] private GameObject adrenalineBurstPrefab;
    [SerializeField] private float dashBurstScale = 0.7f;
    [SerializeField] private float adrenalineBurstScale = 0.7f;
    [SerializeField] private GameObject chargedBurstPrefab;
    [SerializeField] private GameObject focusBurstPrefab;
    [SerializeField] private GameObject focusAuraPrefab;
    [SerializeField] private GameObject shockwaveBurstPrefab;
    [SerializeField] private GameObject pullBurstPrefab;
    [SerializeField] private float pullBurstScale = 1.5f;
    [SerializeField] private float chargedBurstScale = 0.5f;
    [SerializeField] private float focusBurstScale = 0.6f;
    [SerializeField] private float shockwaveBurstScale = 1.5f;
    [SerializeField] private float loopAuraScale = 0.6f;

    private readonly System.Collections.Generic.List<GameObject> timedLoops = new System.Collections.Generic.List<GameObject>();
    private ParticleSystem[] auraSystems;
    private ParticleSystem[] glowSystems;
    private Coroutine dashRoutine;
    private Coroutine adrenalineStopRoutine;

    void Awake()
    {
        auraSystems = adrenalineAura.GetComponentsInChildren<ParticleSystem>(true);
        glowSystems = ricochetGlow.GetComponentsInChildren<ParticleSystem>(true);
        StopSystems(auraSystems, ParticleSystemStopBehavior.StopEmittingAndClear);
        StopSystems(glowSystems, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnEnable()
    {
        health.Died += HandleDied;
    }

    void OnDisable()
    {
        health.Died -= HandleDied;
        StopAll();
    }

    public void Play(AbilityData ability)
    {
        switch (ability.Type)
        {
            case AbilityType.Dash:
                SpawnBurst(dashBurstPrefab, transform, Vector3.zero, dashBurstScale, false);
                RestartDashTrail(ability.Duration);
                break;
            case AbilityType.Adrenaline:
                SpawnBurst(adrenalineBurstPrefab, transform, Vector3.up * 0.05f, adrenalineBurstScale, true);
                RestartTimed(auraSystems, ability.Duration, ref adrenalineStopRoutine);
                break;
            case AbilityType.ChargedShot:
                SpawnBurst(chargedBurstPrefab, ricochetGlow.transform.parent, ricochetGlow.transform.localPosition, chargedBurstScale, true);
                break;
            case AbilityType.Focus:
                SpawnBurst(focusBurstPrefab, transform, Vector3.up * 0.05f, focusBurstScale, true);
                SpawnTimedLoop(focusAuraPrefab, ability.Duration);
                break;
            case AbilityType.Shockwave:
                SpawnBurst(shockwaveBurstPrefab, transform, Vector3.up * 0.1f, shockwaveBurstScale * ability.Magnitude / 4f, false);
                break;
            case AbilityType.Pull:
                SpawnBurst(pullBurstPrefab, transform, Vector3.up * 0.1f, pullBurstScale * ability.Magnitude / 8f, false);
                break;
        }
    }

    private void SpawnTimedLoop(GameObject prefab, float duration)
    {
        if (prefab == null) return;

        GameObject loop = Instantiate(prefab, transform);

        loop.transform.localPosition = Vector3.up * 0.05f;
        loop.transform.localScale = prefab.transform.localScale * loopAuraScale;

        foreach (ParticleSystem system in loop.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        timedLoops.Add(loop);
        StartCoroutine(EndTimedLoop(loop, duration));
    }

    private IEnumerator EndTimedLoop(GameObject loop, float duration)
    {
        yield return new WaitForSeconds(duration);

        timedLoops.Remove(loop);

        if (loop == null) yield break;

        StopSystems(loop.GetComponentsInChildren<ParticleSystem>(true), ParticleSystemStopBehavior.StopEmitting);
        Destroy(loop, BURST_LIFETIME_SECONDS);
    }

    private void SpawnBurst(GameObject prefab, Transform parent, Vector3 localPosition, float scale, bool isFollowing)
    {
        if (prefab == null) return;

        GameObject burst = Instantiate(prefab, parent);

        burst.transform.localPosition = localPosition;
        burst.transform.localScale = prefab.transform.localScale * scale;

        if (!isFollowing)
        {
            burst.transform.SetParent(RuntimeRoot.Instance.transform, true);
        }

        foreach (ParticleSystem system in burst.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        Destroy(burst, BURST_LIFETIME_SECONDS);
    }

    private void RestartDashTrail(float duration)
    {
        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
        }

        dashRoutine = StartCoroutine(DashTrail(duration + GHOST_EXTRA_SECONDS));
    }

    private void RestartTimed(ParticleSystem[] systems, float duration, ref Coroutine stopRoutine)
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
        }

        foreach (ParticleSystem system in systems)
        {
            system.Play(false);
        }

        stopRoutine = StartCoroutine(StopEmittingAfter(systems, duration));
    }

    private IEnumerator DashTrail(float duration)
    {
        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            SpawnGhost();

            yield return new WaitForSeconds(GHOST_INTERVAL_SECONDS);
        }

        dashRoutine = null;
    }

    private IEnumerator StopEmittingAfter(ParticleSystem[] systems, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        StopSystems(systems, ParticleSystemStopBehavior.StopEmitting);
    }

    private void SpawnGhost()
    {
        Transform modelRoot = characterVisual.ModelRoot;

        if (modelRoot == null) return;

        GameObject ghostRoot = new GameObject("DashGhost");
        Material material = new Material(ghostMaterial);
        Color clear = ghostColor;

        clear.a = 0f;
        material.SetColor(BaseColorId, ghostColor);
        ghostRoot.transform.SetParent(RuntimeRoot.Instance.transform);

        foreach (MeshFilter filter in modelRoot.GetComponentsInChildren<MeshFilter>())
        {
            GameObject part = new GameObject("Part");
            MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
            MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();

            part.transform.SetParent(ghostRoot.transform);
            part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            part.transform.localScale = filter.transform.lossyScale;
            ghostFilter.sharedMesh = filter.sharedMesh;
            ghostRenderer.sharedMaterial = material;
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        material.DOColor(clear, BaseColorId, GHOST_FADE_SECONDS).SetEase(Ease.OutQuad).SetLink(ghostRoot).OnComplete(() =>
        {
            Destroy(ghostRoot);
            Destroy(material);
        });
    }

    private void HandleDied(Health killer)
    {
        StopAll();
    }

    private void StopAll()
    {
        StopAllCoroutines();

        foreach (GameObject loop in timedLoops)
        {
            if (loop != null)
            {
                Destroy(loop);
            }
        }

        timedLoops.Clear();
        dashRoutine = null;
        adrenalineStopRoutine = null;
        StopSystems(auraSystems, ParticleSystemStopBehavior.StopEmitting);
        StopSystems(glowSystems, ParticleSystemStopBehavior.StopEmitting);
    }

    private static void StopSystems(ParticleSystem[] systems, ParticleSystemStopBehavior behavior)
    {
        if (systems == null) return;

        foreach (ParticleSystem system in systems)
        {
            system.Stop(false, behavior);
        }
    }
}
