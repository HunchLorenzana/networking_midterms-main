using UnityEngine;

public class TrainLights : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer _lightRenderer;
    [SerializeField] private Material _lightMaterial;

    [Header("Colors")]
    [SerializeField] private Color _greenColor = Color.green;
    [SerializeField] private Color _yellowColor = Color.yellow;
    [SerializeField] private Color _redColor = Color.red;

    [Header("Emission")]
    [SerializeField] private float _emissionIntensity = 2f;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int EmissionColorID =
        Shader.PropertyToID("_EmissionColor");

    private Material _instanceMaterial;

    private void Awake()
    {
        if (_lightRenderer == null || _lightMaterial == null)
        {
            Debug.LogWarning("TrainLights: Assign the Renderer and Material.", this);
            return;
        }

        // Create a unique material instance.
        _instanceMaterial = new Material(_lightMaterial);

        // Assign the unique material to the renderer.
        _lightRenderer.material = _instanceMaterial;

        SetGreen();
    }

    public void SetGreen()
    {
        SetLightColor(_greenColor);
    }

    public void SetYellow()
    {
        SetLightColor(_yellowColor);
    }

    public void SetRed()
    {
        SetLightColor(_redColor);
    }

    private void SetLightColor(Color color)
    {
        if (_instanceMaterial == null)
            return;

        if (_instanceMaterial.HasProperty(BaseColorID))
        {
            _instanceMaterial.SetColor(BaseColorID, color);
        }

        if (_instanceMaterial.HasProperty(EmissionColorID))
        {
            Color emission = color * _emissionIntensity;

            _instanceMaterial.SetColor(EmissionColorID, emission);

            _instanceMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void OnDestroy()
    {
        if (_instanceMaterial != null)
        {
            Destroy(_instanceMaterial);
        }
    }
}