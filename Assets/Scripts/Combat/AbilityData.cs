using UnityEngine;

// Ability assets store this as a number, so reordering or inserting values changes which ability an asset is
public enum AbilityType
{
    Dash,
    Shield,
    Adrenaline,
    ChargedShot,
    Focus,
    Shockwave,
    Pull,
    Mend
}

[CreateAssetMenu(fileName = "NewAbility", menuName = "KITBASH/Ability Data")]
public class AbilityData : ItemData
{
    [SerializeField] private AbilityType type;
    [SerializeField] private float cooldown = 5f;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float magnitude;
    [SerializeField] private float secondaryMagnitude;
    [SerializeField] private float tertiaryMagnitude;

    public float TertiaryMagnitude => tertiaryMagnitude;
    public AbilityType Type => type;
    public float Cooldown => cooldown;
    public float Duration => duration;
    public float Magnitude => magnitude;
    public float SecondaryMagnitude => secondaryMagnitude;
}
