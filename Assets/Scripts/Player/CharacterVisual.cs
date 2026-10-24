using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    private const string TORSO_NAME = "torso";

    [SerializeField] private CharacterCatalog catalog;
    [SerializeField] private MatchParticipant participant;
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private HitFlash hitFlash;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private RuntimeAnimatorController controller;
    [SerializeField] private int embeddedId;

    private GameObject currentModel;
    private Vector3 modelScale = Vector3.one;
    private int shownId;

    public Transform ModelRoot => currentModel != null ? currentModel.transform : null;

    void Awake()
    {
        Animator embedded = visualRoot.GetComponentInChildren<Animator>();

        if (embedded != null)
        {
            currentModel = embedded.gameObject;
            modelScale = currentModel.transform.localScale;
        }

        shownId = embeddedId;
    }

    void Update()
    {
        int id = participant.CharacterId;

        if (id == shownId) return;

        shownId = id;
        Rebuild(id);
    }

    private void Rebuild(int id)
    {
        if (id < 0 || id >= catalog.Count) return;

        holdPoint.SetParent(visualRoot, false);

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        currentModel = Instantiate(catalog.GetModel(id), visualRoot);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.identity;
        currentModel.transform.localScale = modelScale;

        Animator animator = currentModel.GetComponent<Animator>();

        if (animator == null)
        {
            animator = currentModel.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Transform torso = FindDeep(currentModel.transform, TORSO_NAME);

        holdPoint.SetParent(torso, false);

        Vector3 lossy = torso.lossyScale;

        holdPoint.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
        hitFlash.SetRenderers(currentModel.GetComponentsInChildren<Renderer>());
        characterAnimator.SetAnimator(animator);
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName) return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, childName);

            if (found != null) return found;
        }

        return null;
    }
}
