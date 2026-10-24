using System.Collections.Generic;
using UnityEngine;

public class ThrowableSystem : MonoBehaviour
{
    private const float MIN_FLIGHT_SECONDS = 0.3f;
    private const float SPIN_DEGREES_PER_SECOND = 540f;

    private class ThrowState
    {
        public WeaponData Weapon { get; set; }
        public Vector3 Origin { get; set; }
        public Vector3 Target { get; set; }
        public float Elapsed { get; set; }
        public float Duration { get; set; }
        public GameObject Visual { get; set; }
        public GameObject VisualSource { get; set; }
        public LayerMask HitMask { get; set; }
        public Health Owner { get; set; }
        public IProjectileListener Listener { get; set; }
    }

    private readonly List<ThrowState> active = new List<ThrowState>();
    private readonly Dictionary<GameObject, Stack<GameObject>> visualPools = new Dictionary<GameObject, Stack<GameObject>>();

    public static ThrowableSystem Instance { get; private set; }

    public static void Spawn(ThrowLaunch launch)
    {
        if (Instance == null)
        {
            GameObject systemObject = new GameObject("ThrowableSystem");

            systemObject.transform.SetParent(RuntimeRoot.Instance.transform);
            systemObject.AddComponent<ThrowableSystem>();
        }

        Instance.Launch(launch);
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ThrowState state = active[i];

            state.Elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(state.Elapsed / state.Duration);

            if (state.Visual != null)
            {
                Vector3 position = Vector3.Lerp(state.Origin, state.Target, progress);

                position.y += 4f * state.Weapon.ThrowArcHeight * progress * (1f - progress);
                state.Visual.transform.position = position;
                state.Visual.transform.Rotate(SPIN_DEGREES_PER_SECOND * Time.deltaTime, 0f, 0f, Space.Self);
            }

            if (progress < 1f) continue;

            Land(state);
            active.RemoveAt(i);
        }
    }

    private void Launch(ThrowLaunch launch)
    {
        WeaponData weapon = launch.Weapon;
        Vector3 flat = launch.Target - launch.Origin;

        flat.y = 0f;

        ThrowState state = new ThrowState
        {
            Weapon = weapon,
            Origin = launch.Origin,
            Target = launch.Target,
            Duration = Mathf.Max(MIN_FLIGHT_SECONDS, flat.magnitude / Mathf.Max(0.1f, weapon.ProjectileSpeed)),
            HitMask = launch.HitMask,
            Owner = launch.Owner,
            Listener = launch.Listener
        };

        bool hasThrownPrefab = weapon.ThrownPrefab != null;
        GameObject source = hasThrownPrefab ? weapon.ThrownPrefab : weapon.GetGunModel(0);

        if (source != null)
        {
            state.VisualSource = source;
            state.Visual = AcquireVisual(source);
            state.Visual.transform.position = launch.Origin;
            state.Visual.transform.localScale = source.transform.localScale * (hasThrownPrefab ? weapon.ThrownScale : weapon.GetGunScale(0));
        }

        active.Add(state);
    }

    private GameObject AcquireVisual(GameObject source)
    {
        if (!visualPools.TryGetValue(source, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            visualPools[source] = pool;
        }

        GameObject visual = pool.Count > 0 ? pool.Pop() : Instantiate(source, transform);

        visual.SetActive(true);

        return visual;
    }

    private void Land(ThrowState state)
    {
        WeaponData weapon = state.Weapon;

        if (state.Visual != null)
        {
            state.Visual.SetActive(false);
            visualPools[state.VisualSource].Push(state.Visual);
            state.Visual = null;
        }

        Explosion.Detonate(weapon, state.Target, state.HitMask, state.Owner, state.Listener, transform);
    }
}
