using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class MapMatchHandler : MonoBehaviour
{
    [Header("Player Spawnpoints")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Map Systems")]
    [Tooltip("Optional: maps without a train leave this empty")]
    [SerializeField] private TrainController _trainController;

    private MatchManager _matchManager;
    private Coroutine _registrationCoroutine;

    private void Start()
    {
        _registrationCoroutine = StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        while (MatchManager.Instance == null)
            yield return null;

        _matchManager = MatchManager.Instance;

        while (!_matchManager.IsServerReady)
            yield return null;

        _matchManager.RegisterMapMatchHandler(this);

        _registrationCoroutine = null;
    }

    public Transform GetSpawnPoint(int index)
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogWarning("[MapMatchHandler] No spawnpoints assigned.", this);
            return null;
        }

        return _spawnPoints[index % _spawnPoints.Length];
    }

    public int SpawnPointCount => _spawnPoints?.Length ?? 0;
    public Transform[] SpawnPoints => _spawnPoints;

    // Maps are laid out with their two sides along Z: the default view looks toward +Z, so the far side is the half
    // of the spawn points with the larger Z
    public bool IsOnFarSide(Vector3 position)
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            return false;

        float middleZ = 0f;

        foreach (Transform spawn in _spawnPoints)
            middleZ += spawn.position.z;

        return position.z > middleZ / _spawnPoints.Length;
    }

    public void OnMatchStarted()
    {
        if (_trainController == null)
            return;

        _trainController.StartTrainSequence();
    }

    public void OnMatchEnded()
    {
        if (_trainController != null)
            _trainController.StopTrainSequence();
    }

    private void OnDestroy()
    {
        if (_registrationCoroutine != null)
            StopCoroutine(_registrationCoroutine);

        if (_matchManager != null)
            _matchManager.UnregisterMapMatchHandler(this);
    }
}