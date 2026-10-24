using DG.Tweening;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Health health;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float duration = 0.15f;

    private Color[] baseColors;

    void Awake()
    {
        CacheBaseColors();
    }

    public void SetRenderers(Renderer[] newRenderers)
    {
        renderers = newRenderers;
        CacheBaseColors();
    }

    private void CacheBaseColors()
    {
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            baseColors[i] = renderers[i].material.GetColor(BaseColorId);
        }
    }

    void OnEnable()
    {
        health.Damaged += HandleDamaged;
    }

    void OnDisable()
    {
        health.Damaged -= HandleDamaged;
    }

    private void HandleDamaged(int amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Material material = renderers[i].material;

            material.DOKill();
            material.SetColor(BaseColorId, flashColor);
            material.DOColor(baseColors[i], BaseColorId, duration).SetLink(gameObject);
        }
    }
}
