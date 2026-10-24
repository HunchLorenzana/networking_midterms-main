using Unity.Netcode;
using UnityEngine;

public class AbilityController : NetworkBehaviour
{
    public const int SLOT_COUNT = 2;

    private const float SERVER_COOLDOWN_TOLERANCE = 0.3f;

    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Health health;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private AbilityVfx vfx;

    private readonly AbilityData[] abilities = new AbilityData[SLOT_COUNT];
    private readonly float[] ownerReadyTimes = new float[SLOT_COUNT];
    private readonly float[] serverReadyTimes = new float[SLOT_COUNT];

    private float serverMendEndTime;
    private float serverMendFraction;

    public AbilityData GetAbility(int slot)
    {
        return abilities[slot];
    }

    public float GetCooldownRemaining(int slot)
    {
        return Mathf.Max(0f, ownerReadyTimes[slot] - Time.time);
    }

    public float GetReadyFraction(int slot)
    {
        AbilityData ability = abilities[slot];

        if (ability == null || ability.Cooldown <= 0f) return 1f;

        return 1f - Mathf.Clamp01(GetCooldownRemaining(slot) / ability.Cooldown);
    }

    void OnEnable()
    {
        health.Revived += ResetCooldowns;
        health.Damaged += HandleDamaged;
    }

    void OnDisable()
    {
        health.Revived -= ResetCooldowns;
        health.Damaged -= HandleDamaged;
    }

    void Update()
    {
        if (IsServer)
        {
            TickMend();
        }

        if (!IsOwner || !health.IsAlive) return;

        for (int slot = 0; slot < SLOT_COUNT; slot++)
        {
            if (inputReader.IsAbilityPressed(slot))
            {
                TryActivate(slot);
            }
        }
    }

    public void SetAbilities(AbilityData first, AbilityData second)
    {
        abilities[0] = first;
        abilities[1] = second;
        ResetCooldowns();
    }

    private void ResetCooldowns()
    {
        System.Array.Clear(ownerReadyTimes, 0, SLOT_COUNT);
        System.Array.Clear(serverReadyTimes, 0, SLOT_COUNT);
        serverMendEndTime = 0f;
    }

    private void HandleDamaged(int amount)
    {
        serverMendEndTime = 0f;
    }

    private void TickMend()
    {
        if (serverMendEndTime <= 0f || Time.time < serverMendEndTime) return;

        serverMendEndTime = 0f;

        if (health.IsAlive)
        {
            health.Heal(Mathf.RoundToInt(health.MaxHealth * serverMendFraction));
        }
    }

    private void TryActivate(int slot)
    {
        AbilityData ability = abilities[slot];

        if (ability == null || Time.time < ownerReadyTimes[slot]) return;

        ownerReadyTimes[slot] = Time.time + ability.Cooldown;
        PredictOnOwner(ability);
        ActivateAbilityRpc(slot);
    }

    private void PredictOnOwner(AbilityData ability)
    {
        vfx.Play(ability);

        switch (ability.Type)
        {
            case AbilityType.Dash:
                Vector2 moveInput = inputReader.MoveInput;
                Vector3 dashDirection = moveInput.sqrMagnitude > 0.01f ? new Vector3(moveInput.x, 0f, moveInput.y) : transform.forward;

                motor.StartDash(dashDirection, ability.Magnitude, ability.Duration);
                break;
            case AbilityType.Adrenaline:
                motor.ApplySpeedBoost(1f + ability.Magnitude, ability.Duration);
                weapon.ApplyFireRateBoost(1f + ability.SecondaryMagnitude, ability.Duration);
                break;
            case AbilityType.Focus:
                weapon.ActivateFocus(ability.Duration, ability.Magnitude, ability.SecondaryMagnitude);
                break;
            case AbilityType.Mend:
                motor.ApplySpeedBoost(ability.SecondaryMagnitude, ability.Duration);
                break;
        }
    }

    private void ServerAreaKnockback(AbilityData ability, bool isPull)
    {
        const float PULL_STOP_DISTANCE = 1.5f;
        const float CHEST_HEIGHT = 1f;

        bool isLobby = MatchManager.Instance != null && MatchManager.Instance.Phase == MatchPhase.Lobby;

        foreach (Health other in Health.All)
        {
            if (other == health || !other.IsAlive) continue;
            if (health.TeamId >= 0 && other.TeamId == health.TeamId) continue;
            if (isLobby && !other.IsTrainingTarget) continue;

            Vector3 offset = other.transform.position - transform.position;

            offset.y = 0f;

            float flatDistance = offset.magnitude;

            if (flatDistance > ability.Magnitude) continue;
            if (isPull && !HasClearLine(other, CHEST_HEIGHT)) continue;

            Vector3 away = flatDistance > 0.1f ? offset / flatDistance : transform.forward;
            float distance = isPull ? Mathf.Min(ability.SecondaryMagnitude, Mathf.Max(0f, flatDistance - PULL_STOP_DISTANCE)) : ability.SecondaryMagnitude;
            IKnockbackable knockbackable = other.GetComponent<IKnockbackable>();

            knockbackable?.ApplyKnockback(isPull ? -away : away, distance, ability.TertiaryMagnitude, ability.Duration);
        }
    }

    private bool HasClearLine(Health other, float height)
    {
        Vector3 from = transform.position + Vector3.up * height;
        Vector3 to = other.transform.position + Vector3.up * height;

        if (!Physics.Linecast(from, to, out RaycastHit blocker, ~0, QueryTriggerInteraction.Ignore)) return true;

        return blocker.collider.GetComponentInParent<Health>() == other;
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Server)]
    private void PlayAbilityEffectRpc(int slot)
    {
        AbilityData ability = abilities[slot];

        if (ability == null) return;

        vfx.Play(ability);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ActivateAbilityRpc(int slot)
    {
        if (slot < 0 || slot >= SLOT_COUNT || !health.IsAlive) return;

        AbilityData ability = abilities[slot];

        if (ability == null || Time.time < serverReadyTimes[slot] - SERVER_COOLDOWN_TOLERANCE) return;

        serverReadyTimes[slot] = Time.time + ability.Cooldown;
        PlayAbilityEffectRpc(slot);

        switch (ability.Type)
        {
            case AbilityType.Shield:
                health.ApplyShield(Mathf.RoundToInt(ability.Magnitude), ability.Duration);
                break;
            case AbilityType.Adrenaline:
                weapon.ServerApplyFireRateBoost(1f + ability.SecondaryMagnitude, ability.Duration);
                break;
            case AbilityType.ChargedShot:
                weapon.ServerActivateChargedShot(ability.Duration, ability.Magnitude);
                break;
            case AbilityType.Focus:
                weapon.ServerActivateFocus(ability.Duration, ability.SecondaryMagnitude);
                break;
            case AbilityType.Shockwave:
                ServerAreaKnockback(ability, false);
                break;
            case AbilityType.Pull:
                ServerAreaKnockback(ability, true);
                break;
            case AbilityType.Mend:
                serverMendEndTime = Time.time + ability.Duration;
                serverMendFraction = ability.Magnitude;
                break;
        }
    }
}
