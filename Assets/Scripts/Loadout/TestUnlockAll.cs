using UnityEngine;

// Placed only in test scenes: every item counts as owned while this scene is open
public class TestUnlockAll : MonoBehaviour
{
    void Awake()
    {
        ProfileService.SetEverythingUnlocked(true);
    }

    void OnDestroy()
    {
        ProfileService.SetEverythingUnlocked(false);
    }
}
