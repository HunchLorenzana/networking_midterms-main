using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public enum TrainDirection
{
    StartToEnd,
    EndToStart
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class Train : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private TrainDirection _direction = TrainDirection.StartToEnd;
    [SerializeField] private float _speed = 15f;

    [Header("Independent Rotation")]
    [SerializeField] private Vector3 _trainRotation = Vector3.zero;

    [Header("Cartoon Movement")]
    [SerializeField] private float _acceleration = 8f;
    [SerializeField] private float _deceleration = 8f;
    [SerializeField] private float _overshootDistance = 1f;
    [SerializeField] private float _overshootDuration = 0.2f;
    [SerializeField] private float _settleDuration = 0.3f;

    [Header("Train Carriages")]
    [SerializeField] private Transform[] _carriages;

    [Header("Carriage Bounce")]
    [SerializeField] private float _bounceHeight = 0.08f;
    [SerializeField] private float _bounceSpeed = 8f;
    [SerializeField] private float _rotationAmount = 1.5f;
    [SerializeField] private float _carriageDelay = 0.15f;

    private Vector3[] _carriageOriginalPositions;
    private Quaternion[] _carriageOriginalRotations;

    private float _movementTime;
    private bool _carriageAnimationActive;

    public TrainDirection Direction => _direction;
    public bool IsMoving { get; private set; }

    private void Awake()
    {
        CacheCarriages();
    }

    private void CacheCarriages()
    {
        if (_carriages == null || _carriages.Length == 0)
        {
            // Automatically find all child objects.
            int childCount = transform.childCount;

            _carriages = new Transform[childCount];

            for (int i = 0; i < childCount; i++)
            {
                _carriages[i] = transform.GetChild(i);
            }
        }

        _carriageOriginalPositions = new Vector3[_carriages.Length];
        _carriageOriginalRotations = new Quaternion[_carriages.Length];

        for (int i = 0; i < _carriages.Length; i++)
        {
            if (_carriages[i] == null)
                continue;

            _carriageOriginalPositions[i] = _carriages[i].localPosition;
            _carriageOriginalRotations[i] = _carriages[i].localRotation;
        }
    }

    public IEnumerator Travel(Transform startPoint, Transform endPoint)
    {
        if (!IsServer)
            yield break;

        if (startPoint == null || endPoint == null)
            yield break;

        Transform from = _direction == TrainDirection.StartToEnd
            ? startPoint
            : endPoint;

        Transform to = _direction == TrainDirection.StartToEnd
            ? endPoint
            : startPoint;

        Vector3 startPosition = from.position;
        Vector3 endPosition = to.position;

        Vector3 travelDirection = (endPosition - startPosition).normalized;

        transform.position = startPosition;
        transform.rotation = Quaternion.Euler(_trainRotation);

        _movementTime = 0f;

        IsMoving = true;
        _carriageAnimationActive = true;

        float distance = Vector3.Distance(startPosition, endPosition);

        float travelDuration = distance / Mathf.Max(_speed, 0.01f);

        // Main movement.
        float elapsed = 0f;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / travelDuration);

            // Smooth acceleration and deceleration.
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

            transform.position = Vector3.Lerp(
                startPosition,
                endPosition,
                easedTime
            );

            _movementTime += Time.deltaTime;

            yield return null;
        }

        transform.position = endPosition;

        // Cartoony overshoot.
        Vector3 overshootPosition =
            endPosition + travelDirection * _overshootDistance;

        elapsed = 0f;

        Vector3 overshootStart = transform.position;

        while (elapsed < _overshootDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / _overshootDuration);

            // Ease out.
            float easedTime = 1f - Mathf.Pow(1f - t, 3f);

            transform.position = Vector3.Lerp(
                overshootStart,
                overshootPosition,
                easedTime
            );

            _movementTime += Time.deltaTime;

            yield return null;
        }

        transform.position = overshootPosition;

        // Bounce back to the endpoint.
        elapsed = 0f;

        Vector3 settleStart = transform.position;

        while (elapsed < _settleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / _settleDuration);

            // Smooth settling.
            float easedTime = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                settleStart,
                endPosition,
                easedTime
            );

            _movementTime += Time.deltaTime;

            yield return null;
        }

        transform.position = endPosition;

        IsMoving = false;
        _carriageAnimationActive = false;

        // Reset all carriages.
        ResetCarriages();
    }

    private void LateUpdate()
    {
        if (!_carriageAnimationActive || !IsMoving)
            return;

        AnimateCarriages();
    }

    private void AnimateCarriages()
    {
        for (int i = 0; i < _carriages.Length; i++)
        {
            Transform carriage = _carriages[i];

            if (carriage == null)
                continue;

            // Each carriage gets a different animation phase.
            float phase = _movementTime * _bounceSpeed - i * _carriageDelay;

            // Vertical bouncing.
            float bounce = Mathf.Sin(phase) * _bounceHeight;

            Vector3 originalPosition = _carriageOriginalPositions[i];

            carriage.localPosition = originalPosition + Vector3.up * bounce;

            // Slight cartoony rocking.
            float rotation = Mathf.Sin(phase) * _rotationAmount;

            Quaternion originalRotation = _carriageOriginalRotations[i];

            carriage.localRotation = originalRotation *
                Quaternion.Euler(rotation, 0f, 0f);
        }
    }

    private void ResetCarriages()
    {
        for (int i = 0; i < _carriages.Length; i++)
        {
            if (_carriages[i] == null)
                continue;

            _carriages[i].localPosition = _carriageOriginalPositions[i];
            _carriages[i].localRotation = _carriageOriginalRotations[i];
        }
    }

    public override void OnNetworkDespawn()
    {
        IsMoving = false;
        _carriageAnimationActive = false;

        ResetCarriages();
    }
}