using UnityEngine;

public class LocalPresetApplier : MonoBehaviour
{
    [SerializeField] private LoadoutCatalog catalog;

    private MatchPhase lastPhase;
    private bool hasSent;
    private LoadoutIds lastSent;

    void OnEnable()
    {
        PlayerController.LocalSpawned += HandleLocalSpawned;
        PresetService.Changed += SendSelectedPreset;
    }

    void OnDisable()
    {
        PlayerController.LocalSpawned -= HandleLocalSpawned;
        PresetService.Changed -= SendSelectedPreset;
    }

    void Start()
    {
        lastPhase = MatchManager.Instance.Phase;
        PresetOwnership.Sanitize(catalog);
    }

    // Back in the lobby the server applies changes right away, so resend in case one was still waiting for a respawn
    void Update()
    {
        MatchPhase phase = MatchManager.Instance.Phase;

        if (phase == lastPhase) return;

        lastPhase = phase;

        if (phase == MatchPhase.Lobby)
        {
            hasSent = false;
            SendSelectedPreset();
        }
    }

    private static LoadoutIds ToLoadoutIds(LoadoutPreset preset)
    {
        return new LoadoutIds(
            preset.GetItemId(LoadoutPreset.PRIMARY_SLOT),
            preset.GetItemId(LoadoutPreset.SECONDARY_SLOT),
            preset.GetItemId(LoadoutPreset.FIRST_ABILITY_SLOT),
            preset.GetItemId(LoadoutPreset.SECOND_ABILITY_SLOT),
            preset.GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT),
            preset.GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT + 1),
            preset.GetItemId(LoadoutPreset.FIRST_PASSIVE_SLOT + 2));
    }

    private void HandleLocalSpawned(PlayerController player)
    {
        hasSent = false;
        SendSelectedPreset();
    }

    private void SendSelectedPreset()
    {
        if (PlayerController.Local == null) return;

        LoadoutIds ids = ToLoadoutIds(PresetService.Selected);

        if (hasSent && ids.Equals(lastSent)) return;

        hasSent = true;
        lastSent = ids;
        PlayerController.Local.Loadout.RequestLoadout(ids);
    }
}
