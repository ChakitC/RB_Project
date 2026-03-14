using UnityEngine;

[DefaultExecutionOrder(-110)]
public class StatsHub : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private WeaponSystem weapon; // รู้ว่าอาวุธปัจจุบันคืออะไร

    [Header("Debug (Inspector)")]
    [SerializeField] private bool debugInInspector = true;
    [SerializeField] private bool useUnscaledTime = false;
    [Tooltip("0 = อัปเดตทุกเฟรม, >0 = อัปเดตตามวินาที")]
    [SerializeField, Min(0f)] private float debugRefreshInterval = 0f;

    // --- read-only debug values ---
    [Header("Debug Values (read-only)")]
    [SerializeField] private string dbgWeaponName;
    [SerializeField] private WeaponType dbgWeaponType;
    [SerializeField] private FiringMode dbgFiringMode;

    [SerializeField] private float dbgBaseCharDamage;
    [SerializeField] private float dbgBaseCharCritRatePercent; // 0..100
    [SerializeField] private float dbgBaseCharCritMult;        // 2.0 = 200%

    [SerializeField] private float dbgWeaponDamage;
    [SerializeField] private float dbgWeaponCritRatePercent;   // 0..100
    [SerializeField] private float dbgWeaponCritMult;          // 2.0 = 200%
    [SerializeField] private float dbgWeaponFireInterval;      // เวลา/นัด (seconds/shot)
    [SerializeField] private float dbgWeaponStability;
    [SerializeField] private float dbgWeaponBulletSpeed;
    [SerializeField] private int dbgWeaponMaxMagazine;

    [SerializeField] private float dbgFinalDamage;
    [SerializeField] private float dbgFinalCritRatePercent;    // 0..100
    [SerializeField] private float dbgFinalCritRate01;         // 0..1
    [SerializeField] private float dbgFinalCritMult;           // 2.0 = 200%
    [SerializeField] private float dbgFinalFireInterval;
    [SerializeField] private float dbgFinalStability;
    [SerializeField] private float dbgFinalBulletSpeed;
    [SerializeField] private int dbgFinalMaxMagazine;

    const float BASE_CRIT_MULT = 2f;

    float _nextDebugRefreshTime = -1f;
    GunConfig _lastWeapon;

    void Awake()
    {
        if (!ctx) TryGetComponent(out ctx);
        if (!weapon) TryGetComponent(out weapon);
    }

    void OnEnable()
    {
        // ให้รีเฟรชทันทีเมื่อเปิดใช้งาน
        _nextDebugRefreshTime = -1f;
        _lastWeapon = null;
    }

    void Update()
    {
        if (!debugInInspector) return;

        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        var w = GetCurrentWeapon();

        // ถ้าเปลี่ยนอาวุธ -> รีเฟรชทันที
        if (w != _lastWeapon)
        {
            _lastWeapon = w;
            _nextDebugRefreshTime = -1f;
        }

        if (debugRefreshInterval <= 0f)
        {
            RefreshDebug(w);
            return;
        }

        if (_nextDebugRefreshTime < 0f || t >= _nextDebugRefreshTime)
        {
            RefreshDebug(w);
            _nextDebugRefreshTime = t + debugRefreshInterval;
        }
    }

    void RefreshDebug(GunConfig w)
    {
        dbgWeaponName = w ? w.name : "<none>";
        dbgWeaponType = w ? w.WeaponType : default;   // ถ้ามี WeaponType.None ก็เปลี่ยนเป็น None ได้
        dbgFiringMode = w ? w.firingModes : default;  // ถ้ามี FiringMode.None ก็เปลี่ยนเป็น None ได้

        // base from character/context
        dbgBaseCharDamage = ctx ? ctx.baseDamage : 0f;
        dbgBaseCharCritRatePercent = ctx ? ctx.basecritRate : 0f;          // 0..100
        dbgBaseCharCritMult = ctx ? ctx.basecritMultiplier : BASE_CRIT_MULT; // 2.0 = 200%

        // base from weapon
        dbgWeaponDamage = w ? w.damage : 0f;
        dbgWeaponCritRatePercent = w ? w.critRate : 0f;                 // 0..100
        dbgWeaponCritMult = w ? w.critMultiplier : BASE_CRIT_MULT;      // 2.0 = 200%
        dbgWeaponFireInterval = w ? w.fireRate : 0f;                    // เวลา/นัด
        dbgWeaponStability = w ? w.stability : 0f;
        dbgWeaponBulletSpeed = w ? w.BulletSpeed : 0f;
        dbgWeaponMaxMagazine = w ? w.maxMagazine : 0;

        // final (char + weapon; ต่อไปค่อยบวก modifier/buff)
        dbgFinalDamage = GetDamage(w);
        dbgFinalCritRatePercent = GetCritRatePercent(w);
        dbgFinalCritRate01 = dbgFinalCritRatePercent / 100f;
        dbgFinalCritMult = GetCritMultiplier(w);
        dbgFinalFireInterval = GetFireInterval(w);
        dbgFinalStability = GetStability(w);
        dbgFinalBulletSpeed = GetBulletSpeed(w);
        dbgFinalMaxMagazine = GetMaxMagazine(w);
    }

    GunConfig GetCurrentWeapon()
    {
        // สำคัญ: ให้ใช้ชื่อที่มีจริงใน WeaponSystem ของคุณ
        // ถ้าคุณมี property ชื่อ CurrentWeapon ให้สลับไปใช้ weapon.CurrentWeapon
        if (weapon && weapon.CurrentWeapon) return weapon.CurrentWeapon;
        if (ctx && ctx.currentWeapon) return ctx.currentWeapon;
        return null;
    }

    // -------------------- Public API  --------------------
    public GunConfig CurrentWeapon => GetCurrentWeapon();

    public float Damage => GetDamage(CurrentWeapon);
    public float CritRatePercent => GetCritRatePercent(CurrentWeapon); // 0..100
    public float CritRate01 => CritRatePercent / 100f;                 // 0..1
    public float CritMultiplier => GetCritMultiplier(CurrentWeapon);   // 2.0 = 200%
    public float FireInterval => GetFireInterval(CurrentWeapon);
    public float Stability => GetStability(CurrentWeapon);
    public float BulletSpeed => GetBulletSpeed(CurrentWeapon);
    public int MaxMagazine => GetMaxMagazine(CurrentWeapon);
    public float ReloadTime => GetReloadTime(CurrentWeapon);

    // -------------------- Math --------------------
    public float GetFireInterval(GunConfig w) => w ? w.fireRate : 0f;

    public float GetDamage(GunConfig w)
        => (w ? w.damage : 0f) + (ctx ? ctx.baseDamage + ctx.baseStats.DamageScaling * ctx.levelSystem.Level : 0f);

    // critRate นิยาม: 0..100 (%)
    public float GetCritRatePercent(GunConfig w)
    {
        float pct = (w ? w.critRate : 0f) + (ctx ? ctx.basecritRate + ctx.baseStats.CritrateScaling * ctx.levelSystem.Level : 0f);
        return Mathf.Clamp(pct, 0f, 100f);
    }

    // critMultiplier นิยาม: 2.0 = 200% (ตัวคูณเต็ม)
    // แนวคิด: เอาค่า "ฐานอาวุธ" + "โบนัสจากตัวละคร"
    // โดยโบนัสตัวละคร = (ctx.basecritMultiplier - 2.0)
    
    float GetCharCritMult()
    {
        if (!ctx) return BASE_CRIT_MULT;

        float lvl = ctx.levelSystem.Level;
        float mult = ctx.basecritMultiplier + ctx.baseStats.CritDamageScaling * lvl; 

        return Mathf.Max(1f, mult);
    }

    public float GetCritMultiplier(GunConfig w)
    {
        float weaponMult = w ? Mathf.Max(1f, w.critMultiplier) : BASE_CRIT_MULT;
        float charBonus = GetCharCritMult() - BASE_CRIT_MULT; // ctx=2.0 -> bonus=0
        return Mathf.Max(1f, weaponMult + charBonus);
    }
    public float GetReloadTime(GunConfig w) => w ? w.reloadTime : 0;
    
    public float GetStability(GunConfig w) => w ? w.stability : 0f;
    public float GetBulletSpeed(GunConfig w) => w ? w.BulletSpeed : 0f;
    public int GetMaxMagazine(GunConfig w) => w ? w.maxMagazine : 0;

    // -------------------- Character-only getters --------------------
    public float GetMaximumHealth() => ctx ? ctx.basemaxHealth   + ctx.baseStats.MAXHPScaling   * ctx.levelSystem.Level  : 0f;
    public float GetArmor() => ctx ? ctx.basearmor + ctx.baseStats.ArmorScaling * ctx.levelSystem.Level : 0f;
    public float GetMoveSpeed() => ctx ? ctx.baseSpeed + ctx.baseStats.SpeedScaling * ctx.levelSystem.Level: 0f;
    public float GetMaximumStamina() => ctx ? ctx.baseStamina + ctx.baseStats.StaminaScaling * ctx.levelSystem.Level: 0f;
    public float GetMaximumEnergy() => ctx ? ctx.baseEnagy + ctx.baseStats.EnagyScaling * ctx.levelSystem.Level: 0f;

    // (ถ้าต้องการใช้สกิลอิง baseDamage ตรง ๆ) 
    public float GetSkillBaseDamage() => ctx ? ctx.baseDamage + ctx.baseStats.DamageScaling * ctx.levelSystem.Level : 0f;
}
