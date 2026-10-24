using UnityEngine;

public class Bush : MonoBehaviour
{
    [Header("Sway Settings")]
    [SerializeField] private float _swayAngle = 10f;
    [SerializeField] private float _swaySpeed = 1f;

    [Header("Axis")]
    [SerializeField] private Vector3 _swayAxis = Vector3.forward;

    [Header("Transparency")]
    [SerializeField] private Renderer _bushRenderer;
    [SerializeField] private Material _bushMaterial;
    [SerializeField, Range(0f, 1f)] private float _transparentAlpha = 0.5f;

    [Header("Optional")]
    [SerializeField] private bool _randomizeStart = true;

    private Quaternion _initialRotation;
    private float _randomOffset;

    private float _originalAlpha;
    private bool _playerInside;

    private static readonly int GeneralAlphaID =
        Shader.PropertyToID("_GeneralAlpha");

    private void Awake()
    {
        if (_bushRenderer == null || _bushMaterial == null)
        {
            Debug.LogWarning("Bush: Assign the Renderer and Material.", this);
            return;
        }

        if (!_bushMaterial.HasProperty(GeneralAlphaID))
        {
            Debug.LogWarning("Bush: Material does not have _GeneralAlpha.", this);
            return;
        }

        Material uniqueMaterial = new Material(_bushMaterial);

        _bushRenderer.material = uniqueMaterial;

        _bushMaterial = uniqueMaterial;

        _originalAlpha = _bushMaterial.GetFloat(GeneralAlphaID);
    }

    private void Start()
    {
        _initialRotation = transform.localRotation;

        _randomOffset = _randomizeStart
            ? Random.Range(0f, Mathf.PI * 2f)
            : 0f;
    }

    private void Update()
    {
        float sway = Mathf.Sin(
            Time.time * _swaySpeed + _randomOffset
        ) * _swayAngle;

        Quaternion swayRotation = Quaternion.AngleAxis(
            sway,
            _swayAxis.normalized
        );

        transform.localRotation = _initialRotation * swayRotation;
    }

    // Called by the local player's PlayerBushVisibility for the bushes around it
    public void SetFaded(bool faded)
    {
        SetTransparency(faded);
    }

    private void SetTransparency(bool transparent)
    {
        if (_playerInside == transparent)
            return;

        _playerInside = transparent;

        if (_bushMaterial == null)
            return;

        float alpha = transparent
            ? _transparentAlpha
            : _originalAlpha;

        _bushMaterial.SetFloat(GeneralAlphaID, alpha);
    }

    private void OnDestroy()
    {
        if (_bushMaterial != null)
        {
            Destroy(_bushMaterial);
        }
    }
}