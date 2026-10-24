using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugTools : MonoBehaviour
{
    [SerializeField] private DummyEnemy dummyPrefab;

#if UNITY_EDITOR
    private readonly List<DummyEnemy> dummies = new List<DummyEnemy>();

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !NetworkManager.Singleton.IsServer || PlayerController.Local == null) return;

        if (keyboard.tKey.wasPressedThisFrame)
        {
            SpawnDummy(false);
        }

        if (keyboard.yKey.wasPressedThisFrame)
        {
            SpawnDummy(true);
        }

        if (keyboard.uKey.wasPressedThisFrame)
        {
            ClearDummies();
        }

        if (keyboard.kKey.wasPressedThisFrame)
        {
            KillLocalPlayerWithDummy();
        }
    }

    private void SpawnDummy(bool isChasing)
    {
        Vector3 aimPoint = PlayerController.Local.InputReader.AimPoint;
        DummyEnemy dummy = Instantiate(dummyPrefab, new Vector3(aimPoint.x, 0.1f, aimPoint.z), Quaternion.identity);

        dummy.NetworkObject.Spawn();
        dummy.Init(isChasing);
        dummies.Add(dummy);
    }

    private void ClearDummies()
    {
        foreach (DummyEnemy dummy in dummies)
        {
            if (dummy != null)
            {
                dummy.NetworkObject.Despawn();
            }
        }

        dummies.Clear();
    }

    private void KillLocalPlayerWithDummy()
    {
        dummies.RemoveAll(dummy => dummy == null);

        if (dummies.Count == 0) return;

        PlayerController.Local.Health.TakeDamage(1000, dummies[0].Health);
    }
#else
    void Awake()
    {
        Destroy(this);
    }
#endif
}
