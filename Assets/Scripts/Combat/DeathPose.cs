using DG.Tweening;
using UnityEngine;

public class DeathPose : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float fallDuration = 0.25f;

    void OnEnable()
    {
        health.Died += HandleDied;
        health.Revived += HandleRevived;
    }

    void OnDisable()
    {
        health.Died -= HandleDied;
        health.Revived -= HandleRevived;
    }

    private void HandleDied(Health killer)
    {
        visualRoot.DOKill();
        visualRoot.DOLocalRotate(new Vector3(90f, 0f, 0f), fallDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private void HandleRevived()
    {
        visualRoot.DOKill();
        visualRoot.localRotation = Quaternion.identity;
    }
}
