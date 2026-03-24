using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-110)]
public class StatsHub : MonoBehaviour
{
    const float BASE_CRIT_MULT = 1f;

    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private WeaponSystem weapon;
    [SerializeField] private StatusEffectController statusEffectController;

    [Header("Debug (Inspector)")]
    [SerializeField] private bool debugInInspector = true;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField, Min(0f)] private float debugRefreshInterval = 0f;

    [Header("Debug Values (read-only)")]
    [SerializeField] private string dbgWeaponName;
    [SerializeField] private WeaponType dbgWeaponType;
    [SerializeField] private FiringMode dbgFiringMode;

    [SerializeField] private float dbgBaseCharDamage;
    [SerializeField] private float dbgBaseCharCritRatePercent;
    [SerializeField] private float dbgBaseCharCritMult;

    [SerializeField] private float dbgWeaponDamage;
    [SerializeField] private float dbgWeaponCritRatePercent;
    [SerializeField] private float dbgWeaponCritMult;
    [SerializeField] private float dbgWeaponFireInterval;
    [SerializeField] private float dbgWeaponReloadTime;
    [SerializeField] private float dbgWeaponStability;
    [SerializeField] private float dbgWeaponBulletSpeed;
    [SerializeField] private int dbgWeaponMaxMagazine;

    [SerializeField] private float dbgFinalDamage;
    [SerializeField] private float dbgFinalArmor;
    [SerializeField] private float dbgFinalMoveSpeed;
    [SerializeField] private float dbgFinalCritRatePercent;
    [SerializeField] private float dbgFinalCritRate01;
    [SerializeField] private float dbgFinalCritMult;
    [SerializeField] private float dbgFinalFireInterval;
    [SerializeField] private float dbgFinalReloadTime;
    [SerializeField] private float dbgFinalStability;
    [SerializeField] private float dbgFinalBulletSpeed;
    [SerializeField] private int dbgFinalMaxMagazine;
    [SerializeField] private float dbgFinalMaxHP;
    [SerializeField] private float dbgFinalMaxStamina;
    [SerializeField] private float dbgFinalMaxEnergy;

    float _nextDebugRefreshTime = -1f;
    bool _isDirty = true;
    GunConfig _cachedWeapon;

    readonly List<IStatModifierProvider> _modifierProviders = new();
    readonly List<RuntimeStatModifier> _modifierBuffer = new();

    float _cachedDamage;
    float _cachedArmor;
    float _cachedMoveSpeed;
    float _cachedCritRatePercent;
    float _cachedCritMultiplier;
    float _cachedFireInterval;
    float _cachedReloadTime;
    float _cachedStability;
    float _cachedBulletSpeed;
    int _cachedMaxMagazine;
    float _cachedMaxHealth;
    float _cachedMaxStamina;
    float _cachedMaxEnergy;
    float _cachedSkillBaseDamage;

    void Awake()
    {
        if (!ctx) TryGetComponent(out ctx);
        if (!weapon) TryGetComponent(out weapon);
        if (!statusEffectController) TryGetComponent(out statusEffectController);
        RebuildModifierProviders();
    }

    void OnEnable()
    {
        RebuildModifierProviders();
        _nextDebugRefreshTime = -1f;
        MarkDirty();
    }

    void Update()
    {
        if (!debugInInspector)
            return;

        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        var w = GetCurrentWeapon();

        if (w != _cachedWeapon)
            MarkDirty();

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

    public void MarkDirty()
    {
        _isDirty = true;
    }

    public void RebuildModifierProviders()
    {
        _modifierProviders.Clear();

        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i] == this)
                continue;

            if (behaviours[i] is IStatModifierProvider provider)
                _modifierProviders.Add(provider);
        }
    }

    void RefreshDebug(GunConfig w)
    {
        EnsureCacheFresh(w);

        dbgWeaponName = w ? w.name : "<none>";
        dbgWeaponType = w ? w.WeaponType : default;
        dbgFiringMode = w ? w.firingModes : default;

        dbgBaseCharDamage = GetCharacterDamageBase();
        dbgBaseCharCritRatePercent = GetCharacterCritRateBase();
        dbgBaseCharCritMult = GetCharacterCritMultiplierBase();

        dbgWeaponDamage = w ? w.damage : 0f;
        dbgWeaponCritRatePercent = w ? w.critRate : 0f;
        dbgWeaponCritMult = w ? Mathf.Max(1f, w.critMultiplier) : BASE_CRIT_MULT;
        dbgWeaponFireInterval = w ? w.fireRate : 0f;
        dbgWeaponReloadTime = w ? w.reloadTime : 0f;
        dbgWeaponStability = w ? w.stability : 0f;
        dbgWeaponBulletSpeed = w ? w.BulletSpeed : 0f;
        dbgWeaponMaxMagazine = w ? w.maxMagazine : 0;

        dbgFinalDamage = _cachedDamage;
        dbgFinalArmor = _cachedArmor;
        dbgFinalMoveSpeed = _cachedMoveSpeed;
        dbgFinalCritRatePercent = _cachedCritRatePercent;
        dbgFinalCritRate01 = _cachedCritRatePercent / 100f;
        dbgFinalCritMult = _cachedCritMultiplier;
        dbgFinalFireInterval = _cachedFireInterval;
        dbgFinalReloadTime = _cachedReloadTime;
        dbgFinalStability = _cachedStability;
        dbgFinalBulletSpeed = _cachedBulletSpeed;
        dbgFinalMaxMagazine = _cachedMaxMagazine;
        dbgFinalMaxHP = _cachedMaxHealth;
        dbgFinalMaxStamina = _cachedMaxStamina;
        dbgFinalMaxEnergy = _cachedMaxEnergy;
    }

    void EnsureCacheFresh(GunConfig w)
    {
        if (!_isDirty && w == _cachedWeapon)
            return;

        RecalculateCache(w);
    }

    void RecalculateCache(GunConfig w)
    {
        _cachedWeapon = w;

        float characterDamage = GetCharacterDamageBase();
        float characterArmor = GetCharacterArmorBase();
        float characterMoveSpeed = GetCharacterMoveSpeedBase();
        float characterCritRate = GetCharacterCritRateBase();
        float characterCritMult = GetCharacterCritMultiplierBase();
        float characterMaxHealth = GetCharacterMaxHealthBase();
        float characterMaxStamina = GetCharacterMaxStaminaBase();
        float characterMaxEnergy = GetCharacterMaxEnergyBase();

        float weaponDamage = w ? w.damage : 0f;
        float weaponCritRate = w ? w.critRate : 0f;
        float weaponCritMult = w ? Mathf.Max(1f, w.critMultiplier) : BASE_CRIT_MULT;
        float weaponFireInterval = w ? w.fireRate : 0f;
        float weaponReloadTime = w ? w.reloadTime : 0f;
        float weaponStability = w ? w.stability : 0f;
        float weaponBulletSpeed = w ? w.BulletSpeed : 0f;
        float weaponMagazine = w ? w.maxMagazine : 0f;

        _cachedDamage = Mathf.Max(0f, ApplyStatusModifiers(StatType.Damage, weaponDamage + characterDamage));
        _cachedArmor = Mathf.Max(0f, ApplyStatusModifiers(StatType.Armor, characterArmor));
        _cachedMoveSpeed = Mathf.Max(0f, ApplyStatusModifiers(StatType.MoveSpeed, characterMoveSpeed));
        _cachedCritRatePercent = Mathf.Clamp(ApplyStatusModifiers(StatType.CritRate, weaponCritRate + characterCritRate), 0f, 100f);
        _cachedCritMultiplier = Mathf.Max(1f, ApplyStatusModifiers(StatType.CritMultiplier, weaponCritMult + (characterCritMult - BASE_CRIT_MULT)));
        _cachedFireInterval = Mathf.Max(0.01f, ApplyStatusModifiers(StatType.FireInterval, weaponFireInterval));
        _cachedReloadTime = Mathf.Max(0f, ApplyStatusModifiers(StatType.ReloadTime, weaponReloadTime));
        _cachedStability = Mathf.Max(0f, ApplyStatusModifiers(StatType.Stability, weaponStability));
        _cachedBulletSpeed = Mathf.Max(0f, ApplyStatusModifiers(StatType.BulletSpeed, weaponBulletSpeed));
        _cachedMaxMagazine = Mathf.Max(0, Mathf.RoundToInt(ApplyStatusModifiers(StatType.MaxMagazine, weaponMagazine)));
        _cachedMaxHealth = Mathf.Max(1f, ApplyStatusModifiers(StatType.MaxHP, characterMaxHealth));
        _cachedMaxStamina = Mathf.Max(1f, ApplyStatusModifiers(StatType.MaxStamina, characterMaxStamina));
        _cachedMaxEnergy = Mathf.Max(0f, ApplyStatusModifiers(StatType.MaxEnergy, characterMaxEnergy));
        _cachedSkillBaseDamage = Mathf.Max(0f, ApplyStatusModifiers(StatType.Damage, characterDamage));

        _isDirty = false;
    }

    float ApplyStatusModifiers(StatType statType, float baseValue)
    {
        float flat = 0f;
        float addPercent = 0f;
        float multiply = 1f;

        if (_modifierProviders.Count == 0)
            RebuildModifierProviders();

        _modifierBuffer.Clear();

        for (int i = 0; i < _modifierProviders.Count; i++)
        {
            var provider = _modifierProviders[i];
            if (provider == null)
                continue;

            provider.AppendStatModifiers(_modifierBuffer);
        }

        for (int i = 0; i < _modifierBuffer.Count; i++)
        {
            var modifier = _modifierBuffer[i];
            if (modifier.StatType != statType)
                continue;

            switch (modifier.Operation)
            {
                case ModifierOp.Flat:
                    flat += modifier.Value;
                    break;

                case ModifierOp.AddPercent:
                    addPercent += modifier.Value;
                    break;

                case ModifierOp.Multiply:
                    multiply *= Mathf.Max(0f, 1f + modifier.Value);
                    break;
            }
        }

        return (baseValue + flat) * (1f + addPercent) * multiply;
    }

    float GetCharacterDamageBase()
    {
        return ctx ? ctx.baseDamage + ctx.baseStats.DamageScaling * GetLevel() : 0f;
    }

    float GetCharacterArmorBase()
    {
        return ctx ? ctx.basearmor + ctx.baseStats.ArmorScaling * GetLevel() : 0f;
    }

    float GetCharacterMoveSpeedBase()
    {
        return ctx ? ctx.baseSpeed + ctx.baseStats.SpeedScaling * GetLevel() : 0f;
    }

    float GetCharacterCritRateBase()
    {
        return ctx ? ctx.basecritRate + ctx.baseStats.CritrateScaling * GetLevel() : 0f;
    }

    float GetCharacterCritMultiplierBase()
    {
        if (!ctx)
            return BASE_CRIT_MULT;

        return Mathf.Max(1f, ctx.basecritMultiplier + ctx.baseStats.CritDamageScaling * GetLevel());
    }

    float GetCharacterMaxHealthBase()
    {
        return ctx ? ctx.basemaxHealth + ctx.baseStats.MAXHPScaling * GetLevel() : 0f;
    }

    float GetCharacterMaxStaminaBase()
    {
        return ctx ? ctx.baseStamina + ctx.baseStats.StaminaScaling * GetLevel() : 0f;
    }

    float GetCharacterMaxEnergyBase()
    {
        return ctx ? ctx.baseEnagy + ctx.baseStats.EnagyScaling * GetLevel() : 0f;
    }

    float GetLevel()
    {
        return ctx != null && ctx.levelSystem != null ? ctx.levelSystem.Level : 0f;
    }

    GunConfig GetCurrentWeapon()
    {
        if (weapon && weapon.CurrentWeapon) return weapon.CurrentWeapon;
        if (ctx && ctx.currentWeapon) return ctx.currentWeapon;
        return null;
    }

    float GetDamageInternal(GunConfig w)
    {
        return Mathf.Max(0f, ApplyStatusModifiers(StatType.Damage, (w ? w.damage : 0f) + GetCharacterDamageBase()));
    }

    float GetCritRatePercentInternal(GunConfig w)
    {
        float baseValue = (w ? w.critRate : 0f) + GetCharacterCritRateBase();
        return Mathf.Clamp(ApplyStatusModifiers(StatType.CritRate, baseValue), 0f, 100f);
    }

    float GetCritMultiplierInternal(GunConfig w)
    {
        float weaponMult = w ? Mathf.Max(1f, w.critMultiplier) : BASE_CRIT_MULT;
        float charBonus = GetCharacterCritMultiplierBase() - BASE_CRIT_MULT;
        return Mathf.Max(1f, ApplyStatusModifiers(StatType.CritMultiplier, weaponMult + charBonus));
    }

    float GetFireIntervalInternal(GunConfig w)
    {
        return Mathf.Max(0.01f, ApplyStatusModifiers(StatType.FireInterval, w ? w.fireRate : 0f));
    }

    float GetReloadTimeInternal(GunConfig w)
    {
        return Mathf.Max(0f, ApplyStatusModifiers(StatType.ReloadTime, w ? w.reloadTime : 0f));
    }

    float GetStabilityInternal(GunConfig w)
    {
        return Mathf.Max(0f, ApplyStatusModifiers(StatType.Stability, w ? w.stability : 0f));
    }

    float GetBulletSpeedInternal(GunConfig w)
    {
        return Mathf.Max(0f, ApplyStatusModifiers(StatType.BulletSpeed, w ? w.BulletSpeed : 0f));
    }

    int GetMaxMagazineInternal(GunConfig w)
    {
        return Mathf.Max(0, Mathf.RoundToInt(ApplyStatusModifiers(StatType.MaxMagazine, w ? w.maxMagazine : 0f)));
    }

    public GunConfig CurrentWeapon => GetCurrentWeapon();

    public float Damage => GetDamage(CurrentWeapon);
    public float CritRatePercent => GetCritRatePercent(CurrentWeapon);
    public float CritRate01 => CritRatePercent / 100f;
    public float CritMultiplier => GetCritMultiplier(CurrentWeapon);
    public float FireInterval => GetFireInterval(CurrentWeapon);
    public float Stability => GetStability(CurrentWeapon);
    public float BulletSpeed => GetBulletSpeed(CurrentWeapon);
    public int MaxMagazine => GetMaxMagazine(CurrentWeapon);
    public float ReloadTime => GetReloadTime(CurrentWeapon);

    public float GetFireInterval(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedFireInterval;
        }

        return GetFireIntervalInternal(w);
    }

    public float GetDamage(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedDamage;
        }

        return GetDamageInternal(w);
    }

    public float GetCritRatePercent(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedCritRatePercent;
        }

        return GetCritRatePercentInternal(w);
    }

    public float GetCritMultiplier(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedCritMultiplier;
        }

        return GetCritMultiplierInternal(w);
    }

    public float GetReloadTime(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedReloadTime;
        }

        return GetReloadTimeInternal(w);
    }

    public float GetStability(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedStability;
        }

        return GetStabilityInternal(w);
    }

    public float GetBulletSpeed(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedBulletSpeed;
        }

        return GetBulletSpeedInternal(w);
    }

    public int GetMaxMagazine(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedMaxMagazine;
        }

        return GetMaxMagazineInternal(w);
    }

    public float GetMaximumHealth()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedMaxHealth;
    }

    public float GetArmor()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedArmor;
    }

    public float GetMoveSpeed()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedMoveSpeed;
    }

    public float GetMaximumStamina()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedMaxStamina;
    }

    public float GetMaximumEnergy()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedMaxEnergy;
    }

    public float GetSkillBaseDamage()
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return _cachedSkillBaseDamage;
    }
}
