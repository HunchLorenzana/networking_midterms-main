using UnityEngine;

[CreateAssetMenu(fileName = "NewPassive", menuName = "KITBASH/Passive Data")]
public class PassiveData : ItemData
{
    [Header("Stat changes (fractions: 0.2 = +20%, -0.1 = -10%)")]
    [SerializeField] private float maxHealthPercent;
    [SerializeField] private float moveSpeedPercent;
    [SerializeField] private float damageDealtPercent;
    [SerializeField] private float damageTakenPercent;
    [SerializeField] private float reloadSpeedPercent;
    [SerializeField] private float projectileSpeedPercent;
    [SerializeField] private float rangePercent;

    [Header("Effect")]
    [SerializeField] private PassiveEffect effect;
    [Tooltip("Lifesteal: heal fraction of damage dealt. Ricochet: damage multiplier of bounced hits. Weaken: damage taken multiplier on the target. Scavenger: rounds restored per kill. Second Wind: health fraction per tick. Adrenal Surge: speed bonus fraction. Thick Skin: damage reduction fraction on the first hit after spawning")]
    [SerializeField] private float magnitude;
    [Tooltip("Ricochet: bounce range in meters. Weaken: target speed multiplier. Second Wind: seconds between ticks")]
    [SerializeField] private float secondaryMagnitude;
    [Tooltip("Weaken: debuff seconds. Second Wind: seconds without damage before regeneration starts. Adrenal Surge: boost seconds")]
    [SerializeField] private float duration;
    [Tooltip("Adrenal Surge: seconds before it can trigger again")]
    [SerializeField] private float cooldown;

    public float MaxHealthPercent => maxHealthPercent;
    public float MoveSpeedPercent => moveSpeedPercent;
    public float DamageDealtPercent => damageDealtPercent;
    public float DamageTakenPercent => damageTakenPercent;
    public float ReloadSpeedPercent => reloadSpeedPercent;
    public float ProjectileSpeedPercent => projectileSpeedPercent;
    public float RangePercent => rangePercent;
    public PassiveEffect Effect => effect;
    public float Magnitude => magnitude;
    public float SecondaryMagnitude => secondaryMagnitude;
    public float Duration => duration;
    public float Cooldown => cooldown;
}
