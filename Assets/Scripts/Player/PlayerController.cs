using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    private static readonly List<PlayerController> allPlayers = new List<PlayerController>();

    public static IReadOnlyList<PlayerController> All => allPlayers;
    public static PlayerController Local { get; private set; }

    public static event Action<PlayerController> LocalSpawned;
    public static event Action LocalDespawned;

    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private Health health;
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private AbilityController abilities;
    [SerializeField] private PlayerLoadout loadout;
    [SerializeField] private MatchParticipant participant;

    public PlayerInputReader InputReader => inputReader;
    public Health Health => health;
    public PlayerWeapon Weapon => weapon;
    public AbilityController Abilities => abilities;
    public PlayerLoadout Loadout => loadout;
    public MatchParticipant Participant => participant;

    public override void OnNetworkSpawn()
    {
        allPlayers.Add(this);

        if (!IsOwner) return;

        Local = this;
        inputReader.Activate(Camera.main);

        Transform spawn = MatchManager.Instance.GetSafestSpawn(health);

        motor.PlaceAtSpawn(spawn.position + Vector3.up * 0.05f, spawn.rotation);
        LocalSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        allPlayers.Remove(this);

        if (Local != this) return;

        Local = null;
        inputReader.Deactivate();
        LocalDespawned?.Invoke();
    }

    void Update()
    {
        if (!IsOwner || !health.IsAlive) return;

        motor.Simulate(inputReader.MoveInput, inputReader.AimPoint, weapon.MoveSpeedMultiplier, Time.deltaTime);
    }
}
