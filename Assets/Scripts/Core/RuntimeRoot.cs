using UnityEngine;

public class RuntimeRoot : MonoBehaviour
{
    public static RuntimeRoot Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
