using UnityEngine;

public enum WeaponSwitchAnimation
{
    Flick,
    Spin,
    Raise
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "KITBASH/Weapon Data")]
public class WeaponData : ItemData
{
    [SerializeField] private int damagePerPellet = 12;
    [SerializeField] private int pelletCount = 1;
    [SerializeField] private float fireInterval = 0.125f;
    [SerializeField] private int magazineSize = 30;
    [Tooltip("Seconds to regenerate from empty to full")]
    [SerializeField] private float reloadTime = 1.8f;
    [Tooltip("Seconds after the last shot before ammo starts regenerating")]
    [SerializeField] private float regenDelay = 0.6f;
    [Tooltip("Hold To Fire: holding shoots automatically while dragging. Aim And Release: hold and drag to aim, release to shoot")]
    [SerializeField] private WeaponFireMode fireMode;
    [SerializeField] private float spreadAngle = 2f;
    [SerializeField] private float range = 45f;
    [SerializeField] private float falloffStart;
    [SerializeField] private float falloffEnd;


    [Header("Projectile")]
    [Tooltip("Projectile speed in meters per second")]
    [SerializeField] private float projectileSpeed = 25f;
    [Tooltip("Hit size of the bullet in meters (an invisible sphere swept along its path to detect hits). Gameplay only, not the look: use the bullet prefab for the look. Also sets the hit width of the Knife. Visible as the cyan wire in the setup window")]
    [SerializeField] private float projectileRadius = 0.25f;
    [Tooltip("Only used when there is no Bullet Prefab (the plain line look). With a prefab, the prefab sets the length")]
    [SerializeField] private float projectileLength = 0.6f;
    [Tooltip("Bullet model prefab (empty parent, model child pointing along Z). Its own scale sets the bullet size. Empty uses a plain line using Projectile Length and Tracer Width")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Multiplier on the bullet prefab size. The prefab itself (its Scale) sets the bullet size")]
    [SerializeField] private float bulletScale = 1f;
    [Tooltip("The projectile explodes where it lands (on a player, a wall or at the end of its range) and damages everything within the explosion radius instead of one target")]
    [SerializeField] private bool explosive;

    [Header("Aim indicator")]
    [SerializeField] private AimIndicatorShape indicatorShape = AimIndicatorShape.Line;
    [SerializeField] private Color indicatorColor = new Color(1f, 1f, 1f, 0.35f);
    [Tooltip("Indicator width in meters, 0 uses the projectile collision diameter")]
    [SerializeField] private float indicatorWidth;
    [Tooltip("Length of a brighter strip at the end of a line indicator, 0 for none (cone shapes ignore it)")]
    [SerializeField] private float indicatorEndLength = 0.8f;

    [Header("Handling")]
    [Tooltip("Movement speed multiplier. The changes of both weapons are added together")]
    [SerializeField] private float moveSpeedMultiplier = 1f;
    [Tooltip("Incoming damage multiplier, added together with the other weapon. Above 1 is a debuff")]
    [SerializeField] private float damageTakenMultiplier = 1f;
    [Tooltip("Max health multiplier added to the total from both weapons and the passives. Above 1 is more health")]
    [SerializeField] private float maxHealthMultiplier = 1f;
    [Tooltip("Ammo regeneration speed multiplier, above 1 refills faster")]
    [SerializeField] private float reloadSpeedMultiplier = 1f;
    [Tooltip("Spread degrees added per shot while firing")]
    [SerializeField] private float spreadGrowthPerShot;
    [SerializeField] private float maxExtraSpread;
    [Tooltip("Extra spread degrees lost per second when not firing")]
    [SerializeField] private float spreadRecoverPerSecond = 6f;
    [Tooltip("Half-angle behind the target where a hit is an instant kill, 0 disables backstabs")]
    [SerializeField] private float backstabAngle;
    [Tooltip("Melee weapons show no tracer or muzzle flash")]
    [SerializeField] private bool isMelee;

    [Header("Thrown")]
    [Tooltip("Projectile: bullets fly straight. Thrown: an arcing throw that lands at the aimed spot (up to Range) and explodes")]
    [SerializeField] private WeaponShotType shotType;
    [Tooltip("Explosion radius in meters (thrown weapons and explosive projectiles)")]
    [SerializeField] private float throwRadius = 2.5f;
    [Tooltip("Peak height of the throw arc in meters")]
    [SerializeField] private float throwArcHeight = 3f;
    [Tooltip("Model that flies through the air (empty parent, model child). Empty uses the held gun model")]
    [SerializeField] private GameObject thrownPrefab;
    [Tooltip("Size multiplier of the thrown model")]
    [SerializeField] private float thrownScale = 1f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionScale = 1f;

    [Header("Effects")]
    [SerializeField] private WeaponSwitchAnimation switchAnimation;
    [SerializeField] private Color tracerColor = new Color(1f, 0.9f, 0.4f);
    [Tooltip("Only used when there is no Bullet Prefab (the plain line look)")]
    [SerializeField] private float tracerWidth = 0.06f;
    [SerializeField] private float tracerLifetime = 0.06f;
    [SerializeField] private bool tracerFades;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashScale = 0.4f;

    [Header("Held model")]
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Vector3 modelOffset;
    [SerializeField] private Vector3 modelEuler;
    [SerializeField] private float modelScale = 1f;
    [Tooltip("Muzzle position in the model's own space. Tracers start here")]
    [SerializeField] private Vector3 muzzleOffset;

    [Header("Held guns")]
    [Tooltip("Hand holding the first gun (the Held model fields above)")]
    [SerializeField] private HoldHand hand;
    [Tooltip("Flips the first gun sideways")]
    [SerializeField] private bool mirrorModel;
    [Tooltip("More guns held at the same time, such as the second pistol of akimbo. Each gun adds a fire point at its muzzle")]
    [SerializeField] private HeldGunData[] extraGuns = new HeldGunData[0];

    [Header("Fire points")]
    [Tooltip("Each held gun already has a fire point at its muzzle. Extra points go here, for example angled shots")]
    [SerializeField] private FirePointData[] extraFirePoints = new FirePointData[0];
    [Tooltip("Simultaneous: every shot fires from all fire points. Alternating: each shot fires from the next fire point")]
    [SerializeField] private FirePattern firePattern;

    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float DamageTakenMultiplier => damageTakenMultiplier;
    public float MaxHealthMultiplier => maxHealthMultiplier;
    public bool IsMelee => isMelee;
    public float BackstabAngle => backstabAngle;
    public WeaponSwitchAnimation SwitchAnimation => switchAnimation;
    public Color TracerColor => tracerColor;
    public float TracerWidth => tracerWidth;
    public float TracerLifetime => tracerLifetime;
    public bool TracerFades => tracerFades;
    public GameObject MuzzleFlashPrefab => muzzleFlashPrefab;
    public float MuzzleFlashScale => muzzleFlashScale;
    public GameObject ModelPrefab => modelPrefab;
    public Vector3 ModelOffset => modelOffset;
    public Vector3 ModelEuler => modelEuler;
    public float ModelScale => modelScale;
    public Vector3 MuzzleOffset => muzzleOffset;
    public FirePattern Pattern => firePattern;
    public int GunCount => 1 + extraGuns.Length;
    public int FirePointCount => GunCount + extraFirePoints.Length;
    public int ProjectilesPerShot => PelletCount * (firePattern == FirePattern.Simultaneous ? FirePointCount : 1);
    public int PelletCount => pelletCount;
    public float FireInterval => fireInterval;
    public int MagazineSize => magazineSize;
    public float RefillTime => reloadTime / Mathf.Max(0.1f, reloadSpeedMultiplier);
    public float ReloadSpeedMultiplier => reloadSpeedMultiplier;
    public float SpreadGrowthPerShot => spreadGrowthPerShot;
    public float MaxExtraSpread => maxExtraSpread;
    public float SpreadRecoverPerSecond => spreadRecoverPerSecond;
    public float RegenDelay => regenDelay;
    public WeaponFireMode FireMode => fireMode;
    public float SpreadAngle => spreadAngle;
    public float Range => range;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileRadius => projectileRadius;
    public float ProjectileLength => projectileLength;
    public GameObject BulletPrefab => bulletPrefab;
    public float BulletScale => bulletScale;
    public bool Explosive => explosive;
    public AimIndicatorShape IndicatorShape => indicatorShape;
    public WeaponShotType ShotType => shotType;
    public float ThrowRadius => throwRadius;
    public float ThrowArcHeight => throwArcHeight;
    public GameObject ThrownPrefab => thrownPrefab;
    public float ThrownScale => thrownScale;
    public GameObject ExplosionPrefab => explosionPrefab;
    public float ExplosionScale => explosionScale;
    public Color IndicatorColor => indicatorColor;
    public float IndicatorWidth => indicatorWidth > 0f ? indicatorWidth : projectileRadius * 2f;
    public float IndicatorEndLength => indicatorEndLength;

    public HoldHand GetGunHand(int gun)
    {
        return gun == 0 ? hand : extraGuns[gun - 1].Hand;
    }

    public GameObject GetGunModel(int gun)
    {
        return gun == 0 ? modelPrefab : extraGuns[gun - 1].ModelPrefab;
    }

    public Vector3 GetGunOffset(int gun)
    {
        return gun == 0 ? modelOffset : extraGuns[gun - 1].ModelOffset;
    }

    public Vector3 GetGunEuler(int gun)
    {
        return gun == 0 ? modelEuler : extraGuns[gun - 1].ModelEuler;
    }

    public float GetGunScale(int gun)
    {
        return gun == 0 ? modelScale : extraGuns[gun - 1].ModelScale;
    }

    public bool GetGunMirror(int gun)
    {
        return gun == 0 ? mirrorModel : extraGuns[gun - 1].MirrorModel;
    }

    public Vector3 GetGunMuzzle(int gun)
    {
        return gun == 0 ? muzzleOffset : extraGuns[gun - 1].MuzzleOffset;
    }

    public int GetFirePointGun(int index)
    {
        return index < GunCount ? index : extraFirePoints[index - GunCount].Gun;
    }

    public Vector3 GetFirePointOffset(int index)
    {
        return index < GunCount ? GetGunMuzzle(index) : extraFirePoints[index - GunCount].Offset;
    }

    public float GetFirePointAngle(int index)
    {
        return index < GunCount ? 0f : extraFirePoints[index - GunCount].Angle;
    }

    public float GetMaxFirePointAngle()
    {
        float max = 0f;

        for (int i = 0; i < extraFirePoints.Length; i++)
        {
            max = Mathf.Max(max, Mathf.Abs(extraFirePoints[i].Angle));
        }

        return max;
    }

    public int GetFirePointMask(int shotCounter)
    {
        return firePattern == FirePattern.Simultaneous ? (1 << FirePointCount) - 1 : 1 << (shotCounter % FirePointCount);
    }

    public int GetDamage(float distance)
    {
        if (falloffEnd <= 0f || distance <= falloffStart) return damagePerPellet;
        if (distance >= falloffEnd) return 0;

        float t = (distance - falloffStart) / (falloffEnd - falloffStart);

        return Mathf.RoundToInt(Mathf.Lerp(damagePerPellet, 0f, t));
    }
}
