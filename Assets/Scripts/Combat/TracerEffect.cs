using UnityEngine;

public class TracerEffect : MonoBehaviour
{
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private int poolSize = 48;

    private LineRenderer[] lines;
    private float[] spawnTimes;
    private float[] lifetimes;
    private Color[] colors;
    private bool[] fades;
    private int nextIndex;

    void Start()
    {
        lines = new LineRenderer[poolSize];
        spawnTimes = new float[poolSize];
        lifetimes = new float[poolSize];
        colors = new Color[poolSize];
        fades = new bool[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject lineObject = new GameObject("Tracer" + i);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();

            lineObject.transform.SetParent(RuntimeRoot.Instance.transform);
            line.sharedMaterial = tracerMaterial;
            line.positionCount = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            lines[i] = line;
        }

        ProjectileSystem.Initialize(tracerMaterial);
        AimIndicatorSystem.Initialize();
    }

    void OnEnable()
    {
        PlayerWeapon.AnyPelletFired += ShowTracer;
    }

    void OnDisable()
    {
        PlayerWeapon.AnyPelletFired -= ShowTracer;
    }

    void Update()
    {
        if (lines == null) return;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].enabled) continue;

            float age = Time.time - spawnTimes[i];

            if (age >= lifetimes[i])
            {
                lines[i].enabled = false;
                continue;
            }

            if (!fades[i]) continue;

            Color faded = colors[i];

            faded.a *= 1f - age / lifetimes[i];
            lines[i].startColor = faded;
            lines[i].endColor = faded;
        }
    }

    private void ShowTracer(Vector3 start, Vector3 end, WeaponData weapon)
    {
        if (weapon.IsMelee) return;

        LineRenderer line = lines[nextIndex];

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = weapon.TracerWidth;
        line.endWidth = weapon.TracerFades ? weapon.TracerWidth * 0.5f : weapon.TracerWidth;
        line.startColor = weapon.TracerColor;
        line.endColor = weapon.TracerColor;
        line.enabled = true;
        spawnTimes[nextIndex] = Time.time;
        lifetimes[nextIndex] = weapon.TracerLifetime;
        colors[nextIndex] = weapon.TracerColor;
        fades[nextIndex] = weapon.TracerFades;
        nextIndex = (nextIndex + 1) % lines.Length;
    }
}
