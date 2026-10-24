using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MatchManager : MonoBehaviour
{
    public const int TEAM_COUNT = 2;
    public const int MAX_PLAYERS = 6;
    public const int MIN_PLAYERS = 2;
    public const int TEAM_SIZE_LIMIT = 3;
    public const float ASSIST_WINDOW_SECONDS = 5f;

    private static readonly string[] TeamNames = { "BLUE", "RED" };
    private static readonly Color[] TeamColors = { new Color(0.25f, 0.55f, 1f), new Color(1f, 0.3f, 0.3f) };
    private static readonly Color[] FreeForAllColors =
    {
        new Color(1f, 0.35f, 0.35f),
        new Color(0.35f, 0.6f, 1f),
        new Color(0.4f, 0.9f, 0.4f),
        new Color(1f, 0.85f, 0.3f),
        new Color(0.75f, 0.45f, 1f),
        new Color(0.3f, 0.9f, 0.9f)
    };

    private static string pendingJoinCode = "";
    private static string pendingRegion = "";

    public static MatchManager Instance { get; private set; }

    public event Action<MatchParticipant, MatchParticipant> KillRegistered;
    public event System.Action<string> CustomKillRegistered;

    [SerializeField] private MatchState statePrefab;
    [SerializeField] private BotBrain botPrefab;
    [SerializeField] private BotRoster botRoster;
    [SerializeField] private DummyEnemy dummyPrefab;
    [SerializeField] private int freeForAllKillLimit = 15;
    [SerializeField] private int teamKillLimit = 20;
    [SerializeField] private float matchSeconds = 300f;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float spawnProtectionSeconds = 2f;

    private readonly List<NetworkObject> spawnedBots = new List<NetworkObject>();
    private readonly List<Health> assistants = new List<Health>();

    private MatchState state;
    private MapMatchHandler _mapMatchHandler;
    private bool hasSpawnedPracticeDummies;
    private int botSkillTrophies;
    private bool needsWinCheck;

    public MatchPhase Phase => state != null ? state.Phase : MatchPhase.Lobby;
    public GameMode Mode => state != null ? state.Mode : GameMode.FreeForAll;
    public double MatchStartTime => state != null ? state.MatchStartTime : 0d;
    public bool IsDraw => state != null && state.IsDraw;
    public int WinnerTeam => state != null ? state.WinnerTeam : -1;
    public int MaxPlayers => state != null ? state.MaxPlayers : MAX_PLAYERS;
    public bool FillWithBots => state != null && state.FillWithBots;
    public string JoinCode => state != null ? state.JoinCode : "";
    public string Region => state != null ? state.Region : "";
    public float SpawnProtectionSeconds => spawnProtectionSeconds;
    public int KillLimit => Mode == GameMode.FreeForAll ? freeForAllKillLimit : teamKillLimit;
    public bool IsServerReady => state != null && state.IsServer;
    public bool HasMap => _mapMatchHandler != null;
    public double Now => NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
    public bool CanPlayersAct => Phase == MatchPhase.Lobby || (Phase == MatchPhase.Playing && Now >= MatchStartTime);
    public float TimeRemaining => state != null ? Mathf.Max(0f, (float)(state.MatchEndTime - Now)) : 0f;

    public MatchParticipant WinnerParticipant => state != null ? MatchParticipant.FindByObjectId(state.WinnerObjectId) : null;

    public bool CanStartMatch
    {
        get
        {
            if (Phase != MatchPhase.Lobby || !AreJoinersReady() || MatchParticipant.All.Count == 0) return false;
            if (FillWithBots) return true;
            if (Mode == GameMode.FreeForAll) return MatchParticipant.All.Count >= MIN_PLAYERS;

            return CountTeam(0) > 0 && CountTeam(1) > 0;
        }
    }

    public static string GetTeamName(int teamId)
    {
        return TeamNames[teamId];
    }

    public static Color GetTeamColor(int teamId)
    {
        return TeamColors[teamId];
    }

    public static Color GetFreeForAllColor(int index)
    {
        return index < 0 ? Color.white : FreeForAllColors[index % FreeForAllColors.Length];
    }

    public static void SetPendingJoinInfo(string code, string region)
    {
        pendingJoinCode = code;
        pendingRegion = region;
    }

    public int GetTeamKills(int teamId)
    {
        return state != null ? state.GetTeamKills(teamId) : 0;
    }

    public MatchParticipant GetLeader()
    {
        MatchParticipant leader = null;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (leader == null || participant.Kills > leader.Kills)
            {
                leader = participant;
            }
        }

        return leader;
    }

    public int CountTeam(int teamId)
    {
        int count = 0;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (participant.TeamId == teamId)
            {
                count++;
            }
        }

        return count;
    }

    public int CountHumans()
    {
        int count = 0;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (!participant.IsBot)
            {
                count++;
            }
        }

        return count;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        if (NetworkManager.Singleton.IsServer)
            HandleServerStarted();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        if (IsServerReady && state.Phase == MatchPhase.Playing && Now >= state.MatchEndTime)
        {
            EvaluateEnd(true);
        }
    }

    void LateUpdate()
    {
        if (!needsWinCheck || !IsServerReady || state.Phase != MatchPhase.Playing) return;

        needsWinCheck = false;
        EvaluateEnd(false);
    }

    public void BindState(MatchState matchState)
    {
        state = matchState;
    }

    public void UnbindState(MatchState matchState)
    {
        if (state == matchState)
        {
            state = null;
        }
    }
    public void RaiseCustomKillFeed(string message)
    {
        CustomKillRegistered?.Invoke(message);
    }
    public void RaiseKillRegistered(MatchParticipant killer, MatchParticipant victim)
    {
        KillRegistered?.Invoke(killer, victim);
    }
    public void RegisterCustomKill(string killerName,string customKillMessage,MatchParticipant victim)
    {
        if (victim == null)
            return;

        string coloredKiller = "<color=#FFFFFF>" + killerName + "</color>";

        string coloredVictim = "<color=#" + ColorUtility.ToHtmlStringRGB(victim.DisplayColor) +">" + victim.DisplayName + "</color>";

        string message = customKillMessage.Replace("{killer}", coloredKiller).Replace("{victim}", coloredVictim);

        RaiseCustomKillFeed(message);
    }

    public void RegisterMapMatchHandler(MapMatchHandler handler)
    {
        if (!IsServerReady || handler == null)
            return;

        _mapMatchHandler = handler;
        SpawnPracticeDummies();
    }

    public void UnregisterMapMatchHandler(MapMatchHandler handler)
    {
        if (_mapMatchHandler == handler)
            _mapMatchHandler = null;
    }

    public void SetMode(GameMode newMode)
    {
        if (!IsServerReady || state.Phase != MatchPhase.Lobby) return;

        state.Mode = newMode;
        AssignTeams();
    }

    public void SetFillWithBots(bool value)
    {
        if (!IsServerReady || state.Phase != MatchPhase.Lobby) return;

        state.FillWithBots = value;
    }

    public void CycleMaxPlayers()
    {
        if (!IsServerReady || state.Phase != MatchPhase.Lobby) return;

        int lowest = Mathf.Max(MIN_PLAYERS, CountHumans());
        int next = state.MaxPlayers + 1;

        state.MaxPlayers = next > MAX_PLAYERS ? lowest : next;
    }

    // Bots play at the BotRoster difficulty for these trophies (the local player's, until matchmaking is online)
    public void SetBotSkillTrophies(int trophies)
    {
        botSkillTrophies = trophies;
    }

    public void SetMaxPlayers(int count)
    {
        if (!IsServerReady || state.Phase != MatchPhase.Lobby) return;

        state.MaxPlayers = Mathf.Clamp(count, Mathf.Max(MIN_PLAYERS, CountHumans()), MAX_PLAYERS);
    }

    public void StartMatch()
    {
        if (!IsServerReady || !CanStartMatch) return;

        SpawnBots();
        state.ResetTeamKills();
        state.IsDraw = false;
        state.WinnerTeam = -1;
        state.WinnerObjectId = 0UL;
        needsWinCheck = false;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            participant.ServerResetStats();
            participant.ServerSetReady(false);
        }

        PlaceAllAtSpawns(false);

        state.MatchStartTime = Now + countdownSeconds;
        state.MatchEndTime = state.MatchStartTime + matchSeconds;
        state.Phase = MatchPhase.Playing;

        StartCoroutine(StartMapSystemsAfterCountdown());
    }

    public void ReturnToLobby()
    {
        if (!IsServerReady || state.Phase != MatchPhase.MatchOver)
            return;

        _mapMatchHandler?.OnMatchEnded();

        RemoveBots();
        state.Phase = MatchPhase.Lobby;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            participant.ServerResetStats();
            participant.ServerSetReady(false);
        }

        PlaceAllAtSpawns(true);
    }

    private IEnumerator StartMapSystemsAfterCountdown()
    {
        while (Now < state.MatchStartTime)
        {
            if (state.Phase != MatchPhase.Playing)
                yield break;

            yield return null;
        }

        while (_mapMatchHandler == null)
        {
            if (state.Phase != MatchPhase.Playing || !IsServerReady)
                yield break;

            yield return null;
        }

        if (state.Phase != MatchPhase.Playing || !IsServerReady)
            yield break;

        Debug.Log("[MatchManager] Countdown finished. Starting map systems.");

        _mapMatchHandler.OnMatchStarted();
    }

    public void HandleParticipantJoined(MatchParticipant participant)
    {
        if (state == null || state.Mode == GameMode.FreeForAll)
        {
            participant.ServerSetTeam(-1);
            return;
        }

        participant.ServerSetTeam(CountTeam(0) <= CountTeam(1) ? 0 : 1);
    }

    public void HandleParticipantLeft()
    {
        if (!IsServerReady || state.Phase != MatchPhase.Playing) return;

        bool hasTooFew = Mode == GameMode.FreeForAll ? MatchParticipant.All.Count < MIN_PLAYERS : CountTeam(0) == 0 || CountTeam(1) == 0;

        if (hasTooFew)
        {
            EvaluateEnd(true);
        }
    }

    public void ServerSwitchTeam(MatchParticipant participant)
    {
        if (!IsServerReady || state.Phase != MatchPhase.Lobby || state.Mode != GameMode.TeamDeathmatch || participant.TeamId < 0) return;

        int otherTeam = 1 - participant.TeamId;

        if (CountTeam(otherTeam) >= TEAM_SIZE_LIMIT) return;

        participant.ServerSetTeam(otherTeam);
    }

    public void ReportDeath(MatchParticipant victim, MatchParticipant killer)
    {
        if (!IsServerReady) return;

        bool isSuicide = killer == null || killer == victim;

        assistants.Clear();
        victim.Health.CollectAssistants(isSuicide ? null : killer.Health, ASSIST_WINDOW_SECONDS, assistants);

        if (state.Phase == MatchPhase.Playing)
        {
            foreach (Health assistant in assistants)
            {
                MatchParticipant assistParticipant = MatchParticipant.FindByHealth(assistant);

                if (assistParticipant != null)
                {
                    assistParticipant.ServerAddAssist();
                }
            }

            victim.ServerAddDeath();

            if (!isSuicide)
            {
                killer.ServerAddKill();

                if (killer.TeamId >= 0)
                {
                    state.AddTeamKill(killer.TeamId);
                }
            }

            needsWinCheck = true;
        }

        state.KillFeedRpc(!isSuicide, isSuicide ? 0UL : killer.NetworkObjectId, victim.NetworkObjectId);
    }

    public Transform GetSafestSpawn(Health forHealth)
    {
        Transform[] spawnPool;

        if (Phase == MatchPhase.Lobby)
        {
            spawnPool = GetLobbySpawnPool();
        }
        else
        {
            if (_mapMatchHandler == null)
            {
                Debug.LogWarning("[MatchManager] MapMatchHandler is not registered.");
                return null;
            }

            spawnPool = new Transform[_mapMatchHandler.SpawnPointCount];

            for (int i = 0; i < spawnPool.Length; i++)
            {
                spawnPool[i] = _mapMatchHandler.GetSpawnPoint(i);
            }
        }

        if (spawnPool == null || spawnPool.Length == 0)
        {
            Debug.LogWarning("[MatchManager] No spawnpoints available.");
            return null;
        }

        Transform best = null;
        float bestDistance = -1f;

        foreach (Transform spawn in spawnPool)
        {
            if (spawn == null)
                continue;

            // Lobby spawns do not need enemy-distance evaluation.
            if (Phase == MatchPhase.Lobby)
                return spawn;

            float nearestEnemy = float.MaxValue;

            foreach (Health other in Health.All)
            {
                if (other == null || other == forHealth || !other.IsAlive)
                    continue;

                bool isTeammate =
                    forHealth != null &&
                    forHealth.TeamId >= 0 &&
                    other.TeamId == forHealth.TeamId;

                if (isTeammate)
                    continue;

                float distance = Vector3.Distance(
                    spawn.position,
                    other.transform.position
                );

                nearestEnemy = Mathf.Min(nearestEnemy, distance);
            }

            if (nearestEnemy <= bestDistance)
                continue;

            bestDistance = nearestEnemy;
            best = spawn;
        }

        return best;
    }

    private void HandleServerStarted()
    {
        MatchState spawned = Instantiate(statePrefab);

        spawned.NetworkObject.Spawn();
        spawned.SetJoinInfo(pendingJoinCode, pendingRegion);
        spawnedBots.Clear();
        SpawnPracticeDummies();
    }

    // A practice area's spawn points when the scene has one (practice map, TestArena), otherwise the map's own
    private Transform[] GetLobbySpawnPool()
    {
        if (PracticeArea.Current != null) return PracticeArea.Current.SpawnPoints;

        if (_mapMatchHandler != null) return _mapMatchHandler.SpawnPoints;

        Debug.LogError("[MatchManager] No PracticeArea and no registered map: nowhere to spawn. Check the map scene.");

        return null;
    }

    // Runs on server start (TestArena) and when a map registers (Match scene). Dummies spawn only once
    private void SpawnPracticeDummies()
    {
        PracticeArea practice = PracticeArea.Current;

        if (hasSpawnedPracticeDummies || practice == null || !IsServerReady) return;

        hasSpawnedPracticeDummies = true;

        foreach (Transform point in practice.TrainingDummyPoints)
        {
            DummyEnemy dummy = Instantiate(dummyPrefab, point.position + Vector3.up * 0.1f, point.rotation);

            dummy.NetworkObject.Spawn();
            dummy.Init(false);
            dummy.SetHome(point.position + Vector3.up * 0.1f, point.rotation);
        }

        Transform[] walkerPoints = practice.WalkingDummyPoints;

        for (int i = 0; i + 1 < walkerPoints.Length; i += 2)
        {
            Vector3 first = walkerPoints[i].position + Vector3.up * 0.1f;
            Vector3 second = walkerPoints[i + 1].position + Vector3.up * 0.1f;
            Quaternion facing = Quaternion.LookRotation(second - first);
            DummyEnemy walker = Instantiate(dummyPrefab, first, facing);

            walker.NetworkObject.Spawn();
            walker.Init(false);
            walker.SetHome(first, facing);
            walker.SetPatrol(first, second);
        }
    }

    private bool AreJoinersReady()
    {
        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (participant.IsJoiner && !participant.IsReady) return false;
        }

        return true;
    }

    private void SpawnBots()
    {
        if (!state.FillWithBots) return;

        HashSet<string> namesInUse = new HashSet<string>();
        BotRoster.BotDifficulty difficulty = botRoster.GetDifficulty(botSkillTrophies);

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            namesInUse.Add(participant.DisplayName);
        }

        while (MatchParticipant.All.Count < state.MaxPlayers)
        {
            BotBrain bot = Instantiate(botPrefab);
            string botName = botRoster.PickName(namesInUse);

            namesInUse.Add(botName);
            bot.NetworkObject.Spawn();
            bot.ServerInit(botName, botRoster.PickPreset(), difficulty);
            spawnedBots.Add(bot.NetworkObject);
        }
    }

    private void RemoveBots()
    {
        foreach (NetworkObject bot in spawnedBots)
        {
            if (bot != null && bot.IsSpawned)
            {
                bot.Despawn();
            }
        }

        spawnedBots.Clear();
    }

    private void AssignTeams()
    {
        for (int i = 0; i < MatchParticipant.All.Count; i++)
        {
            MatchParticipant participant = MatchParticipant.All[i];

            participant.ServerSetTeam(state.Mode == GameMode.TeamDeathmatch ? i % TEAM_COUNT : -1);
        }
    }

    private void PlaceAllAtSpawns(bool toLobby)
    {
        Transform[] spawnPool;

        if (toLobby)
        {
            spawnPool = GetLobbySpawnPool();
        }
        else
        {
            if (_mapMatchHandler == null)
            {
                Debug.LogError("[MatchManager] MapMatchHandler is not registered.");
                return;
            }

            int mapSpawnCount = _mapMatchHandler.SpawnPointCount;

            if (mapSpawnCount <= 0)
            {
                Debug.LogError("[MatchManager] No map spawnpoints available.");
                return;
            }

            spawnPool = new Transform[mapSpawnCount];

            for (int i = 0; i < mapSpawnCount; i++)
            {
                spawnPool[i] = _mapMatchHandler.GetSpawnPoint(i);
            }
        }

        if (spawnPool == null || spawnPool.Length == 0)
        {
            Debug.LogError(toLobby
                ? "[MatchManager] No lobby spawnpoints available."
                : "[MatchManager] No map spawnpoints available.");

            return;
        }

        int spawnCount = spawnPool.Length;
        int[] teamCounters = new int[TEAM_COUNT];

        for (int i = 0; i < MatchParticipant.All.Count; i++)
        {
            MatchParticipant participant = MatchParticipant.All[i];

            int spawnIndex = i % spawnCount;

            // Only apply team-based spawn grouping during an actual match.
            if (!toLobby && participant.TeamId >= 0)
            {
                int teamSpawnOffset = spawnCount / TEAM_COUNT;

                if (teamSpawnOffset <= 0)
                {
                    Debug.LogWarning(
                        "[MatchManager] Not enough map spawnpoints for team separation."
                    );
                }
                else
                {
                    spawnIndex =
                        participant.TeamId * teamSpawnOffset
                        + teamCounters[participant.TeamId]++ % teamSpawnOffset;

                    spawnIndex %= spawnCount;
                }
            }

            Transform spawn = spawnPool[spawnIndex];

            if (spawn == null)
                continue;

            participant.Teleport(
                spawn.position + Vector3.up * 0.05f,
                spawn.rotation
            );

            participant.Health.ResetHealth();
        }
    }

    private void EvaluateEnd(bool isTimeUp)
    {
        if (state.Mode == GameMode.FreeForAll)
        {
            EvaluateFreeForAll(isTimeUp);
        }
        else
        {
            EvaluateTeams(isTimeUp);
        }
    }

    private void EvaluateFreeForAll(bool isTimeUp)
    {
        MatchParticipant leader = GetLeader();
        int topKills = leader == null ? 0 : leader.Kills;

        if (!isTimeUp && topKills < freeForAllKillLimit) return;

        int tiedCount = 0;

        foreach (MatchParticipant participant in MatchParticipant.All)
        {
            if (participant.Kills == topKills)
            {
                tiedCount++;
            }
        }

        bool isTied = leader == null || tiedCount > 1;

        state.IsDraw = isTied;
        state.WinnerObjectId = isTied ? 0UL : leader.NetworkObjectId;

        _mapMatchHandler?.OnMatchEnded();
        state.Phase = MatchPhase.MatchOver;
    }

    private void EvaluateTeams(bool isTimeUp)
    {
        int blue = state.GetTeamKills(0);
        int red = state.GetTeamKills(1);

        if (!isTimeUp && Mathf.Max(blue, red) < teamKillLimit) return;

        state.IsDraw = blue == red;
        state.WinnerTeam = blue == red ? -1 : (blue > red ? 0 : 1);

        _mapMatchHandler?.OnMatchEnded();
        state.Phase = MatchPhase.MatchOver;
    }
}
