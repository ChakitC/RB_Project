using System.Collections.Generic;
using UnityEngine;

public class WeaponAffixRuntimeController : MonoBehaviour, IStatModifierProvider
{
    [Header("Refs")]
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private WeaponAffixDatabase affixDatabase;

    readonly Dictionary<string, StatusEffectDef> _reloadBuffCache = new();

    void Awake()
    {
        if (!weaponSystem) TryGetComponent(out weaponSystem);
        if (!statsHub) TryGetComponent(out statsHub);
        if (!statusEffectController) TryGetComponent(out statusEffectController);
    }

    void OnEnable()
    {
        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
    }

    void OnDisable()
    {
        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
    }

    public void NotifyWeaponEquipped()
    {
        statsHub?.MarkDirty();
    }

    public void HandleShotFired()
    {
        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (instance == null)
            return;

        instance.shotCounter = Mathf.Max(0, instance.shotCounter) + 1;

        if (!TryResolveAffix(instance.mainAffix, out var definition))
            return;

        if (definition.behaviorType != WeaponAffixBehaviorType.SpecialProjectileEveryNthShot)
            return;

        int requiredShots = Mathf.Max(1, definition.requiredShots);
        if (instance.shotCounter < requiredShots)
            return;

        instance.shotCounter = 0;

        if (definition.procChance > 0f && Random.value > definition.procChance)
            return;

        weaponSystem?.SpawnAffixProjectile(
            definition.specialProjectileConfig,
            definition.specialProjectilePrefab,
            definition.specialProjectileDamageMultiplier,
            definition.specialProjectileSpeedMultiplier);
    }

    public void HandleReloadCompleted()
    {
        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (instance == null)
            return;

        bool appliedAny = ApplyReloadBuff(instance.mainAffix, instance.instanceId);

        if (instance.subAffixes != null)
        {
            for (int i = 0; i < instance.subAffixes.Count; i++)
                appliedAny |= ApplyReloadBuff(instance.subAffixes[i], instance.instanceId);
        }

        if (appliedAny)
            statsHub?.MarkDirty();
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null || affixDatabase == null || weaponSystem == null)
            return;

        var instance = weaponSystem.CurrentWeaponInstance;
        if (instance == null)
            return;

        string sourceId = BuildSourceId(instance.instanceId);

        AppendStatModifier(buffer, instance.mainAffix, sourceId);

        if (instance.subAffixes == null)
            return;

        for (int i = 0; i < instance.subAffixes.Count; i++)
        {
            AppendStatModifier(buffer, instance.subAffixes[i], sourceId);
        }
    }

    bool ApplyReloadBuff(RolledAffixData rolledAffix, string instanceId)
    {
        if (!TryResolveAffix(rolledAffix, out var definition))
            return false;

        if (definition.behaviorType != WeaponAffixBehaviorType.TimedBuffOnReload)
            return false;

        var effect = GetOrCreateReloadBuffEffect(definition, rolledAffix, instanceId);
        if (effect == null)
            return false;

        statusEffectController?.ApplyEffect(effect, gameObject, 1);
        return true;
    }

    void AppendStatModifier(List<RuntimeStatModifier> buffer, RolledAffixData rolledAffix, string sourceId)
    {
        if (!TryResolveAffix(rolledAffix, out var definition))
            return;

        if (definition.behaviorType != WeaponAffixBehaviorType.StatModifier)
            return;

        buffer.Add(new RuntimeStatModifier(
            definition.statType,
            definition.modifierOp,
            definition.ResolvePrimaryValue(rolledAffix),
            sourceId));
    }

    bool TryResolveAffix(RolledAffixData rolledAffix, out WeaponAffixDefinition definition)
    {
        definition = null;

        if (rolledAffix == null || string.IsNullOrWhiteSpace(rolledAffix.affixId) || affixDatabase == null)
            return false;

        definition = affixDatabase.GetById(rolledAffix.affixId);
        return definition != null;
    }

    StatusEffectDef GetOrCreateReloadBuffEffect(WeaponAffixDefinition definition, RolledAffixData rolledAffix, string instanceId)
    {
        if (!definition)
            return null;

        string cacheKey = $"{instanceId}:{definition.affixId}:{definition.ResolvePrimaryValue(rolledAffix):0.###}";
        if (_reloadBuffCache.TryGetValue(cacheKey, out var existing) && existing != null)
            return existing;

        var effect = ScriptableObject.CreateInstance<StatusEffectDef>();
        effect.effectId = $"{BuildSourceId(instanceId)}:{definition.affixId}:reload-buff";
        effect.category = StatusEffectCategory.Buff;
        effect.duration = Mathf.Max(0f, definition.buffDurationSeconds);
        effect.maxStacks = 1;
        effect.stackMode = StackMode.RefreshDuration;
        effect.modifiers = new List<StatusEffectModifier>
        {
            new StatusEffectModifier
            {
                statType = definition.statType,
                operation = definition.modifierOp,
                value = definition.ResolvePrimaryValue(rolledAffix)
            }
        };

        _reloadBuffCache[cacheKey] = effect;
        return effect;
    }

    static string BuildSourceId(string instanceId)
    {
        return string.IsNullOrWhiteSpace(instanceId)
            ? "weapon:unknown"
            : $"weapon:{instanceId}";
    }
}
