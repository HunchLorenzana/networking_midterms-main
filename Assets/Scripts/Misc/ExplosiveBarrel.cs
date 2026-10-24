using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Blocks shots like a crate, explodes after a few hits or when caught in any explosion, then respawns
public class ExplosiveBarrel : NetworkBehaviour
{
    private const int OVERLAP_BUFFER_SIZE = 32;
    private const float EFFECT_LIFETIME_SECONDS = 3f;

    private static readonly List<ExplosiveBarrel> all = new List<ExplosiveBarrel>();
    private static readonly Collider[] overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];
    private static readonly HashSet<Health> damaged = new HashSet<Health>();

    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Collider blockingCollider;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionScale = 1f;
    [SerializeField] private int hitsToExplode = 3;
    [SerializeField] private int damage = 60;
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float chainDelaySeconds = 0.15f;
    [Tooltip("0 = stays destroyed for the rest of the session")]
    [SerializeField] private float respawnSeconds = 20f;
    [SerializeField] private LayerMask damageMask = Physics.AllLayers;

    private readonly NetworkVariable<bool> isIntact = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int hitsTaken;
    private bool isDetonating;

    public override void OnNetworkSpawn()
    {
        all.Add(this);
        isIntact.OnValueChanged += HandleIntactChanged;
        ApplyIntact(isIntact.Value);
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        isIntact.OnValueChanged -= HandleIntactChanged;
    }

    public void ServerRegisterHit(Health attacker)
    {
        if (!IsServer || !isIntact.Value || isDetonating) return;

        hitsTaken++;

        if (hitsTaken >= hitsToExplode)
        {
            Explode(attacker);
        }
    }

    public void ServerDetonate(Health attacker, float delaySeconds)
    {
        if (!IsServer || !isIntact.Value || isDetonating) return;

        isDetonating = true;
        StartCoroutine(DetonateAfter(attacker, delaySeconds));
    }

    private IEnumerator DetonateAfter(Health attacker, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        Explode(attacker);
    }

    // The attacker gets the kill credit. Like rockets, it does not hurt the attacker or their teammates
    private void Explode(Health attacker)
    {
        Vector3 center = transform.position;

        isIntact.Value = false;
        isDetonating = false;
        hitsTaken = 0;
        ExplodeRpc(center);

        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, damageMask, QueryTriggerInteraction.Ignore);

        damaged.Clear();

        for (int i = 0; i < count; i++)
        {
            Health target = overlapBuffer[i].GetComponentInParent<Health>();

            if (target == null || target == attacker || !damaged.Add(target) || !target.CanTakeDamageFrom(attacker)) continue;

            target.TakeDamage(damage, attacker);
        }

        foreach (ExplosiveBarrel other in all)
        {
            if (other != this && (other.transform.position - center).sqrMagnitude <= radius * radius)
            {
                other.ServerDetonate(attacker, chainDelaySeconds);
            }
        }

        if (respawnSeconds > 0f)
        {
            StartCoroutine(RespawnAfterDelay());
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnSeconds);

        isIntact.Value = true;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ExplodeRpc(Vector3 center)
    {
        if (explosionPrefab == null) return;

        GameObject effect = Instantiate(explosionPrefab, center, Quaternion.identity);

        effect.transform.localScale = explosionPrefab.transform.localScale * explosionScale;
        Destroy(effect, EFFECT_LIFETIME_SECONDS);
    }

    private void HandleIntactChanged(bool previousValue, bool newValue)
    {
        ApplyIntact(newValue);
    }

    private void ApplyIntact(bool intact)
    {
        foreach (Renderer target in renderers)
        {
            target.enabled = intact;
        }

        blockingCollider.enabled = intact;
    }
}
