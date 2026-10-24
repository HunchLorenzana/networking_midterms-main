using System;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class NetworkMenu : MonoBehaviour
{
    private const int MAX_CLIENTS = MatchManager.MAX_PLAYERS - 1;
    private const string RELAY_CONNECTION_TYPE = "wss";
    private const string LOCAL_ADDRESS = "127.0.0.1";
    private const ushort LOCAL_PORT = 7777;
    private const string NAME_PREFS_KEY = "PlayerName";

    [SerializeField] private UnityTransport transport;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button soloButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string regionOverride = "asia-southeast1";

    private bool isConnecting;
    private bool isLeaving;

    void Start()
    {
        hostButton.onClick.AddListener(StartRelayHost);
        joinButton.onClick.AddListener(StartRelayClient);
        soloButton.onClick.AddListener(StartSolo);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        soloButton.gameObject.SetActive(Application.isEditor);
        nameInput.characterLimit = PlayerNames.MAX_LENGTH;
        nameInput.text = GetSavedName();
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        statusText.text = "Ready";
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void ReturnToMainMenu()
    {
        isLeaving = true;
        MenuLaunch.ReturnToMainMenu();
    }

    // The profile holds the name. Older installs kept it in PlayerPrefs
    private static string GetSavedName()
    {
        string profileName = ProfileService.Current.PlayerName;

        if (!string.IsNullOrEmpty(profileName)) return profileName;

        return PlayerPrefs.GetString(NAME_PREFS_KEY, "Player" + UnityEngine.Random.Range(1, 100));
    }

    private void StartSolo()
    {
        if (isConnecting || !TryPrepareName()) return;

        isLeaving = false;
        transport.UseWebSockets = false;
        transport.SetConnectionData(LOCAL_ADDRESS, LOCAL_PORT);
        BeginHosting("", "");
    }

    private async void StartRelayHost()
    {
        if (isConnecting || !TryPrepareName()) return;

        isConnecting = true;
        isLeaving = false;
        SetButtons(false);

        try
        {
            statusText.text = "Signing in...";
            await SignInAsync();

            statusText.text = "Creating Relay allocation...";
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CLIENTS, string.IsNullOrEmpty(regionOverride) ? null : regionOverride);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            ConfigureRelayTransport(allocation.ToRelayServerData(RELAY_CONNECTION_TYPE));
            BeginHosting(joinCode, allocation.Region);
        }
        catch (Exception exception)
        {
            statusText.text = "Failed to host";
            Debug.LogException(exception);
        }
        finally
        {
            isConnecting = false;
            SetButtons(true);
        }
    }

    private async void StartRelayClient()
    {
        if (isConnecting || !TryPrepareName()) return;

        string joinCode = joinCodeInput.text.Trim().ToUpperInvariant();

        if (joinCode.Length == 0)
        {
            statusText.text = "Enter a join code";
            return;
        }

        isConnecting = true;
        isLeaving = false;
        SetButtons(false);

        try
        {
            statusText.text = "Signing in...";
            await SignInAsync();

            statusText.text = "Joining room...";
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            ConfigureRelayTransport(allocation.ToRelayServerData(RELAY_CONNECTION_TYPE));

            statusText.text = NetworkManager.Singleton.StartClient() ? "Connecting..." : "Failed to join";
        }
        catch (Exception exception)
        {
            statusText.text = "Invalid or expired join code";
            Debug.LogException(exception);
        }
        finally
        {
            isConnecting = false;
            SetButtons(true);
        }
    }

    private void BeginHosting(string joinCode, string region)
    {
        MatchManager.SetPendingJoinInfo(joinCode, region);
        NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
        statusText.text = NetworkManager.Singleton.StartHost() ? "Hosting" : "Failed to host";
    }

    private bool TryPrepareName()
    {
        string playerName = PlayerNames.Clean(nameInput.text);

        if (playerName.Length == 0)
        {
            statusText.text = "Enter a name first";
            return false;
        }

        ProfileService.SetPlayerName(playerName);
        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName);

        return true;
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string playerName = PlayerNames.Clean(Encoding.UTF8.GetString(request.Payload));
        MatchManager match = MatchManager.Instance;
        bool isLobby = match.Phase == MatchPhase.Lobby;
        bool hasRoom = NetworkManager.Singleton.ConnectedClientsIds.Count < match.MaxPlayers;
        bool hasName = playerName.Length > 0;
        bool isApproved = isLobby && hasRoom && hasName;

        response.Approved = isApproved;
        response.CreatePlayerObject = isApproved;
        response.Reason = !hasName ? "Name required" : (!isLobby ? "Match in progress" : (hasRoom ? "" : "Room is full"));

        if (isApproved)
        {
            PlayerNames.Set(request.ClientNetworkId, playerName);
        }
    }

    private async Task SignInAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void ConfigureRelayTransport(Unity.Networking.Transport.Relay.RelayServerData relayServerData)
    {
        transport.UseWebSockets = true;
        transport.SetRelayServerData(relayServerData);
        transport.UseWebSockets = true;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (isLeaving || NetworkManager.Singleton.IsServer) return;

        string reason = NetworkManager.Singleton.DisconnectReason;
        bool isTransportMessage = string.IsNullOrEmpty(reason) || reason.StartsWith("[");

        statusText.text = isTransportMessage ? "Host left the room" : reason;
    }

    private void SetButtons(bool isInteractable)
    {
        hostButton.interactable = isInteractable;
        joinButton.interactable = isInteractable;
        soloButton.interactable = isInteractable;
        mainMenuButton.interactable = isInteractable;
    }
}
