using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-110)]
public class StatsHub : MonoBehaviour
{
    const float BASE_CRIT_MULT = CharacterStatFormula.BaseCritMultiplier;
    const float MIN_STABILITY_PERCENT = CharacterStatFormula.MinStabilityPercent;
    const float MAX_STABILITY_PERCENT = CharacterStatFormula.MaxStabilityPercent;

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
    [SerializeField, InspectorName("Weapon Stability (%)")] private float dbgWeaponStability;
    [SerializeField] private float dbgWeaponBulletSpeed;
    [SerializeField] private int dbgWeaponMaxMagazine;
    [SerializeField] private int dbgWeaponMaxReserveAmmo;

    [SerializeField] private float dbgFinalDamage;
    [SerializeField] private float dbgFinalArmor;
    [SerializeField] private float dbgFinalMoveSpeed;
    [SerializeField] private float dbgFinalCritRatePercent;
    [SerializeField] private float dbgFinalCritRate01;
    [SerializeField] private float dbgFinalCritMult;
    [SerializeField] private float dbgFinalFireInterval;
    [SerializeField] private float dbgFinalReloadTime;
    [SerializeField, InspectorName("Final Stability (%)")] private float dbgFinalStability;
    [SerializeField] private float dbgFinalBulletSpeed;
    [SerializeField] private int dbgFinalMaxMagazine;
    [SerializeField] private int dbgFinalMaxReserveAmmo;
    [SerializeField] private float dbgFinalMaxHP;
    [SerializeField] private float dbgFinalMaxStamina;
    [SerializeField, InspectorName("Final Stamina Regen")] private float dbgFinalStaminaRegen;
    [SerializeField] private float dbgFinalMaxEnergy;

    float _nextDebugRefreshTime = -1f;
    bool _isDirty = true;
    GunConfig _cachedWeapon;

    readonly List<IStatModifierProvider> _modifierProviders = new();
    readonly List<RuntimeStatModifier> _modifierBuffer = new();
    readonly List<RuntimeStatModifier> _modifierProbeBuffer = new();
    readonly List<IStatModifierProvider> _subscribedModifierProviders = new();

    bool _hasCachedInputSignature;
    int _cachedInputSignature;
    int _revision;
    int _lastSignatureProbeFrame = -1;
    GunConfig _lastSignatureProbeWeapon;
    int _lastSignatureProbe;

    public event Action StatsDirty;
    public int Revision => _revision;

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
    int _cachedMaxReserveAmmo;
    float _cachedMaxHealth;
    float _cachedMaxStamina;
    float _cachedMaxEnergy;
    float _cachedSkillBaseDamage;

    void Awake()
    {
        ResolveReferences();
        RebuildModifierProviders();
    }

    void OnEnable()
    {
        ResolveReferences();
        RebuildModifierProviders();
        _nextDebugRefreshTime = -1f;
        MarkDirty();
    }

    void OnDisable()
    {
        UnsubscribeModifierProviders();
    }

    void OnDestroy()
    {
        UnsubscribeModifierProviders();
    }

    void OnTransformChildrenChanged()
    {
        if (!isActiveAndEnabled)
            return;

        RebuildModifierProviders();
        MarkDirty();
    }

    void ResolveReferences()
    {
        if (!ctx)
        {
            TryGetComponent(out ctx);
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }

        ctx?.ResolveReferences();

        if (ctx != null && ctx.StatsHub != this)
            ctx.StatsHub = this;

        if (!weapon && ctx != null)
            weapon = ctx.WeaponSystem;
        if (!weapon)
            TryGetComponent(out weapon);
        if (!weapon && ctx != null)
            weapon = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (!statusEffectController && ctx != null)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);
        if (!statusEffectController)
            TryGetComponent(out statusEffectController);
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
        StatsDirty?.Invoke();
    }

    public bool RefreshCacheIfInputsChanged(bool logSilentChange = false)
    {
        int previousRevision = _revision;
        EnsureCacheFresh(GetCurrentWeapon(), logSilentChange);
        return _revision != previousRevision;
    }

    public void RebuildModifierProviders()
    {
        UnsubscribeModifierProviders();
        _modifierProviders.Clear();

        var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i] == this)
                continue;

            if (behaviours[i] is IStatModifierProvider provider)
            {
                _modifierProviders.Add(provider);
                SubscribeModifierProvider(provider);
            }
        }

        InvalidateSignatureProbe();
    }

    void SubscribeModifierProvider(IStatModifierProvider provider)
    {
        if (provider == null)
            return;

        provider.StatModifiersChanged -= HandleModifierProviderChanged;
        provider.StatModifiersChanged += HandleModifierProviderChanged;
        _subscribedModifierProviders.Add(provider);
    }

    void UnsubscribeModifierProviders()
    {
        for (int i = 0; i < _subscribedModifierProviders.Count; i++)
        {
            var provider = _subscribedModifierProviders[i];
            if (provider != null)
                provider.StatModifiersChanged -= HandleModifierProviderChanged;
        }

        _subscribedModifierProviders.Clear();
    }

    void HandleModifierProviderChanged()
    {
        InvalidateSignatureProbe();
        MarkDirty();
    }

    void InvalidateSignatureProbe()
    {
        _lastSignatureProbeFrame = -1;
        _lastSignatureProbeWeapon = null;
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
        dbgWeaponMaxReserveAmmo = w ? WeaponInstanceFactory.ResolveMaxReserveAmmo(w) : 0;

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
        dbgFinalMaxReserveAmmo = _cachedMaxReserveAmmo;
        dbgFinalMaxHP = _cachedMaxHealth;
        dbgFinalMaxStamina = _cachedMaxStamina;
        dbgFinalStaminaRegen = ctx != null && ctx.StaminaSystem != null
            ? ctx.StaminaSystem.ResolvedRegenerationRate
            : 0f;
        dbgFinalMaxEnergy = _cachedMaxEnergy;
    }

    void EnsureCacheFresh(GunConfig w, bool logSilentChange = false)
    {
        bool wasDirty = _isDirty;
        bool weaponChanged = w != _cachedWeapon;
        bool missingSignature = !_hasCachedInputSignature;

        if (wasDirty || weaponChanged || missingSignature)
        {
            if (logSilentChange && !wasDirty && !missingSignature)
                LogSilentInputChange(w);

            RecalculateCache(w);
            return;
        }

        int currentSignature = ProbeInputSignature(w);
        if (currentSignature == _cachedInputSignature)
            return;

        if (logSilentChange)
            LogSilentInputChange(w);

        RecalculateCache(w, _modifierProbeBuffer, currentSignature);
    }

    void LogSilentInputChange(GunConfig w)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string weaponName = w ? w.name : "<none>";
        Debug.LogWarning(
            $"StatsHub input changed without dirty event. Check IStatModifierProvider.StatModifiersChanged for weapon '{weaponName}'.",
            this);
#endif
    }

    void RecalculateCache(GunConfig w)
    {
        CollectModifierSnapshot(_modifierBuffer);
        int inputSignature = CalculateInputSignature(w, _modifierBuffer);
        RecalculateCacheFromSnapshot(w, inputSignature);
    }

    void RecalculateCache(GunConfig w, List<RuntimeStatModifier> modifierSnapshot, int inputSignature)
    {
        _modifierBuffer.Clear();
        _modifierBuffer.AddRange(modifierSnapshot);
        RecalculateCacheFromSnapshot(w, inputSignature);
    }

    void RecalculateCacheFromSnapshot(GunConfig w, int inputSignature)
    {
        bool signatureChanged = !_hasCachedInputSignature || inputSignature != _cachedInputSignature || w != _cachedWeapon;
        _cachedWeapon = w;

        CharacterStatTotals totals = CharacterStatFormula.Compute(BuildStatInputs(w), _modifierBuffer);

        _cachedDamage = totals.Damage;
        _cachedArmor = totals.Armor;
        _cachedMoveSpeed = totals.MoveSpeed;
        _cachedCritRatePercent = totals.CritRatePercent;
        _cachedCritMultiplier = totals.CritMultiplier;
        _cachedFireInterval = totals.FireInterval;
        _cachedReloadTime = totals.ReloadTime;
        _cachedStability = totals.Stability;
        _cachedBulletSpeed = totals.BulletSpeed;
        _cachedMaxMagazine = totals.MaxMagazine;
        _cachedMaxReserveAmmo = totals.MaxReserveAmmo;
        _cachedMaxHealth = totals.MaxHealth;
        _cachedMaxStamina = totals.MaxStamina;
        _cachedMaxEnergy = totals.MaxEnergy;
        _cachedSkillBaseDamage = totals.SkillBaseDamage;

        _cachedInputSignature = inputSignature;
        _hasCachedInputSignature = true;
        _isDirty = false;
        InvalidateSignatureProbe();

        if (signatureChanged)
            _revision++;
    }

    int ProbeInputSignature(GunConfig w)
    {
        int frame = Application.isPlaying ? Time.frameCount : -1;
        if (Application.isPlaying &&
            _lastSignatureProbeFrame == frame &&
            _lastSignatureProbeWeapon == w)
        {
            return _lastSignatureProbe;
        }

        CollectModifierSnapshot(_modifierProbeBuffer);
        int signature = CalculateInputSignature(w, _modifierProbeBuffer);

        if (Application.isPlaying)
        {
            _lastSignatureProbeFrame = frame;
            _lastSignatureProbeWeapon = w;
            _lastSignatureProbe = signature;
        }

        return signature;
    }

    void CollectModifierSnapshot(List<RuntimeStatModifier> buffer)
    {
        buffer.Clear();

        if (_modifierProviders.Count == 0)
            RebuildModifierProviders();

        for (int i = 0; i < _modifierProviders.Count; i++)
        {
            var provider = _modifierProviders[i];
            if (provider == null)
                continue;

            if (provider is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;

            provider.AppendStatModifiers(buffer);
        }
    }

    int CalculateInputSignature(GunConfig w, List<RuntimeStatModifier> modifiers)
    {
        unchecked
        {
            int hash = 17;

            hash = CombineHash(hash, ctx ? ctx.GetInstanceID() : 0);
            hash = CombineHash(hash, ctx != null && ctx.baseStats != null ? ctx.baseStats.GetInstanceID() : 0);
            hash = CombineHash(hash, GetLevel());
            hash = CombineHash(hash, GetCharacterDamageBase());
            hash = CombineHash(hash, GetCharacterArmorBase());
            hash = CombineHash(hash, GetCharacterMoveSpeedBase());
            hash = CombineHash(hash, GetCharacterCritRateBase());
            hash = CombineHash(hash, GetCharacterCritMultiplierBase());
            hash = CombineHash(hash, GetCharacterMaxHealthBase());
            hash = CombineHash(hash, GetCharacterMaxStaminaBase());
            hash = CombineHash(hash, GetCharacterMaxEnergyBase());

            hash = CombineHash(hash, w ? w.GetInstanceID() : 0);
            hash = CombineHash(hash, w ? w.damage : 0f);
            hash = CombineHash(hash, w ? w.critRate : 0f);
            hash = CombineHash(hash, w ? w.critMultiplier : BASE_CRIT_MULT);
            hash = CombineHash(hash, w ? w.fireRate : 0f);
            hash = CombineHash(hash, w ? w.reloadTime : 0f);
            hash = CombineHash(hash, w ? w.stability : 0f);
            hash = CombineHash(hash, w ? w.BulletSpeed : 0f);
            hash = CombineHash(hash, w ? w.maxMagazine : 0);
            hash = CombineHash(hash, w ? w.maxReserveAmmo : 0);
            hash = CombineHash(hash, w != null && w.infiniteReserveAmmo ? 1 : 0);

            for (int i = 0; i < _modifierProviders.Count; i++)
            {
                var provider = _modifierProviders[i];
                if (provider == null)
                {
                    hash = CombineHash(hash, 0);
                    hash = CombineHash(hash, 0);
                    continue;
                }

                hash = CombineHash(hash, provider is UnityEngine.Object obj ? obj.GetInstanceID() : 0);
                hash = CombineHash(hash, provider is Behaviour behaviour && behaviour.isActiveAndEnabled ? 1 : 0);
            }

            hash = CombineHash(hash, modifiers.Count);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                hash = CombineHash(hash, (int)modifier.StatType);
                hash = CombineHash(hash, (int)modifier.Operation);
                hash = CombineHash(hash, modifier.Value);
                hash = CombineHash(hash, modifier.ModifierKey);
            }

            return hash;
        }
    }

    static int CombineHash(int hash, int value)
    {
        unchecked { return hash * 31 + value; }
    }

    static int CombineHash(int hash, float value)
    {
        unchecked { return hash * 31 + value.GetHashCode(); }
    }

    static int CombineHash(int hash, string value)
    {
        unchecked { return hash * 31 + (value != null ? StringComparer.Ordinal.GetHashCode(value) : 0); }
    }

    CharacterStatInputs BuildStatInputs(GunConfig w)
    {
        return new CharacterStatInputs
        {
            CharacterDamage = GetCharacterDamageBase(),
            CharacterArmor = GetCharacterArmorBase(),
            CharacterMoveSpeed = GetCharacterMoveSpeedBase(),
            CharacterCritRate = GetCharacterCritRateBase(),
            CharacterCritMultiplier = GetCharacterCritMultiplierBase(),
            CharacterMaxHealth = GetCharacterMaxHealthBase(),
            CharacterMaxStamina = GetCharacterMaxStaminaBase(),
            CharacterMaxEnergy = GetCharacterMaxEnergyBase(),
            Weapon = w
        };
    }

    static float ApplyStatusModifiers(List<RuntimeStatModifier> modifiers, StatType statType, float baseValue)
    {
        return CharacterStatFormula.ApplyModifiers(modifiers, statType, baseValue);
    }

    float GetCharacterDamageBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.baseDamage + ctx.baseStats.DamageScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterArmorBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.basearmor + ctx.baseStats.ArmorScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterMoveSpeedBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.baseSpeed + ctx.baseStats.SpeedScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterCritRateBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.basecritRate + ctx.baseStats.CritrateScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterCritMultiplierBase()
    {
        if (ctx == null || ctx.baseStats == null)
            return BASE_CRIT_MULT;

        return Mathf.Max(1f, ctx.basecritMultiplier + ctx.baseStats.CritDamageScaling * GetLevelOffset());
    }

    float GetCharacterMaxHealthBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.basemaxHealth + ctx.baseStats.MAXHPScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterMaxStaminaBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.baseStamina + ctx.baseStats.StaminaScaling * GetLevelOffset() : 0f;
    }

    float GetCharacterMaxEnergyBase()
    {
        return ctx != null && ctx.baseStats != null ? ctx.baseEnagy + ctx.baseStats.EnagyScaling * GetLevelOffset() : 0f;
    }

    float GetLevel()
    {
        if (ctx is EnemyContext enemyContext && enemyContext.EnemyLevelSystem != null)
            return enemyContext.EnemyLevelSystem.Level;

        return ctx != null && ctx.levelSystem != null ? ctx.levelSystem.Level : 1f;
    }

    float GetLevelOffset()
    {
        return Mathf.Max(0f, GetLevel() - 1f);
    }

    GunConfig GetCurrentWeapon()
    {
        if (weapon && weapon.CurrentWeapon) return weapon.CurrentWeapon;
        if (ctx && ctx.currentWeapon) return ctx.currentWeapon;
        return null;
    }

    float GetDamageInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0f, ApplyStatusModifiers(_modifierBuffer, StatType.Damage, (w ? w.damage : 0f) + GetCharacterDamageBase()));
    }

    float GetCritRatePercentInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        float baseValue = (w ? w.critRate : 0f) + GetCharacterCritRateBase();
        return Mathf.Clamp(ApplyStatusModifiers(_modifierBuffer, StatType.CritRate, baseValue), 0f, 100f);
    }

    float GetCritMultiplierInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        float weaponMult = w ? Mathf.Max(1f, w.critMultiplier) : BASE_CRIT_MULT;
        float charBonus = GetCharacterCritMultiplierBase() - BASE_CRIT_MULT;
        return Mathf.Max(1f, ApplyStatusModifiers(_modifierBuffer, StatType.CritMultiplier, weaponMult + charBonus));
    }

    float GetFireIntervalInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0.01f, ApplyStatusModifiers(_modifierBuffer, StatType.FireInterval, w ? w.fireRate : 0f));
    }

    float GetReloadTimeInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0f, ApplyStatusModifiers(_modifierBuffer, StatType.ReloadTime, w ? w.reloadTime : 0f));
    }

    float GetStabilityInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Clamp(
            ApplyStatusModifiers(_modifierBuffer, StatType.Stability, w ? w.stability : 0f),
            MIN_STABILITY_PERCENT,
            MAX_STABILITY_PERCENT);
    }

    float GetBulletSpeedInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0f, ApplyStatusModifiers(_modifierBuffer, StatType.BulletSpeed, w ? w.BulletSpeed : 0f));
    }

    int GetMaxMagazineInternal(GunConfig w)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0, Mathf.RoundToInt(ApplyStatusModifiers(_modifierBuffer, StatType.MaxMagazine, w ? w.maxMagazine : 0f)));
    }

    int GetMaxReserveAmmoInternal(GunConfig w)
    {
        if (!w || w.infiniteReserveAmmo)
            return 0;

        EnsureCacheFresh(GetCurrentWeapon());
        int maxMagazine = GetMaxMagazine(w);
        int baseValue = WeaponInstanceFactory.ResolveMaxReserveAmmo(w, maxMagazine);
        return Mathf.Max(0, Mathf.RoundToInt(ApplyStatusModifiers(_modifierBuffer, StatType.MaxReserveAmmo, baseValue)));
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
    public int MaxReserveAmmo => GetMaxReserveAmmo(CurrentWeapon);
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

    public float GetReloadDuration(float baseDuration)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0f, ApplyStatusModifiers(
            _modifierBuffer,
            StatType.ReloadTime,
            Mathf.Max(0f, baseDuration)));
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

    public int GetMaxReserveAmmo(GunConfig w)
    {
        GunConfig current = GetCurrentWeapon();
        if (w == current)
        {
            EnsureCacheFresh(current);
            return _cachedMaxReserveAmmo;
        }

        return GetMaxReserveAmmoInternal(w);
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

    public float GetStaminaRegenerationRate(float baseRate)
    {
        EnsureCacheFresh(GetCurrentWeapon());
        return Mathf.Max(0f, ApplyStatusModifiers(_modifierBuffer, StatType.StaminaRegenRate, Mathf.Max(0f, baseRate)));
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
