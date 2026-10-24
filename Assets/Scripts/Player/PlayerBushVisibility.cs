using System.Collections.Generic;
using UnityEngine;

// A character standing in a bush is hidden from enemies unless they are close, or it recently shot or was hit
public class PlayerBushVisibility : MonoBehaviour
{
    private const int OVERLAP_BUFFER_SIZE = 32;
    private const float FEET_HEIGHT = 0.3f;
    // Shot events only carry the muzzle position, so the shooter is the character within this distance of it
    private const float SHOT_OWNER_RADIUS = 1.2f;

    private static readonly Dictionary<Health, PlayerBushVisibility> byHealth = new Dictionary<Health, PlayerBushVisibility>();
    private static readonly Collider[] overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];

    [SerializeField] private Health health;
    [SerializeField] private MatchParticipant participant;
    [SerializeField] private float bushCheckRadius = 0.25f;
    [SerializeField] private float revealDistance = 2.5f;
    [SerializeField] private float revealSeconds = 1f;
    [Tooltip("Bushes within this distance fade while the local player hides in one")]
    [SerializeField] private float fadeRadius = 2.5f;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private readonly HashSet<Bush> fadedBushes = new HashSet<Bush>();
    private readonly HashSet<Bush> nearbyBushes = new HashSet<Bush>();
    private readonly List<Bush> bushesToRestore = new List<Bush>();

    private float revealedUntil;
    private bool isShown = true;

    public bool IsHiddenInBush { get; private set; }

    void OnEnable()
    {
        byHealth[health] = this;
        health.Damaged += HandleDamaged;
        PlayerWeapon.AnyShotFired += HandleShotFired;
    }

    void OnDisable()
    {
        byHealth.Remove(health);
        health.Damaged -= HandleDamaged;
        PlayerWeapon.AnyShotFired -= HandleShotFired;
        SetFadedBushes(false);
        SetShown(true);
    }

    void Update()
    {
        IsHiddenInBush = health.IsAlive && IsInBush();

        PlayerController local = PlayerController.Local;
        bool isLocal = local != null && local.Health == health;

        if (isLocal)
        {
            UpdateBushFade();
        }

        bool shouldShow = local == null || isLocal || IsVisibleTo(local.transform.position, local.Participant.TeamId);

        // Re-applied every frame while hidden: weapon models, the health bar and effects can switch on at any time
        if (!shouldShow || !isShown)
        {
            SetShown(shouldShow);
        }
    }

    // Used by bots on the server: true when the viewer cannot see this character
    public static bool IsHiddenFrom(Health target, Vector3 viewerPosition, int viewerTeam)
    {
        return byHealth.TryGetValue(target, out PlayerBushVisibility visibility) && !visibility.IsVisibleTo(viewerPosition, viewerTeam);
    }

    private bool IsVisibleTo(Vector3 viewerPosition, int viewerTeam)
    {
        if (!IsHiddenInBush || Time.time < revealedUntil) return true;

        if (viewerTeam >= 0 && viewerTeam == participant.TeamId) return true;

        return (viewerPosition - transform.position).sqrMagnitude <= revealDistance * revealDistance;
    }

    private bool IsInBush()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * FEET_HEIGHT, bushCheckRadius, overlapBuffer, Physics.AllLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            if (overlapBuffer[i].TryGetComponent(out Bush _)) return true;
        }

        return false;
    }

    private void UpdateBushFade()
    {
        nearbyBushes.Clear();

        if (IsHiddenInBush)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, fadeRadius, overlapBuffer, Physics.AllLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (overlapBuffer[i].TryGetComponent(out Bush bush))
                {
                    nearbyBushes.Add(bush);
                }
            }
        }

        bushesToRestore.Clear();

        foreach (Bush bush in fadedBushes)
        {
            if (!nearbyBushes.Contains(bush))
            {
                bushesToRestore.Add(bush);
            }
        }

        foreach (Bush bush in bushesToRestore)
        {
            fadedBushes.Remove(bush);

            if (bush != null)
            {
                bush.SetFaded(false);
            }
        }

        foreach (Bush bush in nearbyBushes)
        {
            if (fadedBushes.Add(bush))
            {
                bush.SetFaded(true);
            }
        }
    }

    private void SetFadedBushes(bool isFaded)
    {
        foreach (Bush bush in fadedBushes)
        {
            if (bush != null)
            {
                bush.SetFaded(isFaded);
            }
        }

        fadedBushes.Clear();
    }

    // forceRenderingOff is used so other scripts that enable or disable renderers are not overridden
    private void SetShown(bool shown)
    {
        isShown = shown;
        GetComponentsInChildren(true, renderers);

        foreach (Renderer target in renderers)
        {
            target.forceRenderingOff = !shown;
        }

        if (shown)
        {
            foreach (Canvas canvas in hiddenCanvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            hiddenCanvases.Clear();

            return;
        }

        GetComponentsInChildren(true, canvases);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.enabled)
            {
                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }
    }

    private void HandleDamaged(int amount)
    {
        revealedUntil = Time.time + revealSeconds;
    }

    private void HandleShotFired(Vector3 muzzle, Vector3 direction, WeaponData weapon)
    {
        if ((muzzle - transform.position).sqrMagnitude <= SHOT_OWNER_RADIUS * SHOT_OWNER_RADIUS)
        {
            revealedUntil = Time.time + revealSeconds;
        }
    }
}
