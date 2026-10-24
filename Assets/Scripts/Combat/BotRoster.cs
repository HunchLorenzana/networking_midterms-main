using System.Collections.Generic;
using UnityEngine;

// Everything that makes bots look and play like people: names, premade builds and skill by the player's trophies
[CreateAssetMenu(fileName = "BotRoster", menuName = "KITBASH/Bot Roster")]
public class BotRoster : ScriptableObject
{
    [System.Serializable]
    public struct BotPreset
    {
        [SerializeField] private string presetName;
        [SerializeField] private int primaryWeaponId;
        [SerializeField] private int secondaryWeaponId;
        [Tooltip("Fraction of the weapon range the bot tries to fight at: low rushes in, high keeps distance")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float preferredRangeFraction;

        public string PresetName => presetName;
        public int PrimaryWeaponId => primaryWeaponId;
        public int SecondaryWeaponId => secondaryWeaponId;
        public float PreferredRangeFraction => preferredRangeFraction;

        public BotPreset(string presetName, int primaryWeaponId, int secondaryWeaponId, float preferredRangeFraction)
        {
            this.presetName = presetName;
            this.primaryWeaponId = primaryWeaponId;
            this.secondaryWeaponId = secondaryWeaponId;
            this.preferredRangeFraction = preferredRangeFraction;
        }
    }

    [System.Serializable]
    public struct BotDifficulty
    {
        [Tooltip("The player's trophies needed for this tier")]
        [SerializeField] private int minTrophies;
        [Tooltip("Delay before shooting a new target")]
        [SerializeField] private float reactionSeconds;
        [Tooltip("Extra random aim error, in degrees")]
        [SerializeField] private float aimErrorDegrees;

        public int MinTrophies => minTrophies;
        public float ReactionSeconds => reactionSeconds;
        public float AimErrorDegrees => aimErrorDegrees;

        public BotDifficulty(int minTrophies, float reactionSeconds, float aimErrorDegrees)
        {
            this.minTrophies = minTrophies;
            this.reactionSeconds = reactionSeconds;
            this.aimErrorDegrees = aimErrorDegrees;
        }
    }

    [Tooltip("Human-like names, 12 characters at most. Bots are never labelled as bots")]
    [SerializeField] private string[] names =
    {
        "Kairo", "mika_07", "Zeph", "Lumen", "ArcadeAce", "Tobi", "nyx", "Juno22", "Blitzer", "Rook",
        "PaoloG", "sunny", "Vexa", "Dex", "KiraKira", "Momo", "ZenMode", "Rafa", "Pixel", "Hiro",
        "Ollie", "Nova", "jaybee", "Sora", "Bruno", "Tala", "Cyrus", "Lia", "Gizmo", "Ember",
        "Taro", "Rin", "Marco_P", "Ivy", "Bastian", "Kodi", "Yuki", "Remy", "Nashy", "Ezra"
    };
    [SerializeField] private BotPreset[] presets =
    {
        new BotPreset("Tank", 1004, 1002, 0.35f),
        new BotPreset("Sniper", 1003, 1005, 0.75f),
        new BotPreset("Rusher", 1005, 1006, 0.15f),
        new BotPreset("Rifleman", 1001, 1002, 0.45f),
        new BotPreset("Gunslinger", 1007, 1001, 0.3f),
        new BotPreset("Demolition", 1009, 1005, 0.5f),
        new BotPreset("Bomber", 1008, 1001, 0.5f)
    };
    [SerializeField] private BotDifficulty[] difficulties =
    {
        new BotDifficulty(0, 0.6f, 9f),
        new BotDifficulty(300, 0.45f, 6f),
        new BotDifficulty(800, 0.35f, 4f),
        new BotDifficulty(1500, 0.25f, 2.5f)
    };

    private readonly List<string> availableNames = new List<string>();

    public int PresetCount => presets.Length;

    public BotPreset GetPreset(int index)
    {
        return presets[index];
    }

    public BotPreset PickPreset()
    {
        return presets[Random.Range(0, presets.Length)];
    }

    // The highest tier the player's trophies reach
    public BotDifficulty GetDifficulty(int playerTrophies)
    {
        BotDifficulty found = difficulties[0];

        foreach (BotDifficulty difficulty in difficulties)
        {
            if (playerTrophies >= difficulty.MinTrophies)
            {
                found = difficulty;
            }
        }

        return found;
    }

    // A random name not in use yet. When every name is taken, a name gets a number added
    public string PickName(ICollection<string> namesInUse)
    {
        availableNames.Clear();

        foreach (string candidate in names)
        {
            if (!namesInUse.Contains(candidate))
            {
                availableNames.Add(candidate);
            }
        }

        if (availableNames.Count > 0) return availableNames[Random.Range(0, availableNames.Count)];

        return names[Random.Range(0, names.Length)] + Random.Range(10, 100);
    }
}
