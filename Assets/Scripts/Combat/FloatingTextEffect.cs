using DG.Tweening;
using TMPro;
using UnityEngine;

public class FloatingTextEffect : MonoBehaviour
{
    private const float RISE_DISTANCE = 1.4f;
    private const float LIFETIME_SECONDS = 1f;
    private const float POP_SECONDS = 0.18f;
    private const float FADE_START_FRACTION = 0.6f;

    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color backstabColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private float fontSize = 7f;

    void OnEnable()
    {
        PlayerWeapon.FloatingTextRequested += Spawn;
    }

    void OnDisable()
    {
        PlayerWeapon.FloatingTextRequested -= Spawn;
    }

    private void Spawn(Vector3 position, FloatingTextKind kind)
    {
        Color color = backstabColor;
        GameObject textObject = new GameObject("FloatingText");
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        Camera view = Camera.main;

        textObject.transform.SetParent(RuntimeRoot.Instance.transform);
        textObject.transform.position = position;

        if (view != null)
        {
            textObject.transform.rotation = view.transform.rotation;
        }

        text.font = font;
        text.text = "BACKSTAB!";
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.sortingOrder = 100;

        Vector3 baseScale = Vector3.one;

        textObject.transform.localScale = baseScale * 0.2f;
        textObject.transform.DOScale(baseScale, POP_SECONDS).SetEase(Ease.OutBack).SetLink(textObject);
        textObject.transform.DOMove(position + Vector3.up * RISE_DISTANCE, LIFETIME_SECONDS).SetEase(Ease.OutCubic).SetLink(textObject);
        DOVirtual.Float(1f, 0f, LIFETIME_SECONDS * (1f - FADE_START_FRACTION), alpha =>
        {
            Color faded = color;

            faded.a = alpha;
            text.color = faded;
        }).SetDelay(LIFETIME_SECONDS * FADE_START_FRACTION).SetLink(textObject);

        Destroy(textObject, LIFETIME_SECONDS);
    }
}
