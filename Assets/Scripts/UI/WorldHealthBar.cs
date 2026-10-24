using UnityEngine;

public class WorldHealthBar : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Health health;
    [SerializeField] private MatchParticipant participant;
    [SerializeField] private Transform barRoot;
    [SerializeField] private Transform fill;
    [SerializeField] private Renderer fillRenderer;
    [SerializeField] private float width = 1.2f;
    [SerializeField] private float height = 0.12f;
    [SerializeField] private bool hideForOwner;
    [SerializeField] private Color allyColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);

    private Camera mainCamera;
    private MaterialPropertyBlock colorBlock;
    private bool hasColor;
    private bool isAllyShown;

    void Start()
    {
        mainCamera = Camera.main;
        colorBlock = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        bool isVisible = health.IsAlive && !(hideForOwner && health.IsOwner);

        barRoot.gameObject.SetActive(isVisible);

        if (!isVisible) return;

        float fillWidth = width * health.Fraction;

        barRoot.rotation = mainCamera.transform.rotation;
        fill.localScale = new Vector3(fillWidth, height, 1f);
        fill.localPosition = new Vector3((fillWidth - width) / 2f, 0f, -0.01f);
        RefreshColor();
    }

    private void RefreshColor()
    {
        bool isAlly = IsAlly();

        if (hasColor && isAlly == isAllyShown) return;

        hasColor = true;
        isAllyShown = isAlly;
        fillRenderer.GetPropertyBlock(colorBlock);
        colorBlock.SetColor(BaseColorId, isAlly ? allyColor : enemyColor);
        fillRenderer.SetPropertyBlock(colorBlock);
    }

    private bool IsAlly()
    {
        PlayerController local = PlayerController.Local;

        if (local == null) return false;

        if (local.Health == health) return true;

        MatchManager match = MatchManager.Instance;

        if (match != null && match.Phase == MatchPhase.Lobby && !health.IsTrainingTarget) return true;

        return health.TeamId >= 0 && health.TeamId == local.Health.TeamId;
    }
}
