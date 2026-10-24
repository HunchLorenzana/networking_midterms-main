using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapCatalog", menuName = "KITBASH/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    [SerializeField] private MapDefinition[] maps = new MapDefinition[0];
    [Tooltip("Loaded for Practice, not part of the match rotation")]
    [SerializeField] private MapDefinition practiceMap;

    private readonly List<MapDefinition> candidates = new List<MapDefinition>();

    public IReadOnlyList<MapDefinition> Maps => maps;
    public MapDefinition PracticeMap => practiceMap;

    void OnValidate()
    {
        HashSet<int> seen = new HashSet<int>();

        foreach (MapDefinition map in maps)
        {
            if (map == null) continue;

            if (map.Id == 0)
            {
                Debug.LogWarning("Map has no permanent ID: " + map.name, map);
            }
            else if (!seen.Add(map.Id))
            {
                Debug.LogWarning("Duplicate map ID " + map.Id + " on " + map.name, map);
            }
        }
    }

    // A random map in rotation that supports the mode, or null when there is none
    public MapDefinition PickRandom(GameMode mode)
    {
        candidates.Clear();

        foreach (MapDefinition map in maps)
        {
            if (map != null && map.IsInRotation && map.Supports(mode))
            {
                candidates.Add(map);
            }
        }

        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }
}
