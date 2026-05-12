using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponUpgradeRuntimeController : MonoBehaviour, IStatModifierProvider, IWeaponRuntimeEffectHandler
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;

    [Header("Fallback")]
    [SerializeField] private WeaponUpgradeCurve upgradeCurveOverride;

    readonly List<WeaponUpgradeMilestone> _activeMilestones = new();
    readonly Dictionary<string, int> _shotCounters = new();
    readonly Dictionary<string, StatusEffectDef> _reloadBuffCache = new();

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
    }

    void OnDisable()
    {
        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
    }

    void ResolveReferences()
    {
        if (!ctx)
            TryGetComponent(out ctx);

        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.WeaponSystem;

        if (!weaponSystem)
            TryGetComponent(out weaponSystem);

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (!statsHub && ctx != null)
            statsHub = ctx.StatsHub;

        if (!statsHub)
            TryGetComponent(out statsHub);

        if (!statsHub && ctx != null)
            statsHub = ctx.GetComponentInChildren<StatsHub>(true);

        if (!statusEffectController && ctx != null)
            statusEffectController = ctx.GetComponentInChildren<StatusEffectController>(true);

        if (!statusEffectController)
            TryGetComponent(out statusEffectController);
    }

    public void NotifyWeaponEquipped()
    {
        _shotCounters.Clear();
        statsHub?.MarkDirty();
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null)
            return;

        ResolveReferences();

        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        var weapon = weaponSystem != null ? weaponSystem.CurrentWeapon : null;
        var curve = ResolveUpgradeCurve(weapon);

        if (instance == null || !weapon || curve == null)
            return;

        curve.AppendStatModifiers(buffer, weapon, instance, BuildSourceId(instance.instanceId));
    }

    public void HandleShotFired()
    {
        ResolveReferences();

        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        var weapon = weaponSystem != null ? weaponSystem.CurrentWeapon : null;
        var curve = ResolveUpgradeCurve(weapon);

        if (instance == null || !weapon || curve == null || instance.upgradeLevel <= 0)
            return;

        curve.GetActiveMilestones(_activeMilestones, weapon.WeaponType, instance.upgradeLevel);
        for (int i = 0; i < _activeMilestones.Count; i++)
            HandleShotMilestone(_activeMilestones[i], i, instance);
    }

    public void HandleReloadCompleted()
    {
        ResolveReferences();

        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        var weapon = weaponSystem != null ? weaponSystem.CurrentWeapon : null;
        var curve = ResolveUpgradeCurve(weapon);

        if (instance == null || !weapon || curve == null || instance.upgradeLevel <= 0)
            return;

        bool appliedAny = false;
        curve.GetActiveMilestones(_activeMilestones, weapon.WeaponType, instance.upgradeLevel);

        for (int i = 0; i < _activeMilestones.Count; i++)
        {
            var milestone = _activeMilestones[i];
            if (milestone == null || milestone.effects == null)
                continue;

            string milestoneId = milestone.ResolveId(i);
            for (int j = 0; j < milestone.effects.Count; j++)
            {
                var effect = milestone.effects[j];
                if (effect == null || effect.effectType != WeaponUpgradeEffectType.TimedBuffOnReload)
                    continue;

                appliedAny |= ApplyReloadBuff(effect, instance.instanceId, milestoneId, j);
            }
        }

        if (appliedAny)
            statsHub?.MarkDirty();
    }

    void HandleShotMilestone(WeaponUpgradeMilestone milestone, int milestoneIndex, WeaponInstanceData instance)
    {
        if (milestone == null || milestone.effects == null)
            return;

        string milestoneId = milestone.ResolveId(milestoneIndex);

        for (int i = 0; i < milestone.effects.Count; i++)
        {
            var effect = milestone.effects[i];
            if (effect == null)
                continue;

            switch (effect.effectType)
            {
                case WeaponUpgradeEffectType.ExtraProjectileChance:
                    if (PassesProcChance(effect))
                        SpawnProjectiles(effect);
                    break;

                case WeaponUpgradeEffectType.SpecialProjectileEveryNthShot:
                    HandleNthShotEffect(effect, instance.instanceId, milestoneId, i);
                    break;
            }
        }
    }

    void HandleNthShotEffect(WeaponUpgradeEffect effect, string instanceId, string milestoneId, int effectIndex)
    {
        string counterKey = BuildEffectKey(instanceId, milestoneId, effect, effectIndex);
        _shotCounters.TryGetValue(counterKey, out int currentShots);
        currentShots++;

        int requiredShots = Mathf.Max(1, effect.requiredShots);
        if (currentShots < requiredShots)
        {
            _shotCounters[counterKey] = currentShots;
            return;
        }

        _shotCounters[counterKey] = 0;

        if (PassesProcChance(effect))
            SpawnProjectiles(effect);
    }

    void SpawnProjectiles(WeaponUpgradeEffect effect)
    {
        if (weaponSystem == null || effect == null)
            return;

        int count = Mathf.Max(1, effect.projectileCount);
        for (int i = 0; i < count; i++)
        {
            weaponSystem.SpawnAffixProjectile(
                effect.specialProjectileConfig,
                effect.specialProjectilePrefab,
                effect.specialProjectileDamageMultiplier,
                effect.specialProjectileSpeedMultiplier);
        }
    }

    bool ApplyReloadBuff(WeaponUpgradeEffect effect, string instanceId, string milestoneId, int effectIndex)
    {
        if (effect == null || statusEffectController == null)
            return false;

        if (!PassesProcChance(effect))
            return false;

        var statusEffect = GetOrCreateReloadBuffEffect(effect, instanceId, milestoneId, effectIndex);
        if (statusEffect == null)
            return false;

        statusEffectController.ApplyEffect(statusEffect, gameObject, 1);
        return true;
    }

    StatusEffectDef GetOrCreateReloadBuffEffect(
        WeaponUpgradeEffect effect,
        string instanceId,
        string milestoneId,
        int effectIndex)
    {
        string effectId = effect.ResolveId(milestoneId, effectIndex);
        string cacheKey = $"{instanceId}:{milestoneId}:{effectId}:{effect.value:0.###}:{effect.buffDurationSeconds:0.###}";
        if (_reloadBuffCache.TryGetValue(cacheKey, out var existing) && existing != null)
            return existing;

        var statusEffect = ScriptableObject.CreateInstance<StatusEffectDef>();
        statusEffect.effectId = $"{BuildSourceId(instanceId)}:{milestoneId}:{effectId}:reload-buff";
        statusEffect.category = StatusEffectCategory.Buff;
        statusEffect.duration = Mathf.Max(0f, effect.buffDurationSeconds);
        statusEffect.maxStacks = 1;
        statusEffect.stackMode = StackMode.RefreshDuration;
        statusEffect.modifiers = new List<StatusEffectModifier>
        {
            new StatusEffectModifier
            {
                statType = effect.statType,
                operation = effect.modifierOp,
                value = effect.value
            }
        };

        _reloadBuffCache[cacheKey] = statusEffect;
        return statusEffect;
    }

    WeaponUpgradeCurve ResolveUpgradeCurve(GunConfig weapon)
    {
        if (weapon != null && weapon.upgradeCurve != null)
            return weapon.upgradeCurve;

        return upgradeCurveOverride;
    }

    static bool PassesProcChance(WeaponUpgradeEffect effect)
    {
        if (effect == null)
            return false;

        return Random.value <= Mathf.Clamp01(effect.procChance);
    }

    static string BuildEffectKey(string instanceId, string milestoneId, WeaponUpgradeEffect effect, int effectIndex)
    {
        string resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId) ? "unknown" : instanceId;
        string resolvedMilestoneId = string.IsNullOrWhiteSpace(milestoneId) ? "milestone" : milestoneId;
        string effectId = effect != null ? effect.ResolveId(resolvedMilestoneId, effectIndex) : $"effect:{effectIndex}";
        return $"{resolvedInstanceId}:{resolvedMilestoneId}:{effectId}";
    }

    static string BuildSourceId(string instanceId)
    {
        return string.IsNullOrWhiteSpace(instanceId)
            ? "weapon-upgrade:unknown"
            : $"weapon-upgrade:{instanceId}";
    }
}
