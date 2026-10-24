using UnityEngine;

public class ShieldVisual : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private GameObject shieldObject;

    void OnEnable()
    {
        health.ShieldChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        health.ShieldChanged -= Refresh;
    }

    private void Refresh()
    {
        shieldObject.SetActive(health.HasShield);
    }
}
