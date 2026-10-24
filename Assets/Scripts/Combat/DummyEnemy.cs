using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class DummyEnemy : NetworkBehaviour, ITeleportable, IKnockbackable
{
    private const float KNOCKBACK_SECONDS = 0.25f;

    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float stopDistance = 3f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float respawnDelay = 3f;

    private const float PATROL_ARRIVE_DISTANCE = 0.3f;

    private Vector3 knockbackVelocity;
    private float knockbackEndTime;
    private float slowMultiplier = 1f;
    private float slowEndTime;
    private bool isChasing;
    private bool isPatrolling;
    private bool hasHome;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 patrolFirst;
    private Vector3 patrolSecond;
    private Vector3 patrolTarget;

    public Health Health => health;

    public void ApplySlow(float slowMultiplier, float slowSeconds)
    {
        this.slowMultiplier = slowMultiplier;
        slowEndTime = Time.time + slowSeconds;
    }

    public void ApplyKnockback(Vector3 direction, float distance, float slowMultiplier, float slowSeconds)
    {
        knockbackVelocity = direction.normalized * (distance / KNOCKBACK_SECONDS);
        knockbackEndTime = Time.time + KNOCKBACK_SECONDS;
    }

    public void Init(bool shouldChase)
    {
        isChasing = shouldChase;
    }

    public void SetPatrol(Vector3 first, Vector3 second)
    {
        isPatrolling = true;
        patrolFirst = first;
        patrolSecond = second;
        patrolTarget = second;
    }

    public void SetHome(Vector3 position, Quaternion rotation)
    {
        hasHome = true;
        homePosition = position;
        homeRotation = rotation;
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
        if (!IsServer || !health.IsAlive || !MatchManager.Instance.CanPlayersAct) return;

        Vector3 velocity = Vector3.up * gravity;
        Transform target = isChasing ? FindNearestPlayer() : null;

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude > stopDistance)
            {
                velocity += toTarget.normalized * moveSpeed;
            }

            if (toTarget.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget);
            }
        }

        if (target == null && isPatrolling)
        {
            velocity += GetPatrolVelocity();
        }

        if (Time.time < slowEndTime)
        {
            velocity.x *= slowMultiplier;
            velocity.z *= slowMultiplier;
        }

        if (Time.time < knockbackEndTime)
        {
            velocity += knockbackVelocity;
        }

        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector3 GetPatrolVelocity()
    {
        Vector3 toTarget = patrolTarget - transform.position;

        toTarget.y = 0f;

        if (toTarget.magnitude < PATROL_ARRIVE_DISTANCE)
        {
            patrolTarget = patrolTarget == patrolFirst ? patrolSecond : patrolFirst;
            return Vector3.zero;
        }

        Vector3 direction = toTarget.normalized;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);

        return direction * patrolSpeed;
    }

    private Transform FindNearestPlayer()
    {
        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (PlayerController player in PlayerController.All)
        {
            if (!player.Health.IsAlive) continue;

            float distance = (player.transform.position - transform.position).sqrMagnitude;

            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = player.transform;
        }

        return nearest;
    }

    private void HandleDied(Health killer)
    {
        characterController.enabled = false;

        if (IsServer)
        {
            StartCoroutine(RespawnAfterDelay());
        }
    }

    private void HandleRevived()
    {
        characterController.enabled = true;

        if (IsServer)
        {
            StopAllCoroutines();
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        MatchManager match = MatchManager.Instance;

        if (match.Phase == MatchPhase.MatchOver) yield break;

        if (hasHome)
        {
            Teleport(homePosition, homeRotation);
            patrolTarget = patrolSecond;
        }
        else
        {
            Transform spawn = match.GetSafestSpawn(health);

            Teleport(spawn.position + Vector3.up * 0.05f, spawn.rotation);
        }

        health.ResetHealth(hasHome ? 0f : match.SpawnProtectionSeconds);
    }
}
