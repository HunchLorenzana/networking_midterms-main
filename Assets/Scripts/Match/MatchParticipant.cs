using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class MatchParticipant : NetworkBehaviour
{
    private static readonly List<MatchParticipant> allParticipants = new List<MatchParticipant>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static IReadOnlyList<MatchParticipant> All => allParticipants;

    [SerializeField] private bool isBotPrefab;
    [SerializeField] private Health health;
    [SerializeField] private Material ringMaterial;
    [SerializeField] private PlayerLoadout loadout;
    [SerializeField] private CharacterCatalog characterCatalog;

    private readonly NetworkVariable<int> characterId = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString32Bytes> displayName = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString64Bytes> botLabel = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> slot = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> teamId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isBot = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> kills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> deaths = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> assists = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Renderer teamRing;
    private MaterialPropertyBlock ringBlock;
    private ITeleportable teleportable;
    private string cachedName = "";
    private string cachedBotLabel = "";

    public Health Health => health;
    public string DisplayName => cachedName;
    public int CharacterId => characterId.Value;
    public int CharacterCount => characterCatalog.Count;
    public int Slot => slot.Value;
    public int TeamId => teamId.Value;
    public bool IsReady => isReady.Value;
    public bool IsBot => isBot.Value;
    public bool IsHostPlayer => !isBot.Value && OwnerClientId == NetworkManager.ServerClientId;
    public bool IsJoiner => !isBot.Value && OwnerClientId != NetworkManager.ServerClientId;
    public int Kills => kills.Value;
    public int Deaths => deaths.Value;
    public int Assists => assists.Value;
    public Color DisplayColor => teamId.Value >= 0 ? MatchManager.GetTeamColor(teamId.Value) : MatchManager.GetFreeForAllColor(slot.Value - 1);

    public static MatchParticipant FindByHealth(Health target)
    {
        foreach (MatchParticipant participant in allParticipants)
        {
            if (participant.health == target) return participant;
        }

        return null;
    }

    public static MatchParticipant FindByObjectId(ulong objectId)
    {
        foreach (MatchParticipant participant in allParticipants)
        {
            if (participant.NetworkObjectId == objectId) return participant;
        }

        return null;
    }

    public static MatchParticipant FindBySlot(int slotNumber)
    {
        foreach (MatchParticipant participant in allParticipants)
        {
            if (participant.slot.Value == slotNumber) return participant;
        }

        return null;
    }

    void Awake()
    {
        teleportable = GetComponent<ITeleportable>();
        ringBlock = new MaterialPropertyBlock();
        teamRing = CreateTeamRing();
    }

    public override void OnNetworkSpawn()
    {
        allParticipants.Add(this);
        cachedName = displayName.Value.ToString();
        cachedBotLabel = botLabel.Value.ToString();
        displayName.OnValueChanged += HandleNameChanged;
        botLabel.OnValueChanged += HandleBotLabelChanged;
        slot.OnValueChanged += HandleColorSourceChanged;
        teamId.OnValueChanged += HandleColorSourceChanged;
        ApplyRingColor();

        if (!IsServer) return;

        health.Died += HandleDied;
        isBot.Value = isBotPrefab;
        slot.Value = FindFreeSlot();
        displayName.Value = isBotPrefab ? "Bot" : PlayerNames.Get(OwnerClientId);
        MatchManager.Instance.HandleParticipantJoined(this);
    }

    public override void OnNetworkDespawn()
    {
        allParticipants.Remove(this);
        displayName.OnValueChanged -= HandleNameChanged;
        botLabel.OnValueChanged -= HandleBotLabelChanged;
        slot.OnValueChanged -= HandleColorSourceChanged;
        teamId.OnValueChanged -= HandleColorSourceChanged;

        if (!IsServer) return;

        health.Died -= HandleDied;
        MatchManager.Instance.HandleParticipantLeft();
    }

    public void RequestRename(string newName)
    {
        if (!IsOwner) return;

        RenameRpc(newName);
    }

    public void RequestToggleReady()
    {
        if (!IsOwner) return;

        ToggleReadyRpc();
    }

    public void RequestSwitchTeam()
    {
        if (!IsOwner) return;

        SwitchTeamRpc();
    }

    public void RequestCharacter(int id)
    {
        if (!IsOwner) return;

        SetCharacterRpc(id);
    }

    public void ServerSetCharacter(int id)
    {
        characterId.Value = id;
    }

    public void ServerSetTeam(int newTeamId)
    {
        teamId.Value = newTeamId;
        health.TeamId = newTeamId;
    }

    public void ServerSetName(string newName)
    {
        displayName.Value = newName;
    }

    public void ServerSetBotLabel(string label)
    {
        botLabel.Value = label;
    }

    public void ServerSetReady(bool value)
    {
        isReady.Value = value;
    }

    public void ServerResetStats()
    {
        kills.Value = 0;
        deaths.Value = 0;
        assists.Value = 0;
    }

    public void ServerAddKill()
    {
        kills.Value++;
    }

    public void ServerAddAssist()
    {
        assists.Value++;
    }

    public void ServerAddDeath()
    {
        deaths.Value++;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        teleportable.Teleport(position, rotation);
    }

    public string GetLoadoutLabel()
    {
        return isBot.Value ? cachedBotLabel : loadout.Label;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RenameRpc(string newName)
    {
        string cleanedName = PlayerNames.Clean(newName);

        if (cleanedName.Length == 0 || MatchManager.Instance.Phase != MatchPhase.Lobby) return;

        PlayerNames.Set(OwnerClientId, cleanedName);
        displayName.Value = cleanedName;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ToggleReadyRpc()
    {
        if (MatchManager.Instance.Phase != MatchPhase.Lobby || !IsJoiner) return;

        isReady.Value = !isReady.Value;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SwitchTeamRpc()
    {
        MatchManager.Instance.ServerSwitchTeam(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetCharacterRpc(int id)
    {
        if (id < 0 || id >= characterCatalog.Count || MatchManager.Instance.Phase != MatchPhase.Lobby) return;

        characterId.Value = id;
    }

    private int FindFreeSlot()
    {
        for (int candidate = 1; candidate <= MatchManager.MAX_PLAYERS; candidate++)
        {
            bool isTaken = false;

            foreach (MatchParticipant other in allParticipants)
            {
                if (other != this && other.slot.Value == candidate)
                {
                    isTaken = true;
                }
            }

            if (!isTaken) return candidate;
        }

        return MatchManager.MAX_PLAYERS;
    }

    private void HandleNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        cachedName = newValue.ToString();
    }

    private Renderer CreateTeamRing()
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        Destroy(ring.GetComponent<Collider>());
        ring.name = "TeamRing";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);

        Renderer ringRenderer = ring.GetComponent<Renderer>();

        ringRenderer.sharedMaterial = ringMaterial;

        return ringRenderer;
    }

    private void ApplyRingColor()
    {
        teamRing.GetPropertyBlock(ringBlock);
        ringBlock.SetColor(BaseColorId, slot.Value <= 0 ? Color.white : DisplayColor);
        teamRing.SetPropertyBlock(ringBlock);
    }

    private void HandleColorSourceChanged(int previousValue, int newValue)
    {
        ApplyRingColor();
    }

    private void HandleBotLabelChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        cachedBotLabel = newValue.ToString();
    }

    private void HandleDied(Health attacker)
    {
        MatchParticipant killer = attacker == null ? null : FindByHealth(attacker);

        MatchManager.Instance.ReportDeath(this, killer);
    }
}
