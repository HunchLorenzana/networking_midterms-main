using UnityEngine;

// Marks a practice area: where players wait before a match (or practise) and where training dummies stand.
// Only the practice map and TestArena have one, match maps do not
public class PracticeArea : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [SerializeField] private Transform[] trainingDummyPoints = new Transform[0];
    [Tooltip("Pairs of points: each walking dummy patrols between two")]
    [SerializeField] private Transform[] walkingDummyPoints = new Transform[0];

    public static PracticeArea Current { get; private set; }

    public Transform[] SpawnPoints => spawnPoints;
    public Transform[] TrainingDummyPoints => trainingDummyPoints;
    public Transform[] WalkingDummyPoints => walkingDummyPoints;

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}
