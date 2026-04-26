using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector; 

[CreateAssetMenu(fileName = "GunConfig", menuName = "Scriptable Objects/WeponStats")]
public class GunConfig : ItemDefinition

{
    
    private bool IsBurst => firingModes == FiringMode.Burst;
    public GameObject BulletPrefab; 
    public GameObject WeaponPrefab;

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
    public float stability = 0f;
    public float BulletSpeed = 30f;

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
