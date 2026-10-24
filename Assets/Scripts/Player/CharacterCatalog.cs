using UnityEngine;

[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "KITBASH/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [SerializeField] private GameObject[] models;
    [SerializeField] private Sprite[] icons;
    [SerializeField] private string[] displayNames;

    public int Count => models.Length;

    public GameObject GetModel(int id)
    {
        return models[id];
    }

    public Sprite GetIcon(int id)
    {
        return icons[id];
    }

    public string GetDisplayName(int id)
    {
        return displayNames[id];
    }
}
