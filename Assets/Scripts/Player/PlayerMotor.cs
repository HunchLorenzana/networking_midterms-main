using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerMotor : NetworkBehaviour, ITeleportable, IKnockbackable
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private Health health;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float gravity = -25f;

    private const float GROUNDED_PUSH = -2f;
    private const float MIN_AIM_DISTANCE_SQR = 0.01f;
    private const float KNOCKBACK_SECONDS = 0.25f;

    private float slowMultiplier = 1f;
    private float slowEndTime;
    private float verticalVelocity;
    private Vector3 dashVelocity;
    private float dashEndTime;
    private float speedMultiplier = 1f;
    private float speedBoostEndTime;

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

    public void PlaceAtSpawn(Vector3 position, Quaternion rotation)
    {
        characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        characterController.enabled = true;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (IsOwner)
        {
            TeleportLocal(position, rotation);
            return;
        }

        TeleportOwnerRpc(position, rotation);
    }

    public void StartDash(Vector3 direction, float distance, float duration)
    {
        dashVelocity = direction.normalized * (distance / duration);
        dashEndTime = Time.time + duration;
    }

    public void ApplyKnockback(Vector3 direction, float distance, float slowMultiplier, float slowSeconds)
    {
        if (IsOwner)
        {
            ApplyKnockbackLocal(direction, distance, slowMultiplier, slowSeconds);
            return;
        }

        ApplyKnockbackOwnerRpc(direction, distance, slowMultiplier, slowSeconds);
    }

    public void ApplySlow(float slowMultiplier, float slowSeconds)
    {
        if (IsOwner)
        {
            ApplySlowLocal(slowMultiplier, slowSeconds);
            return;
        }

        ApplySlowOwnerRpc(slowMultiplier, slowSeconds);
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        speedBoostEndTime = Time.time + duration;
    }

    public void Simulate(Vector2 moveInput, Vector3 aimPoint, float weaponSpeedMultiplier, float deltaTime)
    {
        float speed = (Time.time < speedBoostEndTime ? moveSpeed * speedMultiplier : moveSpeed) * weaponSpeedMultiplier;

        if (Time.time < slowEndTime)
        {
            speed *= slowMultiplier;
        }

        Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f) * speed;

        if (Time.time < dashEndTime)
        {
            horizontal += dashVelocity;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = GROUNDED_PUSH;
        }

        verticalVelocity += gravity * deltaTime;
        characterController.Move((horizontal + Vector3.up * verticalVelocity) * deltaTime);

        Vector3 toAim = aimPoint - transform.position;
        toAim.y = 0f;

        if (toAim.sqrMagnitude < MIN_AIM_DISTANCE_SQR) return;

        transform.rotation = Quaternion.LookRotation(toAim);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void TeleportOwnerRpc(Vector3 position, Quaternion rotation)
    {
        TeleportLocal(position, rotation);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ApplyKnockbackOwnerRpc(Vector3 direction, float distance, float slowMultiplier, float slowSeconds)
    {
        ApplyKnockbackLocal(direction, distance, slowMultiplier, slowSeconds);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ApplySlowOwnerRpc(float slowMultiplier, float slowSeconds)
    {
        ApplySlowLocal(slowMultiplier, slowSeconds);
    }

    private void ApplySlowLocal(float slowMultiplier, float slowSeconds)
    {
        if (!health.IsAlive) return;

        this.slowMultiplier = slowMultiplier;
        slowEndTime = Time.time + slowSeconds;
    }

    private void ApplyKnockbackLocal(Vector3 direction, float distance, float slowMultiplier, float slowSeconds)
    {
        if (!health.IsAlive) return;

        StartDash(direction, distance, KNOCKBACK_SECONDS);
        this.slowMultiplier = slowMultiplier;
        slowEndTime = Time.time + slowSeconds;
    }

    private void TeleportLocal(Vector3 position, Quaternion rotation)
    {
        slowEndTime = 0f;
        characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        networkTransform.Teleport(position, rotation, transform.localScale);
        characterController.enabled = health.IsAlive;
        verticalVelocity = 0f;
        dashEndTime = 0f;
        speedBoostEndTime = 0f;
    }

    private void HandleDied(Health killer)
    {
        characterController.enabled = false;
    }

    private void HandleRevived()
    {
        characterController.enabled = true;
    }
}
