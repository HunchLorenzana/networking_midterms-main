using Unity.Collections;
using Unity.Netcode;

public class MatchState : NetworkBehaviour
{
    private readonly NetworkVariable<MatchPhase> phase = new NetworkVariable<MatchPhase>(
        MatchPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<GameMode> mode = new NetworkVariable<GameMode>(
        GameMode.FreeForAll, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> matchStartTime = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> matchEndTime = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isDraw = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> winnerTeam = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> winnerObjectId = new NetworkVariable<ulong>(
        0UL, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> blueKills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> redKills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> maxPlayers = new NetworkVariable<int>(
        6, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> fillWithBots = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString32Bytes> joinCode = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString32Bytes> region = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public MatchPhase Phase { get => phase.Value; set => phase.Value = value; }
    public GameMode Mode { get => mode.Value; set => mode.Value = value; }
    public double MatchStartTime { get => matchStartTime.Value; set => matchStartTime.Value = value; }
    public double MatchEndTime { get => matchEndTime.Value; set => matchEndTime.Value = value; }
    public bool IsDraw { get => isDraw.Value; set => isDraw.Value = value; }
    public int WinnerTeam { get => winnerTeam.Value; set => winnerTeam.Value = value; }
    public ulong WinnerObjectId { get => winnerObjectId.Value; set => winnerObjectId.Value = value; }
    public int MaxPlayers { get => maxPlayers.Value; set => maxPlayers.Value = value; }
    public bool FillWithBots { get => fillWithBots.Value; set => fillWithBots.Value = value; }

    public string JoinCode => joinCode.Value.ToString();
    public string Region => region.Value.ToString();

    public override void OnNetworkSpawn()
    {
        MatchManager.Instance.BindState(this);
    }

    public override void OnNetworkDespawn()
    {
        if (MatchManager.Instance == null) return;

        MatchManager.Instance.UnbindState(this);
    }

    public void SetJoinInfo(string code, string regionName)
    {
        joinCode.Value = code;
        region.Value = regionName;
    }

    public int GetTeamKills(int teamId)
    {
        return teamId == 0 ? blueKills.Value : redKills.Value;
    }

    public void AddTeamKill(int teamId)
    {
        if (teamId == 0)
        {
            blueKills.Value++;
        }
        else
        {
            redKills.Value++;
        }
    }

    public void ResetTeamKills()
    {
        blueKills.Value = 0;
        redKills.Value = 0;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void KillFeedRpc(bool hasKiller, ulong killerObjectId, ulong victimObjectId)
    {
        MatchManager.Instance.RaiseKillRegistered(
            hasKiller ? MatchParticipant.FindByObjectId(killerObjectId) : null,
            MatchParticipant.FindByObjectId(victimObjectId));
    }

    [ClientRpc]
    public void CustomKillFeedClientRpc(string message)
    {
        MatchManager.Instance?.RaiseCustomKillFeed(message);
    }
}
