using UnityEngine;

[CreateAssetMenu(fileName = "MapDefinition", menuName = "KITBASH/Map Definition")]
public class MapDefinition : ScriptableObject
{
    [Tooltip("Permanent unique ID (4xxx). Never change or reuse it")]
    [SerializeField] private int id;
    [SerializeField] private string displayName;
    [Tooltip("The map scene's name exactly as in Build Settings, for example MapTrainCanyon")]
    [SerializeField] private string sceneName;
    [SerializeField] private GameMode[] supportedModes = { GameMode.FreeForAll, GameMode.TeamDeathmatch };
    [SerializeField] private Sprite previewImage;
    [Tooltip("Only maps in rotation can be picked for matches. Leave off while a map is being built")]
    [SerializeField] private bool isInRotation;

    public int Id => id;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public Sprite PreviewImage => previewImage;
    public bool IsInRotation => isInRotation;

    public bool Supports(GameMode mode)
    {
        return System.Array.IndexOf(supportedModes, mode) >= 0;
    }
}
