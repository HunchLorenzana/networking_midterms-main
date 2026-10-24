using Unity.Cinemachine;
using UnityEngine;

public class LocalCameraBinder : MonoBehaviour
{
    private const float FAR_SIDE_YAW = 180f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera followCamera;
    [Tooltip("Its follow offset is turned with the view, so the camera stays behind the player's side")]
    [SerializeField] private CinemachineFollow cameraFollow;

    private PlayerController _player;
    private Vector3 _baseFollowOffset;
    private Quaternion _baseRotation;
    private float _currentYaw;
    private MapMatchHandler _map;

    private void Awake()
    {
        _baseFollowOffset = cameraFollow.FollowOffset;
        _baseRotation = followCamera.transform.rotation;
    }

    private void OnEnable()
    {
        PlayerController.LocalSpawned += HandleLocalSpawned;
        PlayerController.LocalDespawned += HandleLocalDespawned;
    }

    private void OnDisable()
    {
        PlayerController.LocalSpawned -= HandleLocalSpawned;
        PlayerController.LocalDespawned -= HandleLocalDespawned;
    }

    // Like Brawl Stars and MOBAs, a player who starts on the map's far side sees the map turned around, so their side is
    // always at the bottom. It is chosen during the countdown (players stand on their spawns) and kept for the whole match
    private void Update()
    {
        MatchManager match = MatchManager.Instance;

        if (_player == null || match == null) return;

        if (match.Phase != MatchPhase.Playing)
        {
            _map = null;
            SetYaw(0f);

            return;
        }

        if (match.Now >= match.MatchStartTime) return;

        if (_map == null)
        {
            _map = FindFirstObjectByType<MapMatchHandler>();
        }

        if (_map == null) return;

        SetYaw(_map.IsOnFarSide(_player.transform.position) ? FAR_SIDE_YAW : 0f);
    }

    private void SetYaw(float yaw)
    {
        if (Mathf.Approximately(yaw, _currentYaw)) return;

        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

        _currentYaw = yaw;
        cameraFollow.FollowOffset = turn * _baseFollowOffset;
        followCamera.transform.rotation = turn * _baseRotation;

        // Snaps to the new view instead of swinging around the map with damping
        followCamera.PreviousStateIsValid = false;
    }

    private void HandleLocalSpawned(PlayerController spawned)
    {
        _player = spawned;

        if (followCamera == null)
            return;

        followCamera.Follow = _player.transform;
    }

    private void HandleLocalDespawned()
    {
        _player = null;
        _map = null;
        SetYaw(0f);

        if (followCamera == null)
            return;

        followCamera.Follow = null;
    }
}
