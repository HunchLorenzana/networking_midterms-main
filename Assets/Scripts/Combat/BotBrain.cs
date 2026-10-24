using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class BotBrain : NetworkBehaviour, ITeleportable, IKnockbackable, IProjectileListener
{
    private const float KNOCKBACK_SECONDS = 0.25f;

    private const float RETARGET_SECONDS = 0.5f;
    private const float MAX_AIM_ANGLE = 12f;
    private const float TARGET_CHEST_HEIGHT = 1.1f;
    private const float MAX_PREFERRED_RANGE = 16f;

    [SerializeField] private Health health;
    [SerializeField] private MatchParticipant participant;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private Transform shotOrigin;
    [SerializeField] private HeldWeaponVisual heldVisual;
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private LoadoutCatalog catalog;
    [SerializeField] private LayerMask sightMask = ~0;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 420f;
    [Tooltip("Overwritten by the BotRoster difficulty when the bot spawns")]
    [SerializeField] private float reactionSeconds = 0.4f;
    [Tooltip("Overwritten by the BotRoster difficulty when the bot spawns")]
    [SerializeField] private float aimErrorDegrees = 5f;
    [Tooltip("Overwritten by the BotRoster preset when the bot spawns")]
    [SerializeField] private float preferredRangeFraction = 0.4f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float respawnDelay = 3f;

    private readonly WeaponData[] weapons = new WeaponData[2];
    private readonly WeaponAmmo[] ammo = { new WeaponAmmo(), new WeaponAmmo() };
    private readonly int[] shotCounters = new int[2];

    private Health target;
    private readonly NetworkVariable<int> primaryId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> secondaryId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> activeSlot = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector3 knockbackVelocity;
    private float knockbackEndTime;
    private float slowMultiplier = 1f;
    private float slowEndTime;
    private int currentSlot;
    private float nextRetargetTime;
    private float nextShotAllowedTime;
    private float nextStrafeFlipTime;
    private float strafeDirection = 1f;

    public int ActiveSlot => activeSlot.Value;

    public void ApplyKnockback(Vector3 direction, float distance, float slowMultiplier, float slowSeconds)
    {
        knockbackVelocity = direction.normalized * (distance / KNOCKBACK_SECONDS);
        knockbackEndTime = Time.time + KNOCKBACK_SECONDS;
        this.slowMultiplier = slowMultiplier;
        slowEndTime = Time.time + slowSeconds;
    }

    public void ApplySlow(float slowMultiplier, float slowSeconds)
    {
        this.slowMultiplier = slowMultiplier;
        slowEndTime = Time.time + slowSeconds;
    }

    public WeaponData GetWeapon(int slot)
    {
        int id = slot == 0 ? primaryId.Value : secondaryId.Value;

        return id <= 0 ? null : catalog.GetWeapon(id);
    }

    public void ServerInit(string botName, BotRoster.BotPreset preset, BotRoster.BotDifficulty difficulty)
    {
        weapons[0] = catalog.GetWeapon(preset.PrimaryWeaponId) ?? catalog.GetWeaponAt(0);
        weapons[1] = catalog.GetWeapon(preset.SecondaryWeaponId) ?? catalog.GetWeaponAt(1);

        // A preset naming a missing weapon, or the same one twice, still gives two different weapons
        if (weapons[1] == weapons[0])
        {
            weapons[1] = catalog.GetWeaponAt(weapons[0] == catalog.GetWeaponAt(0) ? 1 : 0);
        }

        preferredRangeFraction = preset.PreferredRangeFraction;
        reactionSeconds = difficulty.ReactionSeconds;
        aimErrorDegrees = difficulty.AimErrorDegrees;
        primaryId.Value = weapons[0].Id;
        secondaryId.Value = weapons[1].Id;
        RefillAmmo();
        participant.ServerSetName(botName);
        participant.ServerSetCharacter(Random.Range(0, participant.CharacterCount));
        participant.ServerSetBotLabel(weapons[0].DisplayName + " + " + weapons[1].DisplayName);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        networkTransform.Teleport(position, rotation, transform.localScale);
        characterController.enabled = health.IsAlive;
    }

    void OnEnable()
    {
        health.Died += HandleDied;
        health.Revived += HandleRevived;
    }

    void OnDisable()
    {
        health.Died -= HandleDied;
        health.Revived -= HandleRevived;
    }

    void Update()
    {
        if (!IsServer || weapons[0] == null || !health.IsAlive || !MatchManager.Instance.CanPlayersAct) return;

        float now = Time.time;

        for (int slot = 0; slot < weapons.Length; slot++)
        {
            ammo[slot].Tick(weapons[slot], now);
        }

        if (now >= nextRetargetTime)
        {
            Health found = FindTarget();

            if (found != target)
            {
                nextShotAllowedTime = now + reactionSeconds;
            }

            target = found;
            nextRetargetTime = now + RETARGET_SECONDS;
        }

        Vector3 velocity = Vector3.up * gravity;

        if (target != null && target.IsAlive)
        {
            velocity += Engage(now) * (now < slowEndTime ? slowMultiplier : 1f);
        }

        if (now < knockbackEndTime)
        {
            velocity += knockbackVelocity;
        }

        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector3 Engage(float now)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        Vector3 forward = distance > 0.01f ? toTarget / distance : transform.forward;

        currentSlot = ChooseSlot(distance);
        activeSlot.Value = currentSlot;

        float preferredRange = Mathf.Min(weapons[currentSlot].Range * preferredRangeFraction, MAX_PREFERRED_RANGE);
        Vector3 move = Vector3.zero;

        if (distance > preferredRange + 2f)
        {
            move += forward;
        }
        else if (distance < preferredRange - 2f)
        {
            move -= forward;
        }

        if (now >= nextStrafeFlipTime)
        {
            strafeDirection = -strafeDirection;
            nextStrafeFlipTime = now + Random.Range(1f, 2.5f);
        }

        Vector3 strafe = Vector3.Cross(Vector3.up, forward) * strafeDirection;

        move += strafe * 0.6f;

        if (move.sqrMagnitude > 0.01f && Physics.Raycast(shotOrigin.position, move.normalized, 1.2f, sightMask, QueryTriggerInteraction.Ignore))
        {
            strafeDirection = -strafeDirection;
            move = -strafe;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward), turnSpeed * Time.deltaTime);
        TryShoot(now, forward, distance);

        return Vector3.ClampMagnitude(move, 1f) * moveSpeed;
    }

    private void TryShoot(float now, Vector3 forward, float distance)
    {
        WeaponData weapon = weapons[currentSlot];

        if (now < nextShotAllowedTime || distance > weapon.Range || Vector3.Angle(transform.forward, forward) > MAX_AIM_ANGLE) return;

        Vector3 targetChest = target.transform.position + Vector3.up * TARGET_CHEST_HEIGHT;

        if (Physics.Linecast(shotOrigin.position, targetChest, out RaycastHit sight, sightMask, QueryTriggerInteraction.Ignore) && sight.collider.GetComponentInParent<Health>() != target) return;
        if (!ammo[currentSlot].TryConsume(now, weapon.FireInterval, 0f, 1f)) return;

        if (weapon.ShotType == WeaponShotType.Thrown)
        {
            ThrowAt(currentSlot, weapon);

            return;
        }

        float spread = weapon.SpreadAngle + aimErrorDegrees;
        int pointMask = weapon.GetFirePointMask(shotCounters[currentSlot]);
        int projectileCount = ShotBuilder.CountPoints(pointMask) * weapon.PelletCount;
        Vector3[] origins = new Vector3[projectileCount];
        float[] yawOffsets = new float[projectileCount];
        Vector3 direction = transform.forward;

        shotCounters[currentSlot]++;
        ShotBuilder.Build(weapon, currentSlot, pointMask, spread, heldVisual, shotOrigin.position, origins, yawOffsets);
        LaunchProjectiles(weapon, origins, direction, yawOffsets, this);
        BotShotRpc(currentSlot, pointMask, origins, direction, yawOffsets);
    }

    private void ThrowAt(int slot, WeaponData weapon)
    {
        Vector3 origin = shotOrigin.position;
        Vector3 targetPoint = target.transform.position;

        ThrowableSystem.Spawn(BuildThrow(weapon, origin, targetPoint, this));
        BotThrowRpc(slot, origin, targetPoint);
    }

    private ThrowLaunch BuildThrow(WeaponData weapon, Vector3 origin, Vector3 targetPoint, IProjectileListener listener)
    {
        return new ThrowLaunch
        {
            Weapon = weapon,
            Origin = origin,
            Target = targetPoint,
            HitMask = sightMask,
            Owner = health,
            Listener = listener
        };
    }

    public void HandleProjectileHit(ProjectileHit hit)
    {
        hit.Target.TakeDamage(hit.Weapon.GetDamage(hit.FalloffDistance), health);
    }

    private void LaunchProjectiles(WeaponData weapon, Vector3[] origins, Vector3 direction, float[] yawOffsets, IProjectileListener listener)
    {
        for (int i = 0; i < yawOffsets.Length; i++)
        {
            ProjectileSystem.Spawn(new ProjectileLaunch
            {
                Weapon = weapon,
                Origin = origins[i],
                Direction = Quaternion.AngleAxis(yawOffsets[i], Vector3.up) * direction,
                Range = weapon.Range,
                RangeMultiplier = 1f,
                DamageMultiplier = 1f,
                Bounces = 0,
                BounceRange = 0f,
                HitMask = sightMask,
                Owner = health,
                Listener = listener
            });
        }
    }

    private int ChooseSlot(float distance)
    {
        int best = currentSlot;
        float bestScore = float.MaxValue;

        for (int slot = 0; slot < weapons.Length; slot++)
        {
            if (ammo[slot].Ammo < 1f) continue;

            float score = Mathf.Abs(distance - weapons[slot].Range * preferredRangeFraction);

            if (score >= bestScore) continue;

            bestScore = score;
            best = slot;
        }

        return best;
    }

    private Health FindTarget()
    {
        Health nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (MatchParticipant other in MatchParticipant.All)
        {
            Health otherHealth = other.Health;
            bool isTeammate = health.TeamId >= 0 && otherHealth.TeamId == health.TeamId;

            if (otherHealth == health || isTeammate || !otherHealth.IsAlive) continue;

            if (PlayerBushVisibility.IsHiddenFrom(otherHealth, transform.position, health.TeamId)) continue;

            float distance = (otherHealth.transform.position - transform.position).sqrMagnitude;

            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = otherHealth;
        }

        return nearest;
    }

    private void RefillAmmo()
    {
        for (int slot = 0; slot < weapons.Length; slot++)
        {
            ammo[slot].Reset(weapons[slot], Time.time);
        }
    }

    private void HandleDied(Health killer)
    {
        characterController.enabled = false;

        if (IsServer)
        {
            target = null;
            StartCoroutine(RespawnAfterDelay());
        }
    }

    private void HandleRevived()
    {
        characterController.enabled = true;

        if (!IsServer) return;

        StopAllCoroutines();
        target = null;

        if (weapons[0] != null)
        {
            RefillAmmo();
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        MatchManager match = MatchManager.Instance;

        if (match.Phase != MatchPhase.Playing) yield break;

        Transform spawn = match.GetSafestSpawn(health);

        Teleport(spawn.position + Vector3.up * 0.05f, spawn.rotation);
        health.ResetHealth(match.SpawnProtectionSeconds);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void BotThrowRpc(int slot, Vector3 origin, Vector3 targetPoint)
    {
        characterAnimator.PlayShoot();

        WeaponData weapon = GetWeapon(slot);

        if (weapon == null || IsServer) return;

        ThrowableSystem.Spawn(BuildThrow(weapon, origin, targetPoint, null));
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void BotShotRpc(int slot, int pointMask, Vector3[] origins, Vector3 direction, float[] yawOffsets)
    {
        characterAnimator.PlayShoot();

        WeaponData weapon = GetWeapon(slot);

        if (weapon == null) return;

        for (int point = 0; point < weapon.FirePointCount; point++)
        {
            if ((pointMask & (1 << point)) == 0) continue;

            PlayerWeapon.RaiseShotFired(heldVisual.TryGetMuzzle(slot, point, out Vector3 muzzle) ? muzzle : shotOrigin.position, direction, weapon);
        }

        if (IsServer) return;

        LaunchProjectiles(weapon, origins, direction, yawOffsets, null);
    }
}
