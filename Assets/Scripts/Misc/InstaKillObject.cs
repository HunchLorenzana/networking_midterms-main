using Unity.Netcode;
using UnityEngine;

public class InstaKillObject : MonoBehaviour
{
    [Header("Kill Settings")]
    [SerializeField] private string killerName = "The Train";

    [SerializeField]
    private string[] customKillMessages =
    {
        "{killer} crushed {victim}",
        "{killer} wrecked {victim}",
        "{victim} got flattened by {killer}",
        "{victim} was hit by {killer}"
    };

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        Health health = other.GetComponentInParent<Health>();

        if (health == null || !health.IsAlive)
            return;

        MatchParticipant victim = MatchParticipant.FindByHealth(health);

        health.TakeLethalHit(health.MaxHealth, null);

        if (victim == null || MatchManager.Instance == null)
            return;

        if (customKillMessages == null || customKillMessages.Length == 0)
            return;

        string message = customKillMessages[
            Random.Range(0, customKillMessages.Length)
        ];

        MatchManager.Instance.RegisterCustomKill(
            killerName,
            message,
            victim
        );
    }
}