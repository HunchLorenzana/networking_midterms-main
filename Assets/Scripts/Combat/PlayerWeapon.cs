using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerWeapon : NetworkBehaviour, IProjectileListener
{
    public const int SLOT_COUNT = 2;

    private const float SERVER_AMMO_TOLERANCE = 0.5f;
    private const float SERVER_INTERVAL_SCALE = 0.6f;
    private const float MAX_ORIGIN_OFFSET = 2.5f;
    private const float SPREAD_TOLERANCE_DEGREES = 0.5f;
    private const float MIN_THROW_DISTANCE = 1.5f;
    private const float THROW_RANGE_TOLERANCE = 1.5f;
    private const int BACKSTAB_DAMAGE = 9999;
    private const float FLOATING_TEXT_HEIGHT = 2.4f;

    public static event Action<Vector3, Vector3, WeaponData> AnyPelletFired;
    public static event Action<Vector3, Vector3, WeaponData> AnyShotFired;
    public static event Action<Vector3, FloatingTextKind> FloatingTextRequested;

    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Health ownerHealth;
    [SerializeField] private Transform shotOrigin;
    [SerializeField] private HeldWeaponVisual heldVisual;
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private LayerMask hitMask = ~0;

    private readonly WeaponData[] weapons = new WeaponData[SLOT_COUNT];
    private readonly WeaponAmmo[] ownerAmmo = { new WeaponAmmo(), new WeaponAmmo() };
    private readonly WeaponAmmo[] serverAmmo = { new WeaponAmmo(), new WeaponAmmo() };

    private readonly NetworkVariable<int> activeSlot = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly float[] extraSpread = new float[SLOT_COUNT];
    private readonly int[] shotCounters = new int[SLOT_COUNT];

    private float ownerFireRateMultiplier = 1f;
    private float ownerFireRateEndTime;
    private float serverFireRateMultiplier = 1f;
    private float serverFireRateEndTime;
    private float ownerFocusEndTime;
    private float ownerFocusSpreadMultiplier = 1f;
    private float ownerFocusRangeMultiplier = 1f;
    private float serverChargedEndTime;
    private float serverChargedMultiplier = 1f;
    private float serverFocusEndTime;
    private float serverFocusRangeMultiplier = 1f;
    private readonly PassiveRuntime passives = new PassiveRuntime();
    private PassiveData[] equippedPassives = new PassiveData[0];
    private StatTotals stats = StatTotals.Default;

    public int ActiveSlot => activeSlot.Value;
    public float MoveSpeedMultiplier => stats.MoveSpeed * passives.GetMoveSpeedFactor(Time.time);

    private WeaponData ActiveWeapon => weapons[Mathf.Clamp(activeSlot.Value, 0, SLOT_COUNT - 1)];

    public static void RaisePelletFired(Vector3 start, Vector3 end, WeaponData weapon)
    {
        AnyPelletFired?.Invoke(start, end, weapon);
    }

    public static void RaiseShotFired(Vector3 muzzle, Vector3 direction, WeaponData weapon)
    {
        AnyShotFired?.Invoke(muzzle, direction, weapon);
    }

    public WeaponData GetWeapon(int slot)
    {
        return weapons[slot];
    }

    public int GetAmmoCount(int slot)
    {
        return Mathf.FloorToInt(ownerAmmo[slot].Ammo);
    }

    public float GetAmmoFraction(int slot)
    {
        return ownerAmmo[slot].Ammo / weapons[slot].MagazineSize;
    }

    private Vector3 GetMuzzlePosition(int slot, int pointIndex = 0)
    {
        return heldVisual.TryGetMuzzle(slot, pointIndex, out Vector3 muzzle) ? muzzle : shotOrigin.position;
    }

    public float GetFireReadyFraction(int slot)
    {
        return ownerAmmo[slot].GetReadyFraction(Time.time);
    }

    void OnEnable()
    {
        ownerHealth.Revived += HandleRevived;
        ownerHealth.Damaged += HandleOwnerDamaged;
        inputReader.AimStarted += HandleAimStarted;
        inputReader.ShotReleased += HandleShotReleased;
    }

    void OnDisable()
    {
        ownerHealth.Revived -= HandleRevived;
        ownerHealth.Damaged -= HandleOwnerDamaged;
        inputReader.AimStarted -= HandleAimStarted;
        inputReader.ShotReleased -= HandleShotReleased;
    }

    void Update()
    {
        if (IsServer && weapons[0] != null && weapons[1] != null)
        {
            ownerHealth.DamageTakenMultiplier = stats.DamageTaken * passives.GetDamageTakenFactor();

            int regen = ownerHealth.IsAlive ? passives.GetRegenAmount(Time.time, ownerHealth.MaxHealth) : 0;

            if (regen > 0)
            {
                ownerHealth.Heal(regen);
            }
        }

        if (!IsOwner || weapons[0] == null) return;

        if (!ownerHealth.IsAlive)
        {
            HideIndicators();

            return;
        }

        float now = Time.time;

        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            WeaponData weapon = weapons[slot];

            ownerAmmo[slot].Tick(weapon, now, stats.ReloadSpeed);
            extraSpread[slot] = Mathf.Max(0f, extraSpread[slot] - weapon.SpreadRecoverPerSecond * Time.deltaTime);

            if (inputReader.IsFireHeld(slot))
            {
                TryFire(slot);
            }

            UpdateIndicator(slot, weapon);
        }
    }

    private void UpdateIndicator(int slot, WeaponData weapon)
    {
        if (!inputReader.IsAimingSlot(slot))
        {
            AimIndicatorSystem.Hide(slot);

            return;
        }

        bool isFocused = Time.time < ownerFocusEndTime;
        float range = weapon.Range * (isFocused ? ownerFocusRangeMultiplier : 1f) * stats.Range;

        if (weapon.IndicatorShape == AimIndicatorShape.Circle)
        {
            Vector3 toCursor = inputReader.AimPoint - transform.position;

            toCursor.y = 0f;
            range = Mathf.Clamp(toCursor.magnitude, MIN_THROW_DISTANCE, weapon.Range);
        }

        float halfAngle = (weapon.SpreadAngle + extraSpread[slot]) * (isFocused ? ownerFocusSpreadMultiplier : 1f);
        Vector3 origin = shotOrigin.position;

        origin.y = transform.position.y;
        AimIndicatorSystem.Show(slot, origin, inputReader.AimDirection, weapon, range, halfAngle);
    }

    private void HideIndicators()
    {
        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            AimIndicatorSystem.Hide(slot);
        }
    }

    public void SetLoadout(WeaponData primary, WeaponData secondary)
    {
        weapons[0] = primary;
        weapons[1] = secondary;
        inputReader.SetFireMode(0, primary.FireMode);
        inputReader.SetFireMode(1, secondary.FireMode);

        if (IsSpawned && IsOwner)
        {
            activeSlot.Value = 0;
        }

        RefreshStats();
        RefillAll();
    }

    public void SetPassives(PassiveData[] equipped)
    {
        equippedPassives = equipped;
        passives.SetEquipped(equipped, Time.time);
        RefreshStats();
    }

    private void RefreshStats()
    {
        stats = StatTotals.Calculate(weapons[0], weapons[1], equippedPassives);
        ownerHealth.SetMaxHealthMultiplier(stats.MaxHealth);
    }

    private void HandleOwnerDamaged(int amount)
    {
        passives.NotifyDamaged(Time.time);

        if (IsServer)
        {
            ownerHealth.DamageTakenMultiplier = stats.DamageTaken * passives.GetDamageTakenFactor();
        }
    }

    public void ApplyFireRateBoost(float multiplier, float duration)
    {
        ownerFireRateMultiplier = multiplier;
        ownerFireRateEndTime = Time.time + duration;
    }

    public bool TryGetRicochetPreview(List<Vector3> points)
    {
        points.Clear();

        bool isPassive = passives.Ricochet != null && inputReader.IsAimingSlot(ActiveSlot);

        if (!IsOwner || !isPassive || !ownerHealth.IsAlive || ActiveWeapon == null) return false;

        float bounceRange = passives.Ricochet.SecondaryMagnitude;
        float range = ActiveWeapon.Range * (Time.time < ownerFocusEndTime ? ownerFocusRangeMultiplier : 1f) * stats.Range;
        Vector3 origin = shotOrigin.position;
        Vector3 direction = transform.forward;

        points.Add(GetMuzzlePosition(ActiveSlot));

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            points.Add(origin + direction * range);

            return true;
        }

        points.Add(hit.point);

        if (hit.collider.GetComponentInParent<Health>() != null) return true;

        Vector3 bounceDirection = Vector3.Reflect(direction, hit.normal);
        Vector3 bounceOrigin = hit.point + hit.normal * 0.01f;
        bool hasBounceHit = Physics.Raycast(bounceOrigin, bounceDirection, out RaycastHit bounceHit, bounceRange, hitMask, QueryTriggerInteraction.Ignore);

        points.Add(hasBounceHit ? bounceHit.point : bounceOrigin + bounceDirection * bounceRange);

        return true;
    }

    public void ServerApplyFireRateBoost(float multiplier, float duration)
    {
        serverFireRateMultiplier = multiplier;
        serverFireRateEndTime = Time.time + duration;
    }

    public void ActivateFocus(float duration, float spreadMultiplier, float rangeMultiplier)
    {
        ownerFocusEndTime = Time.time + duration;
        ownerFocusSpreadMultiplier = spreadMultiplier;
        ownerFocusRangeMultiplier = rangeMultiplier;
    }

    public void ServerActivateChargedShot(float window, float damageMultiplier)
    {
        serverChargedEndTime = Time.time + window;
        serverChargedMultiplier = damageMultiplier;
    }

    public void ServerActivateFocus(float duration, float rangeMultiplier)
    {
        serverFocusEndTime = Time.time + duration;
        serverFocusRangeMultiplier = rangeMultiplier;
    }

    private void HandleRevived()
    {
        RefillAll();
        passives.Reset(Time.time);

        if (IsServer)
        {
            ownerHealth.DamageTakenMultiplier = stats.DamageTaken * passives.GetDamageTakenFactor();
        }

        ownerFireRateEndTime = 0f;
        ownerFocusEndTime = 0f;
        serverFireRateEndTime = 0f;
        serverChargedEndTime = 0f;
        serverFocusEndTime = 0f;
    }

    private void RefillAll()
    {
        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            extraSpread[slot] = 0f;
            shotCounters[slot] = 0;
            ownerAmmo[slot].Reset(weapons[slot], Time.time);
            serverAmmo[slot].Reset(weapons[slot], Time.time);
        }
    }

    private void HandleAimStarted(int slot)
    {
        if (!IsOwner || weapons[slot] == null || !ownerHealth.IsAlive) return;

        activeSlot.Value = slot;
    }

    private void HandleShotReleased(int slot)
    {
        TryFire(slot);
    }

    private void TryFire(int slot)
    {
        if (!IsOwner || weapons[slot] == null || !ownerHealth.IsAlive) return;

        WeaponData weapon = weapons[slot];
        float now = Time.time;
        float rateMultiplier = now < ownerFireRateEndTime ? ownerFireRateMultiplier : 1f;

        ownerAmmo[slot].Tick(weapon, now, stats.ReloadSpeed);

        if (!ownerAmmo[slot].TryConsume(now, weapon.FireInterval / rateMultiplier, 0f, 1f)) return;

        activeSlot.Value = slot;
        FireShot(slot, inputReader.AimDirection);
    }

    private void FireShot(int slot, Vector3 aimDirection)
    {
        WeaponData weapon = weapons[slot];

        if (weapon.ShotType == WeaponShotType.Thrown)
        {
            FireThrow(slot, aimDirection);

            return;
        }

        bool isFocused = Time.time < ownerFocusEndTime;
        float spread = (weapon.SpreadAngle + extraSpread[slot]) * (isFocused ? ownerFocusSpreadMultiplier : 1f);
        float rangeMultiplier = (isFocused ? ownerFocusRangeMultiplier : 1f) * stats.Range;
        int pointMask = weapon.GetFirePointMask(shotCounters[slot]);
        int projectileCount = ShotBuilder.CountPoints(pointMask) * weapon.PelletCount;
        Vector3[] origins = new Vector3[projectileCount];
        float[] yawOffsets = new float[projectileCount];
        int bounces = passives.Ricochet != null ? 1 : 0;
        float bounceRange = passives.Ricochet != null ? passives.Ricochet.SecondaryMagnitude : 0f;

        shotCounters[slot]++;
        ShotBuilder.Build(weapon, slot, pointMask, spread, heldVisual, shotOrigin.position, origins, yawOffsets);

        for (int i = 0; i < origins.Length; i++)
        {
            origins[i] = ResolveOrigin(origins[i]);
        }

        extraSpread[slot] = Mathf.Min(weapon.MaxExtraSpread, extraSpread[slot] + weapon.SpreadGrowthPerShot);

        characterAnimator.PlayShoot();
        RaiseMuzzleFlashes(slot, weapon, pointMask, aimDirection);

        if (!IsServer)
        {
            LaunchProjectiles(weapon, origins, aimDirection, yawOffsets, rangeMultiplier, 1f, bounces, bounceRange, null);
        }

        SubmitShotRpc(slot, pointMask, origins, aimDirection, yawOffsets);
    }

    private void FireThrow(int slot, Vector3 aimDirection)
    {
        WeaponData weapon = weapons[slot];
        Vector3 muzzle = GetMuzzlePosition(slot);
        Vector3 origin = ResolveOrigin(muzzle);
        Vector3 target = GetThrowTarget(weapon, aimDirection);

        characterAnimator.PlayShoot();
        RaiseShotFired(muzzle, aimDirection, weapon);

        if (!IsServer)
        {
            ThrowableSystem.Spawn(BuildThrow(weapon, origin, target, null));
        }

        SubmitThrowRpc(slot, origin, target);
    }

    private Vector3 GetThrowTarget(WeaponData weapon, Vector3 aimDirection)
    {
        Vector3 toCursor = inputReader.AimPoint - transform.position;

        toCursor.y = 0f;

        Vector3 target = transform.position + aimDirection * Mathf.Clamp(toCursor.magnitude, MIN_THROW_DISTANCE, weapon.Range);

        target.y = transform.position.y;

        return target;
    }

    private ThrowLaunch BuildThrow(WeaponData weapon, Vector3 origin, Vector3 target, IProjectileListener listener)
    {
        return new ThrowLaunch
        {
            Weapon = weapon,
            Origin = origin,
            Target = target,
            HitMask = hitMask,
            Owner = ownerHealth,
            Listener = listener
        };
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitThrowRpc(int slot, Vector3 origin, Vector3 target)
    {
        if (slot < 0 || slot >= SLOT_COUNT || weapons[slot] == null || !ownerHealth.IsAlive || weapons[slot].ShotType != WeaponShotType.Thrown) return;

        WeaponData weapon = weapons[slot];
        float now = Time.time;
        float rateMultiplier = now < serverFireRateEndTime ? serverFireRateMultiplier : 1f;

        serverAmmo[slot].Tick(weapon, now, stats.ReloadSpeed);

        if (!serverAmmo[slot].TryConsume(now, weapon.FireInterval / rateMultiplier, SERVER_AMMO_TOLERANCE, SERVER_INTERVAL_SCALE)) return;

        Vector3 offset = target - transform.position;

        offset.y = 0f;

        if (offset.magnitude > weapon.Range + THROW_RANGE_TOLERANCE)
        {
            target = transform.position + offset.normalized * weapon.Range;
        }

        target.y = transform.position.y;
        origin = ResolveOrigin(origin);
        ThrowableSystem.Spawn(BuildThrow(weapon, origin, target, this));
        ThrowBroadcastRpc(slot, origin, target);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Server)]
    private void ThrowBroadcastRpc(int slot, Vector3 origin, Vector3 target)
    {
        characterAnimator.PlayShoot();

        WeaponData weapon = weapons[slot];

        if (weapon == null || IsServer) return;

        ThrowableSystem.Spawn(BuildThrow(weapon, origin, target, null));
    }

    private void RaiseMuzzleFlashes(int slot, WeaponData weapon, int pointMask, Vector3 aimDirection)
    {
        for (int point = 0; point < weapon.FirePointCount; point++)
        {
            if ((pointMask & (1 << point)) == 0) continue;

            RaiseShotFired(GetMuzzlePosition(slot, point), aimDirection, weapon);
        }
    }

    private Vector3 ResolveOrigin(Vector3 muzzle)
    {
        Vector3 center = shotOrigin.position;

        if ((muzzle - center).sqrMagnitude > MAX_ORIGIN_OFFSET * MAX_ORIGIN_OFFSET) return center;

        return Physics.Linecast(center, muzzle, hitMask, QueryTriggerInteraction.Ignore) ? center : muzzle;
    }

    private void LaunchProjectiles(WeaponData weapon, Vector3[] origins, Vector3 aimDirection, float[] yawOffsets, float rangeMultiplier, float damageMultiplier, int bounces, float bounceRange, IProjectileListener listener)
    {
        for (int i = 0; i < yawOffsets.Length; i++)
        {
            ProjectileSystem.Spawn(new ProjectileLaunch
            {
                Weapon = weapon,
                Origin = origins[i],
                Direction = Quaternion.AngleAxis(yawOffsets[i], Vector3.up) * aimDirection,
                Range = weapon.Range * rangeMultiplier,
                RangeMultiplier = rangeMultiplier,
                DamageMultiplier = damageMultiplier,
                SpeedMultiplier = stats.ProjectileSpeed,
                Bounces = bounces,
                BounceRange = bounceRange,
                HitMask = hitMask,
                Owner = ownerHealth,
                Listener = listener
            });
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitShotRpc(int slot, int pointMask, Vector3[] origins, Vector3 aimDirection, float[] yawOffsets)
    {
        if (slot < 0 || slot >= SLOT_COUNT || weapons[slot] == null || !ownerHealth.IsAlive) return;

        WeaponData weapon = weapons[slot];
        int pointCount = ShotBuilder.CountPoints(pointMask);
        int allowedPoints = weapon.Pattern == FirePattern.Simultaneous ? weapon.FirePointCount : 1;

        if (pointMask <= 0 || pointMask >= 1 << weapon.FirePointCount || pointCount > allowedPoints) return;
        if (yawOffsets.Length != pointCount * weapon.PelletCount || origins.Length != yawOffsets.Length) return;

        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude < 0.0001f) return;

        float now = Time.time;
        float rateMultiplier = now < serverFireRateEndTime ? serverFireRateMultiplier : 1f;

        serverAmmo[slot].Tick(weapon, now, stats.ReloadSpeed);

        if (!serverAmmo[slot].TryConsume(now, weapon.FireInterval / rateMultiplier, SERVER_AMMO_TOLERANCE, SERVER_INTERVAL_SCALE)) return;

        float shotMultiplier = 1f;

        if (now < serverChargedEndTime)
        {
            shotMultiplier = serverChargedMultiplier;
            serverChargedEndTime = 0f;
        }

        float maxYaw = weapon.SpreadAngle + weapon.MaxExtraSpread + weapon.GetMaxFirePointAngle() + SPREAD_TOLERANCE_DEGREES;

        for (int i = 0; i < yawOffsets.Length; i++)
        {
            yawOffsets[i] = Mathf.Clamp(yawOffsets[i], -maxYaw, maxYaw);
            origins[i] = ResolveOrigin(origins[i]);
        }

        aimDirection.Normalize();

        float rangeMultiplier = (now < serverFocusEndTime ? serverFocusRangeMultiplier : 1f) * stats.Range;
        int bounces = passives.Ricochet != null ? 1 : 0;
        float bounceRange = passives.Ricochet != null ? passives.Ricochet.SecondaryMagnitude : 0f;

        LaunchProjectiles(weapon, origins, aimDirection, yawOffsets, rangeMultiplier, shotMultiplier, bounces, bounceRange, this);
        ShotBroadcastRpc(slot, pointMask, origins, aimDirection, yawOffsets, rangeMultiplier, bounces, bounceRange);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Server)]
    private void ShotBroadcastRpc(int slot, int pointMask, Vector3[] origins, Vector3 aimDirection, float[] yawOffsets, float rangeMultiplier, int bounces, float bounceRange)
    {
        characterAnimator.PlayShoot();

        WeaponData weapon = weapons[slot];

        if (weapon == null) return;

        RaiseMuzzleFlashes(slot, weapon, pointMask, aimDirection);

        if (IsServer) return;

        LaunchProjectiles(weapon, origins, aimDirection, yawOffsets, rangeMultiplier, 1f, bounces, bounceRange, null);
    }

    public void HandleProjectileHit(ProjectileHit hit)
    {
        Health target = hit.Target;
        WeaponData weapon = hit.Weapon;
        float damageMultiplier = hit.DamageMultiplier * stats.DamageDealt;

        if (hit.IsBounce && passives.Ricochet != null)
        {
            damageMultiplier *= passives.Ricochet.Magnitude;
        }

        ApplyWeaken(target);

        if (!hit.IsBounce && IsBackstab(weapon, target))
        {
            int killDealt = target.TakeLethalHit(BACKSTAB_DAMAGE, ownerHealth);

            if (!target.IsAlive)
            {
                FloatingTextRpc(target.transform.position + Vector3.up * FLOATING_TEXT_HEIGHT, (int)FloatingTextKind.Backstab);
            }

            ApplyLifesteal(killDealt);
            ServerScavenge(weapon, target);

            return;
        }

        int dealt = target.TakeDamage(Mathf.RoundToInt(weapon.GetDamage(hit.FalloffDistance) * damageMultiplier), ownerHealth);

        ApplyLifesteal(dealt);
        ServerScavenge(weapon, target);
    }

    private void ServerScavenge(WeaponData weapon, Health target)
    {
        if (passives.Scavenger == null || target.IsAlive) return;

        int slot = weapon == weapons[0] ? 0 : 1;
        float rounds = passives.Scavenger.Magnitude;

        serverAmmo[slot].AddRounds(rounds, weapons[slot]);
        GrantAmmoRpc(slot, rounds);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void GrantAmmoRpc(int slot, float rounds)
    {
        ownerAmmo[slot].AddRounds(rounds, weapons[slot]);
    }

    private void ApplyLifesteal(int damageDealt)
    {
        if (passives.Lifesteal == null || damageDealt <= 0) return;

        ownerHealth.Heal(Mathf.Max(1, Mathf.RoundToInt(damageDealt * passives.Lifesteal.Magnitude)));
    }

    private bool IsBackstab(WeaponData weapon, Health target)
    {
        if (weapon.BackstabAngle <= 0f) return false;

        Vector3 toAttacker = transform.position - target.transform.position;
        Vector3 behind = -target.transform.forward;

        toAttacker.y = 0f;
        behind.y = 0f;

        if (toAttacker.sqrMagnitude < 0.0001f || behind.sqrMagnitude < 0.0001f) return false;

        return Vector3.Angle(behind, toAttacker) <= weapon.BackstabAngle;
    }

    private void ApplyWeaken(Health target)
    {
        if (passives.Weaken == null || !target.CanTakeDamageFrom(ownerHealth)) return;

        float seconds = passives.Weaken.Duration;

        target.ApplyWeaken(passives.Weaken.Magnitude, seconds);
        target.GetComponent<IKnockbackable>()?.ApplySlow(passives.Weaken.SecondaryMagnitude, seconds);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void FloatingTextRpc(Vector3 position, int kind)
    {
        FloatingTextRequested?.Invoke(position, (FloatingTextKind)kind);
    }
}
