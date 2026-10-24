public readonly struct ProjectileHit
{
    public Health Target { get; }
    public WeaponData Weapon { get; }
    public float FalloffDistance { get; }
    public float DamageMultiplier { get; }
    public bool IsBounce { get; }

    public ProjectileHit(Health target, WeaponData weapon, float falloffDistance, float damageMultiplier, bool isBounce)
    {
        Target = target;
        Weapon = weapon;
        FalloffDistance = falloffDistance;
        DamageMultiplier = damageMultiplier;
        IsBounce = isBounce;
    }
}
