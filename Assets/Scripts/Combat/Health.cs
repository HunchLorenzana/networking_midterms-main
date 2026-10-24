using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    private static readonly List<Health> allHealth = new List<Health>();

    public static IReadOnlyList<Health> All => allHealth;

    public event Action<int> Damaged;
    public event Action<Health> Died;
    public event Action Revived;
    public static event Action<Health, bool> WeakenedChanged;
    public static event Action<Health, bool> ProtectionChanged;

    public event Action ShieldChanged;

    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool isTrainingTarget;

    private readonly NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isShieldActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isWeakened = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isProtected = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly Dictionary<Health, float> recentAttackers = new Dictionary<Health, float>();

    private int shieldRemaining;
    private float shieldEndTime;
    private float protectedUntil;
    private float weakenMultiplier = 1f;
    private float weakenEndTime;
    private float maxHealthMultiplier = 1f;

    public int TeamId { get; set; } = -1;
    public float DamageTakenMultiplier { get; set; } = 1f;
    public bool IsTrainingTarget => isTrainingTarget;
    public int MaxHealth => Mathf.Max(1, Mathf.RoundToInt(maxHealth * maxHealthMultiplier));
    public int CurrentHealth => currentHealth.Value;
    public bool IsAlive => currentHealth.Value > 0;
    public float Fraction => (float)currentHealth.Value / MaxHealth;
    public bool HasShield => isShieldActive.Value;

    public override void OnNetworkSpawn()
    {
        allHealth.Add(this);

        if (IsServer)
        {
            currentHealth.Value = MaxHealth;
            return;
        }

        currentHealth.OnValueChanged += HandleHealthChanged;
        isShieldActive.OnValueChanged += HandleShieldChanged;
        isWeakened.OnValueChanged += HandleWeakenedChanged;
        isProtected.OnValueChanged += HandleProtectionChanged;

        if (currentHealth.Value <= 0)
        {
            Died?.Invoke(null);
        }

        if (isShieldActive.Value)
        {
            ShieldChanged?.Invoke();
        }

        if (isWeakened.Value)
        {
            WeakenedChanged?.Invoke(this, true);
        }

        if (isProtected.Value)
        {
            ProtectionChanged?.Invoke(this, true);
        }
    }

    public override void OnNetworkDespawn()
    {
        allHealth.Remove(this);
        currentHealth.OnValueChanged -= HandleHealthChanged;
        isShieldActive.OnValueChanged -= HandleShieldChanged;
        isWeakened.OnValueChanged -= HandleWeakenedChanged;
        isProtected.OnValueChanged -= HandleProtectionChanged;
    }

    void Update()
    {
        if (!IsServer) return;

        if (isWeakened.Value && Time.time >= weakenEndTime)
        {
            ClearWeaken();
        }

        if (isProtected.Value && Time.time >= protectedUntil)
        {
            SetProtected(false);
        }

        if (shieldRemaining <= 0 || Time.time < shieldEndTime) return;

        ClearShield();
    }

    public void SetMaxHealthMultiplier(float multiplier)
    {
        bool wasFull = currentHealth.Value >= MaxHealth;

        maxHealthMultiplier = multiplier;

        if (!IsServer || !IsSpawned || currentHealth.Value <= 0) return;

        currentHealth.Value = wasFull ? MaxHealth : Mathf.Min(currentHealth.Value, MaxHealth);
    }

    public void ApplyShield(int amount, float duration)
    {
        if (!IsServer) return;

        shieldRemaining = amount;
        shieldEndTime = Time.time + duration;
        isShieldActive.Value = true;
        ShieldChanged?.Invoke();
    }

    public int TakeDamage(int amount, Health attacker)
    {
        return ApplyDamage(amount, attacker, true);
    }

    public int TakeLethalHit(int lethalDamage, Health attacker)
    {
        if (shieldRemaining > 0)
        {
            ApplyDamage(shieldRemaining, attacker, false);

            return 0;
        }

        return ApplyDamage(lethalDamage, attacker, true);
    }

    public void Heal(int amount)
    {
        if (!IsServer || !IsAlive || amount <= 0) return;

        currentHealth.Value = Mathf.Min(MaxHealth, currentHealth.Value + amount);
    }

    public bool CanTakeDamageFrom(Health attacker)
    {
        if (!IsServer || !IsAlive || Time.time < protectedUntil) return false;
        if (attacker != null && attacker != this && TeamId >= 0 && attacker.TeamId == TeamId) return false;

        return isTrainingTarget || MatchManager.Instance == null || MatchManager.Instance.Phase != MatchPhase.Lobby;
    }

    public void ApplyWeaken(float damageTakenMultiplier, float seconds)
    {
        if (!IsServer) return;

        weakenMultiplier = damageTakenMultiplier;
        weakenEndTime = Time.time + seconds;

        if (!isWeakened.Value)
        {
            isWeakened.Value = true;
            WeakenedChanged?.Invoke(this, true);
        }
    }

    private void ClearWeaken()
    {
        weakenEndTime = 0f;

        if (!isWeakened.Value) return;

        isWeakened.Value = false;
        WeakenedChanged?.Invoke(this, false);
    }

    private void HandleWeakenedChanged(bool previousValue, bool newValue)
    {
        WeakenedChanged?.Invoke(this, newValue);
    }

    private void SetProtected(bool value)
    {
        if (isProtected.Value == value) return;

        isProtected.Value = value;
        ProtectionChanged?.Invoke(this, value);
    }

    private void HandleProtectionChanged(bool previousValue, bool newValue)
    {
        ProtectionChanged?.Invoke(this, newValue);
    }

    private int ApplyDamage(int amount, Health attacker, bool usesMultiplier)
    {
        if (amount <= 0 || !CanTakeDamageFrom(attacker)) return 0;

        if (usesMultiplier)
        {
            float weaken = Time.time < weakenEndTime ? weakenMultiplier : 1f;

            amount = Mathf.Max(1, Mathf.RoundToInt(amount * DamageTakenMultiplier * weaken));
        }

        if (shieldRemaining > 0)
        {
            int absorbed = Mathf.Min(amount, shieldRemaining);

            shieldRemaining -= absorbed;
            amount -= absorbed;

            if (shieldRemaining == 0)
            {
                ClearShield();
            }

            if (amount <= 0) return 0;
        }

        int dealt = Mathf.Min(amount, currentHealth.Value);

        currentHealth.Value -= dealt;

        if (attacker != null && attacker != this)
        {
            recentAttackers[attacker] = Time.time;
        }

        Damaged?.Invoke(dealt);

        if (currentHealth.Value > 0) return dealt;

        ClearShield();
        SetProtected(false);
        ClearWeaken();
        Died?.Invoke(attacker);

        return dealt;
    }

    public void CollectAssistants(Health killer, float windowSeconds, List<Health> assistants)
    {
        foreach (KeyValuePair<Health, float> attacker in recentAttackers)
        {
            if (attacker.Key != null && attacker.Key != killer && Time.time - attacker.Value <= windowSeconds)
            {
                assistants.Add(attacker.Key);
            }
        }

        recentAttackers.Clear();
    }

    public void ResetHealth(float protectionSeconds = 0f)
    {
        if (!IsServer) return;

        recentAttackers.Clear();
        currentHealth.Value = MaxHealth;
        protectedUntil = Time.time + protectionSeconds;
        SetProtected(protectionSeconds > 0f);
        ClearWeaken();
        ClearShield();
        Revived?.Invoke();
    }

    private void ClearShield()
    {
        bool hadShield = isShieldActive.Value;

        shieldRemaining = 0;
        isShieldActive.Value = false;

        if (hadShield)
        {
            ShieldChanged?.Invoke();
        }
    }

    private void HandleHealthChanged(int previousValue, int newValue)
    {
        if (newValue < previousValue)
        {
            Damaged?.Invoke(previousValue - newValue);
        }

        if (previousValue > 0 && newValue <= 0)
        {
            Died?.Invoke(null);
        }
        else if (previousValue <= 0 && newValue > 0)
        {
            Revived?.Invoke();
        }
    }

    private void HandleShieldChanged(bool previousValue, bool newValue)
    {
        ShieldChanged?.Invoke();
    }
}
