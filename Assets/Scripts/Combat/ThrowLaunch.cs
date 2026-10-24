using UnityEngine;

public struct ThrowLaunch
{
    public WeaponData Weapon { get; set; }
    public Vector3 Origin { get; set; }
    public Vector3 Target { get; set; }
    public LayerMask HitMask { get; set; }
    public Health Owner { get; set; }
    public IProjectileListener Listener { get; set; }
}
