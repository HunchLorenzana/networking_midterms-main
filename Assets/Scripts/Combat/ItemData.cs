using UnityEngine;

public abstract class ItemData : ScriptableObject
{
    [Tooltip("Permanent unique ID. Never change or reuse it: saved presets, purchases and network messages refer to it")]
    [SerializeField] private int id;
    [SerializeField] private string displayName;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [Tooltip("Scrap price to unlock this item in the Workshop")]
    [SerializeField] private int scrapPrice;
    [Tooltip("Owned from the start, no price")]
    [SerializeField] private bool isStarter;

    public int Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public int ScrapPrice => scrapPrice;
    public bool IsStarter => isStarter;
}
