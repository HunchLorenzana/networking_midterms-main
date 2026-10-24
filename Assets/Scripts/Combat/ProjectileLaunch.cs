using UnityEngine;

public struct ProjectileLaunch
{
    public WeaponData Weapon { get; set; }
    public Vector3 Origin { get; set; }
    public Vector3 Direction { get; set; }
    public float Range { get; set; }
    public float RangeMultiplier { get; set; }
    public float DamageMultiplier { get; set; }
    public int Bounces { get; set; }
    public float BounceRange { get; set; }
    public float SpeedMultiplier { get; set; }
    public LayerMask HitMask { get; set; }
    public Health Owner { get; set; }
    public IProjectileListener Listener { get; set; }
}
