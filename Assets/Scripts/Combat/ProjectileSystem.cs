using System.Collections.Generic;
using UnityEngine;

public class ProjectileSystem : MonoBehaviour
{
    private const int POOL_SIZE = 192;
    private const int HIT_BUFFER_SIZE = 16;
    private const float WALL_NUDGE = 0.02f;

    private class ProjectileState
    {
        public bool IsActive { get; set; }
        public WeaponData Weapon { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public float Radius { get; set; }
        public float Length { get; set; }
        public GameObject Visual { get; set; }
        public GameObject VisualSource { get; set; }
        public float RemainingRange { get; set; }
        public float LegDistance { get; set; }
        public float RangeMultiplier { get; set; }
        public float DamageMultiplier { get; set; }
        public int BouncesLeft { get; set; }
        public float BounceRange { get; set; }
        public bool IsBounced { get; set; }
        public LayerMask HitMask { get; set; }
        public Health Owner { get; set; }
        public IProjectileListener Listener { get; set; }
    }

    private readonly ProjectileState[] states = new ProjectileState[POOL_SIZE];
    private readonly LineRenderer[] lines = new LineRenderer[POOL_SIZE];
    private readonly RaycastHit[] hitBuffer = new RaycastHit[HIT_BUFFER_SIZE];
    private readonly Dictionary<GameObject, Stack<GameObject>> visualPools = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, Vector3> nativeSizes = new Dictionary<GameObject, Vector3>();

    private int nextIndex;

    public static ProjectileSystem Instance { get; private set; }

    public static void Initialize(Material material)
    {
        if (Instance != null) return;

        GameObject systemObject = new GameObject("ProjectileSystem");

        systemObject.transform.SetParent(RuntimeRoot.Instance.transform);
        systemObject.AddComponent<ProjectileSystem>().Build(material);
    }

    public static void Spawn(ProjectileLaunch launch)
    {
        if (Instance == null)
        {
            Initialize(null);
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
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < POOL_SIZE; i++)
        {
            if (states[i].IsActive)
            {
                Step(i, deltaTime);
            }
        }
    }

    private void Build(Material material)
    {
        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject lineObject = new GameObject("Projectile" + i);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();

            lineObject.transform.SetParent(transform);
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            lines[i] = line;
            states[i] = new ProjectileState();
        }
    }

    private void Launch(ProjectileLaunch launch)
    {
        for (int attempt = 0; attempt < POOL_SIZE; attempt++)
        {
            int index = (nextIndex + attempt) % POOL_SIZE;
            ProjectileState state = states[index];

            if (state.IsActive) continue;

            WeaponData weapon = launch.Weapon;

            state.IsActive = true;
            state.Weapon = weapon;
            state.Position = launch.Origin;
            state.Direction = launch.Direction.normalized;
            state.Speed = weapon.ProjectileSpeed * (launch.SpeedMultiplier > 0f ? launch.SpeedMultiplier : 1f);
            state.Radius = weapon.ProjectileRadius;
            state.Length = weapon.ProjectileLength;
            state.RemainingRange = launch.Range;
            state.LegDistance = 0f;
            state.RangeMultiplier = launch.RangeMultiplier;
            state.DamageMultiplier = launch.DamageMultiplier;
            state.BouncesLeft = launch.Bounces;
            state.BounceRange = launch.BounceRange;
            state.IsBounced = false;
            state.HitMask = launch.HitMask;
            state.Owner = launch.Owner;
            state.Listener = launch.Listener;

            LineRenderer line = lines[index];

            line.startWidth = weapon.TracerWidth;
            line.endWidth = weapon.TracerWidth;
            line.startColor = weapon.TracerColor;
            line.endColor = weapon.TracerColor;
            state.VisualSource = weapon.IsMelee ? null : weapon.BulletPrefab;
            float visualLength = state.Length;

            state.Visual = state.VisualSource != null ? AcquireVisual(state.VisualSource, weapon, out visualLength) : null;

            if (state.Visual != null)
            {
                state.Length = visualLength;
            }

            line.enabled = state.Visual == null && !weapon.IsMelee;
            UpdateLine(index);
            nextIndex = (index + 1) % POOL_SIZE;

            return;
        }
    }

    private void Step(int index, float deltaTime)
    {
        ProjectileState state = states[index];
        float travel = Mathf.Min(state.Speed * deltaTime, state.RemainingRange);
        int hitCount = Physics.SphereCastNonAlloc(state.Position, state.Radius, state.Direction, hitBuffer, travel, state.HitMask, QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        int nearestIndex = -1;
        Health nearestHealth = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null && !IsValidTarget(state, health)) continue;
            if (hit.distance >= nearestDistance) continue;

            nearestDistance = hit.distance;
            nearestIndex = i;
            nearestHealth = health;
        }

        if (nearestIndex < 0)
        {
            state.Position += state.Direction * travel;
            state.LegDistance += travel;
            state.RemainingRange -= travel;

            if (state.RemainingRange <= 0f)
            {
                Explode(state, state.Position);
                Deactivate(index);
            }
            else
            {
                UpdateLine(index);
            }

            return;
        }

        Vector3 center = state.Position + state.Direction * nearestDistance;

        state.LegDistance += nearestDistance;

        if (nearestHealth != null && state.Weapon.Explosive)
        {
            Explode(state, center);
            Deactivate(index);

            return;
        }

        if (nearestHealth != null)
        {
            ProjectileHit report = new ProjectileHit(nearestHealth, state.Weapon, state.LegDistance / state.RangeMultiplier, state.DamageMultiplier, state.IsBounced);

            if (state.Listener != null && (state.Listener as Object) != null)
            {
                state.Listener.HandleProjectileHit(report);
            }

            Deactivate(index);

            return;
        }

        // Only the server copy of a shot has a listener, so only it damages barrels
        if (state.Listener != null && (state.Listener as Object) != null)
        {
            ExplosiveBarrel barrel = hitBuffer[nearestIndex].collider.GetComponentInParent<ExplosiveBarrel>();

            if (barrel != null)
            {
                barrel.ServerRegisterHit(state.Owner);
            }
        }

        if (state.BouncesLeft <= 0 || state.Weapon.Explosive)
        {
            Explode(state, center);
            Deactivate(index);

            return;
        }

        Vector3 normal = hitBuffer[nearestIndex].normal;
        Vector3 bounceDirection = Vector3.Reflect(state.Direction, normal);

        bounceDirection.y = 0f;
        state.Direction = bounceDirection.sqrMagnitude > 0.0001f ? bounceDirection.normalized : -state.Direction;
        state.Position = center + normal * WALL_NUDGE;
        state.BouncesLeft--;
        state.IsBounced = true;
        state.RemainingRange = state.BounceRange;
        state.LegDistance = 0f;
        UpdateLine(index);
    }

    private void Explode(ProjectileState state, Vector3 point)
    {
        if (!state.Weapon.Explosive) return;

        Explosion.Detonate(state.Weapon, point, state.HitMask, state.Owner, state.Listener, transform);
    }

    private bool IsValidTarget(ProjectileState state, Health target)
    {
        if (target == state.Owner || !target.IsAlive) return false;

        return state.Owner == null || state.Owner.TeamId < 0 || state.Owner.TeamId != target.TeamId;
    }

    private void UpdateLine(int index)
    {
        ProjectileState state = states[index];

        if (state.Visual != null)
        {
            state.Visual.transform.SetPositionAndRotation(state.Position - state.Direction * (state.Length * 0.5f), Quaternion.LookRotation(state.Direction));

            return;
        }

        lines[index].SetPosition(0, state.Position - state.Direction * state.Length);
        lines[index].SetPosition(1, state.Position);
    }

    private GameObject AcquireVisual(GameObject prefab, WeaponData weapon, out float length)
    {
        if (!visualPools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            visualPools[prefab] = pool;
        }

        GameObject visual = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);

        if (!nativeSizes.TryGetValue(prefab, out Vector3 nativeSize))
        {
            nativeSize = MeasureSize(visual);
            nativeSizes[prefab] = nativeSize;
        }

        length = nativeSize.z * weapon.BulletScale;
        visual.transform.localScale = prefab.transform.localScale * weapon.BulletScale;
        visual.SetActive(true);

        return visual;
    }

    private Vector3 MeasureSize(GameObject visual)
    {
        visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;

        foreach (Renderer meshRenderer in renderers)
        {
            bounds.Encapsulate(meshRenderer.bounds);
        }

        return bounds.size;
    }

    private void Deactivate(int index)
    {
        ProjectileState state = states[index];

        state.IsActive = false;
        state.Listener = null;
        lines[index].enabled = false;

        if (state.Visual == null) return;

        state.Visual.SetActive(false);
        visualPools[state.VisualSource].Push(state.Visual);
        state.Visual = null;
    }
}
