using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the Match scene: hosts locally, loads the chosen map scene on top, spawns the player there,
// then either starts a match with bots right away or stays in practice
public class MatchDirector : MonoBehaviour
{
    private const string LOCAL_ADDRESS = "127.0.0.1";
    private const ushort LOCAL_PORT = 7777;
    private const string FALLBACK_NAME = "Player";
    // CharacterMenu applies the saved character one frame after spawning, and it only works before the match starts
    private const int FRAMES_BEFORE_START = 3;

    [Tooltip("Used when the scene is played directly in the editor")]
    [SerializeField] private bool defaultIsPractice;
    [SerializeField] private GameMode defaultMode = GameMode.FreeForAll;
    [SerializeField] private UnityTransport transport;
    [SerializeField] private MapCatalog mapCatalog;
    [SerializeField] private MapLoadingScreen loadingScreen;
    [Tooltip("The loading screen stays up at least this long so the map name and preview can be seen")]
    [SerializeField] private float minimumLoadingSeconds = 1.5f;

    private GameMode mode;
    private MapDefinition map;
    private float loadingStartTime;

    public bool IsPractice { get; private set; }

    void Start()
    {
        if (!MenuLaunch.TryTakePendingLaunch(out bool isPractice, out mode))
        {
            isPractice = defaultIsPractice;
            mode = defaultMode;
        }

        IsPractice = isPractice;
        map = IsPractice ? mapCatalog.PracticeMap : mapCatalog.PickRandom(mode);

        if (map == null)
        {
            Debug.LogError("[MatchDirector] No map in rotation supports " + mode + ". Check the MapCatalog.");
            MenuLaunch.ReturnToMainMenu();

            return;
        }

        loadingScreen.Show(map);
        loadingStartTime = Time.realtimeSinceStartup;
        StartHost();
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleMapLoaded;
        NetworkManager.Singleton.SceneManager.LoadScene(map.SceneName, LoadSceneMode.Additive);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleMapLoaded;
    }

    private void StartHost()
    {
        string playerName = PlayerNames.Clean(ProfileService.Current.PlayerName);

        if (playerName.Length == 0)
        {
            playerName = FALLBACK_NAME;
        }

        NetworkManager network = NetworkManager.Singleton;

        transport.UseWebSockets = false;
        transport.SetConnectionData(LOCAL_ADDRESS, LOCAL_PORT);
        network.NetworkConfig.ConnectionApproval = true;
        network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName);
        network.ConnectionApprovalCallback = ApproveConnection;
        network.StartHost();
    }

    private void HandleMapLoaded(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != map.SceneName) return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleMapLoaded;

        // The map's lighting and skybox apply, and runtime objects are created in the map scene
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        StartCoroutine(SpawnPlayersWhenMapReady());
    }

    // Players are spawned only now, so they land on the map's spawn points instead of before the map exists
    private IEnumerator SpawnPlayersWhenMapReady()
    {
        MatchManager match = MatchManager.Instance;
        NetworkManager network = NetworkManager.Singleton;

        // The loading screen is up until the local player exists, so holding the spawn keeps it visible
        while (!match.IsServerReady || !match.HasMap || Time.realtimeSinceStartup - loadingStartTime < minimumLoadingSeconds)
        {
            yield return null;
        }

        foreach (ulong clientId in network.ConnectedClientsIds)
        {
            Instantiate(network.NetworkConfig.PlayerPrefab).GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }

        if (IsPractice) yield break;

        while (PlayerController.Local == null)
        {
            yield return null;
        }

        for (int i = 0; i < FRAMES_BEFORE_START; i++)
        {
            yield return null;
        }

        match.SetMode(mode);
        match.SetBotSkillTrophies(ProfileService.Current.Trophies);
        match.SetFillWithBots(true);
        match.SetMaxPlayers(MatchManager.MAX_PLAYERS);
        match.StartMatch();
    }

    private static void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string playerName = PlayerNames.Clean(Encoding.UTF8.GetString(request.Payload));

        response.Approved = true;
        response.CreatePlayerObject = false;
        PlayerNames.Set(request.ClientNetworkId, playerName.Length > 0 ? playerName : FALLBACK_NAME);
    }
}
