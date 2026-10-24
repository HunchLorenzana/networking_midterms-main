using Unity.Netcode;
using UnityEngine;

public class TrainBarricade : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _barricadePivot;

    [Header("Rotation")]
    [SerializeField] private Vector3 _openRotation = new Vector3(0f, 0f, -90f);
    [SerializeField] private Vector3 _closedRotation = Vector3.zero;
    [SerializeField] private float _rotationSpeed = 90f;

    // Server owns the state.
    // Clients receive updates automatically.
    private NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Quaternion _targetRotation;

    private void Awake()
    {
        if (_barricadePivot == null)
            _barricadePivot = transform;

        _targetRotation = Quaternion.Euler(_openRotation);
    }

    public override void OnNetworkSpawn()
    {
        // Listen for state changes on server and clients.
        _isOpen.OnValueChanged += OnOpenStateChanged;

        // Apply the current state, including for late-joining clients.
        UpdateTargetRotation(_isOpen.Value);
    }

    public override void OnNetworkDespawn()
    {
        _isOpen.OnValueChanged -= OnOpenStateChanged;
    }

    private void Update()
    {
        if (_barricadePivot == null)
            return;

        _barricadePivot.localRotation = Quaternion.RotateTowards(
            _barricadePivot.localRotation,
            _targetRotation,
            _rotationSpeed * Time.deltaTime
        );
    }

    public void SetOpen(bool open)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only the server can change the barricade state.", this);
            return;
        }

        _isOpen.Value = open;
    }

    private void OnOpenStateChanged(bool previousValue, bool newValue)
    {
        UpdateTargetRotation(newValue);
    }

    private void UpdateTargetRotation(bool open)
    {
        _targetRotation = Quaternion.Euler(
            open ? _openRotation : _closedRotation
        );
    }
}