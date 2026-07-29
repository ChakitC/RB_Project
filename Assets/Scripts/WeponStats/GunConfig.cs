using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector; 

[CreateAssetMenu(fileName = "GunConfig", menuName = "Scriptable Objects/WeponStats")]
public class GunConfig : ItemDefinition

{
    
    private bool IsBurst => firingModes == FiringMode.Burst;
    public GameObject BulletPrefab; 
    public GameObject WeaponPrefab;

    [Header("Upgrade")]
    public WeaponUpgradeCurve upgradeCurve;

    [Header("Weapon Model Mount")]
    [ToggleLeft] public bool overrideWeaponModelMountOffset = false;
    public WeaponModelMountOffset rightHandWeaponModelMount = WeaponModelMountOffset.Identity;
    public WeaponModelMountOffset leftHandWeaponModelMount = WeaponModelMountOffset.Identity;

    [Header("Audio")]
    public AudioCue fireCue;
    public AudioCue hitCue;
    public AudioCue emptyCue;
    public AudioCue reloadCue;
    public AudioCue equipCue;
    
    [Header("Base Stats")]
        
    public WeaponType WeaponType;
    public FiringMode firingModes;
    public float damage = 0f;
    public float fireRate = 0f;
    public int magazine = 0;
    public int maxMagazine = 0;
    public float critRate = 0f;            
    public float critMultiplier = 1f;
    [LabelText("Stability (%)")]
    [Range(0f, 100f)]
    [Tooltip("Reduces weapon sway. 0% uses full sway and 100% removes sway completely.")]
    public float stability = 0f;
    public float BulletSpeed = 30f;

    [Header("Ammo Reserve")]
    [ToggleLeft] public bool infiniteReserveAmmo = false;

    [Tooltip("0 uses maxMagazine * 3 as the reserve cap.")]
    [Min(0)] public int maxReserveAmmo = 0;

    [Tooltip("When disabled, reserve ammo starts full.")]
    [ToggleLeft] public bool overrideStartingReserveAmmo = false;

    [ShowIf(nameof(overrideStartingReserveAmmo))]
    [Min(0)] public int startingReserveAmmo = 0;

    [Header("Stagger")]
    [Min(0f)] public float staggerPower = 10f;
    
    
    [Header("ReloadSetting")]
    public bool autoloader = false;

    [LabelText("Magazine Reload")]
    [FormerlySerializedAs("magazineRelode")] 
    public bool magazineReload = false;

    [HideIf(nameof(reloadPerBullet))]
    [Min(0f)]
    public float reloadTime = 0f;

    [Space]
    [ToggleLeft]
    public bool reloadPerBullet = false;

   
    [ShowIf(nameof(reloadPerBullet))]
    [Min(0f)]
    public float startInsertDelay = 0f;

    [ShowIf(nameof(reloadPerBullet))]
    [Min(0f)]
    public float perBulletInsertTime = 0f;

    [ShowIf(nameof(reloadPerBullet))]
    [Min(0f)]
    public float endInsertDelay = 0f;

    [ShowIf(nameof(reloadPerBullet))]
    public bool shootInterruptsReload = false;
    
    
    [Header("RecoilSettings")]
        
    public float baseSwaySpeed = 0f;
    public float baseMaxSwayAngle = 0f;
    public float baseReturnSpeed = 0f;

    [Header("Third Person Accuracy")]
    [Min(0f)] public float hipSpreadDegrees = 2.2f;
    [Min(0f)] public float aimSpreadDegrees = 0.35f;
    [Min(0f)] public float moveSpreadPenaltyDegrees = 1f;
    [Min(0f)] public float spreadPerShotDegrees = 0.45f;
    [Min(0f)] public float maximumSpreadBloomDegrees = 3f;
    [Min(0f)] public float spreadRecoveryDegreesPerSecond = 3.5f;

    [Header("Third Person Camera Recoil")]
    [Min(0f)] public float cameraRecoilPitch = 1.1f;
    [Min(0f)] public float cameraRecoilYaw = 0.45f;
    
    
    [Title("Burst Settings")]
    [ShowIf(nameof(IsBurst))] [Min(2)]
    public int burstCount = 3;

    [ShowIf(nameof(IsBurst))] [Min(0f)]
    public float burstInterval = 0.08f;

    public override GameObject ResolvePickupVisualPrefab()
    {
        if (pickupVisualPrefab != null)
            return pickupVisualPrefab;

        return WeaponPrefab;
    }
}
