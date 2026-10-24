using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TrainController : NetworkBehaviour
{
    [Header("Train Prefabs")]
    [SerializeField] private NetworkObject[] _trainPrefabs;

    [Header("Train Setup")]
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;

    [Header("Crossing Setup")]
    [SerializeField] private TrainBarricade[] _barricades;
    [SerializeField] private TrainLights[] _lights;

    [Header("Random Train Timing")]
    [SerializeField] private float _minTimeBetweenTrains = 5f;
    [SerializeField] private float _maxTimeBetweenTrains = 15f;

    [Header("Crossing Timing")]
    [SerializeField] private float _yellowDuration = 2f;
    [SerializeField] private float _redDuration = 2f;
    [SerializeField] private float _barricadeOpeningDelay = 1f;

    private NetworkObject _currentTrain;
    private Coroutine _trainCoroutine;

    private bool _isRunning;
    private bool _isCrossing;

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        ResetCrossing();

        Debug.Log(
            "[TrainController] Initialized on server. Crossing reset.",
            this
        );
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        StopTrainSequence();
    }

    #endregion

    #region Train Sequence

    private IEnumerator TrainSequence()
    {
        _isRunning = true;

        Debug.Log("[TrainController] Train sequence started.", this);

        while (_isRunning)
        {
            // Random delay before the next train.
            float waitTime = Random.Range(
                _minTimeBetweenTrains,
                _maxTimeBetweenTrains
            );

            Debug.Log(
                $"[TrainController] Waiting {waitTime:F1} seconds before next train.",
                this
            );

            yield return new WaitForSeconds(waitTime);

            if (!_isRunning)
                break;

            // Safety check: wait until the previous train is completely gone.
            if (_currentTrain != null && _currentTrain.IsSpawned)
            {
                Debug.LogWarning(
                    "[TrainController] Previous train is still active. Waiting.",
                    this
                );

                while (_isRunning &&
                       _currentTrain != null &&
                       _currentTrain.IsSpawned)
                {
                    yield return null;
                }

                if (!_isRunning)
                    break;
            }

            // Spawn a randomly selected train.
            NetworkObject trainObject = SpawnRandomTrain();

            if (trainObject == null)
            {
                Debug.LogWarning(
                    "[TrainController] Failed to spawn train. Sequence stopped.",
                    this
                );

                break;
            }

            Train train = trainObject.GetComponent<Train>();

            if (train == null)
            {
                Debug.LogError(
                    "[TrainController] Spawned train prefab has no Train component.",
                    trainObject
                );

                if (trainObject.IsSpawned)
                    trainObject.Despawn(true);

                _currentTrain = null;
                break;
            }

            // Warning phase.
            Debug.Log("[TrainController] Train spawned. Setting lights to yellow.", this);

            SetAllLightsYellow();

            yield return new WaitForSeconds(_yellowDuration);

            if (!_isRunning)
                break;

            // Crossing phase.
            Debug.Log("[TrainController] Setting lights to red.", this);

            SetAllLightsRed();

            yield return new WaitForSeconds(_redDuration);

            if (!_isRunning)
                break;

            // Open barricades before the train crosses.
            Debug.Log("[TrainController] Opening barricades.", this);

            SetBarricadesOpen(true);

            yield return new WaitForSeconds(_barricadeOpeningDelay);

            if (!_isRunning)
                break;

            Debug.Log("[TrainController] Train crossing started.", this);

            _isCrossing = true;

            // This coroutine must finish before another train can spawn.
            yield return train.Travel(_startPoint, _endPoint);

            _isCrossing = false;

            if (!_isRunning)
                break;

            Debug.Log("[TrainController] Train crossing finished.", this);

            // Despawn the train before the next iteration.
            if (trainObject != null && trainObject.IsSpawned)
                trainObject.Despawn(true);

            _currentTrain = null;

            // Reset crossing.
            SetBarricadesOpen(false);
            SetAllLightsGreen();

            Debug.Log(
                "[TrainController] Crossing reset. Waiting for next train.",
                this
            );
        }

        // Final cleanup.
        CleanupActiveTrain();

        _isRunning = false;
        _isCrossing = false;
        _trainCoroutine = null;

        Debug.Log("[TrainController] Train sequence stopped.", this);
    }

    #endregion

    #region Train Spawning

    private NetworkObject SpawnRandomTrain()
    {
        if (_trainPrefabs == null || _trainPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[TrainController] No train prefabs assigned.",
                this
            );

            return null;
        }

        // Collect valid train prefabs.
        List<NetworkObject> validPrefabs = new List<NetworkObject>();

        foreach (NetworkObject prefab in _trainPrefabs)
        {
            if (prefab == null)
                continue;

            if (prefab.GetComponent<Train>() == null)
            {
                Debug.LogWarning(
                    $"[TrainController] {prefab.name} has no Train component.",
                    prefab
                );

                continue;
            }

            validPrefabs.Add(prefab);
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogError(
                "[TrainController] No valid train prefabs assigned.",
                this
            );

            return null;
        }

        // Random train selection.
        NetworkObject selectedPrefab = validPrefabs[
            Random.Range(0, validPrefabs.Count)
        ];

        Train trainSettings = selectedPrefab.GetComponent<Train>();

        // Direction is determined by the selected prefab.
        Transform spawnPoint =
            trainSettings.Direction == TrainDirection.StartToEnd
                ? _startPoint
                : _endPoint;

        if (spawnPoint == null)
        {
            Debug.LogError(
                "[TrainController] Train spawn point is missing.",
                this
            );

            return null;
        }

        // Spawn on server and synchronize with clients.
        NetworkObject spawnedTrain = Instantiate(
            selectedPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        spawnedTrain.Spawn(true);

        _currentTrain = spawnedTrain;

        Debug.Log(
            $"[TrainController] Spawned train: {selectedPrefab.name}",
            spawnedTrain
        );

        return spawnedTrain;
    }

    #endregion

    #region Barricades

    private void SetBarricadesOpen(bool isOpen)
    {
        if (_barricades == null)
            return;

        foreach (TrainBarricade barricade in _barricades)
        {
            if (barricade != null)
                barricade.SetOpen(isOpen);
        }

        SetBarricadesOpenClientRpc(isOpen);
    }

    [ClientRpc]
    private void SetBarricadesOpenClientRpc(bool isOpen)
    {
        if (IsServer)
            return;

        if (_barricades == null)
            return;

        foreach (TrainBarricade barricade in _barricades)
        {
            if (barricade != null)
                barricade.SetOpen(isOpen);
        }
    }

    #endregion

    #region Lights

    private void SetAllLightsYellow()
    {
        SetAllLightsLocal(1);
        SetLightsClientRpc(1);
    }

    private void SetAllLightsRed()
    {
        SetAllLightsLocal(2);
        SetLightsClientRpc(2);
    }

    private void SetAllLightsGreen()
    {
        SetAllLightsLocal(0);
        SetLightsClientRpc(0);
    }

    private void SetAllLightsLocal(int state)
    {
        if (_lights == null)
            return;

        foreach (TrainLights light in _lights)
        {
            if (light == null)
                continue;

            switch (state)
            {
                case 0:
                    light.SetGreen();
                    break;

                case 1:
                    light.SetYellow();
                    break;

                case 2:
                    light.SetRed();
                    break;
            }
        }
    }

    [ClientRpc]
    private void SetLightsClientRpc(int state)
    {
        if (IsServer)
            return;

        SetAllLightsLocal(state);
    }

    #endregion

    #region Crossing Reset

    private void ResetCrossing()
    {
        SetBarricadesOpen(true);
        SetAllLightsGreen();

        Debug.Log(
            "[TrainController] Crossing reset. Barricades open, lights green.",
            this
        );
    }

    private void CleanupActiveTrain()
    {
        if (_currentTrain != null && _currentTrain.IsSpawned)
        {
            Debug.Log("[TrainController] Despawning active train.", this);

            _currentTrain.Despawn(true);
        }

        _currentTrain = null;

        _isCrossing = false;

        SetBarricadesOpen(false);
        SetAllLightsGreen();
    }

    #endregion

    #region Public Controls

    [ContextMenu("Start Train Sequence")]
    public void StartTrainSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[TrainController] Start the train sequence in Play Mode.",
                this
            );

            return;
        }

        if (!IsServer)
        {
            Debug.LogWarning(
                "[TrainController] Only the server can start the train sequence.",
                this
            );

            return;
        }

        if (_isRunning)
        {
            Debug.LogWarning(
                "[TrainController] Train sequence is already running.",
                this
            );

            return;
        }

        if (_minTimeBetweenTrains < 0f)
            _minTimeBetweenTrains = 0f;

        if (_maxTimeBetweenTrains < _minTimeBetweenTrains)
            _maxTimeBetweenTrains = _minTimeBetweenTrains;

        Debug.Log("[TrainController] StartTrainSequence() called.", this);

        _trainCoroutine = StartCoroutine(TrainSequence());
    }

    [ContextMenu("Stop Train Sequence")]
    public void StopTrainSequence()
    {
        if (!Application.isPlaying)
            return;

        if (!IsServer)
        {
            Debug.LogWarning(
                "[TrainController] Only the server can stop the train sequence.",
                this
            );

            return;
        }

        Debug.Log("[TrainController] StopTrainSequence() called.", this);

        _isRunning = false;

        if (_trainCoroutine != null)
        {
            StopCoroutine(_trainCoroutine);
            _trainCoroutine = null;
        }

        CleanupActiveTrain();

        _isRunning = false;

        Debug.Log(
            "[TrainController] Train sequence stopped and crossing reset.",
            this
        );
    }

    [ContextMenu("Reset Crossing")]
    public void ResetCrossingContextMenu()
    {
        StopTrainSequence();
    }

    #endregion
}