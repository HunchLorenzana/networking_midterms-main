using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerRespawn : NetworkBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private float respawnDelay = 3f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        health.Died += HandleDied;
        health.Revived += HandleRevived;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        health.Died -= HandleDied;
        health.Revived -= HandleRevived;
    }

    private void HandleDied(Health killer)
    {
        StartCoroutine(RespawnAfterDelay());
    }

    private void HandleRevived()
    {
        StopAllCoroutines();
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        MatchManager match = MatchManager.Instance;

        if (match.Phase == MatchPhase.MatchOver) yield break;

        Transform spawn = match.GetSafestSpawn(health);

        motor.Teleport(spawn.position, spawn.rotation);
        health.ResetHealth(match.SpawnProtectionSeconds);
    }
}
