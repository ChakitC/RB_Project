using UnityEngine;

// Immutable values captured before the intercepted hitbox runtime is destroyed.
public sealed class CooperativeBlockDamageSnapshot
{
    readonly CharacteContext caster;
    readonly int life;
    readonly float damage, critChance, critMultiplier, stagger;
    readonly KnockbackSettings knockback;
    readonly string sourceId, attackId;
    readonly ulong chainId;
    readonly int depth;
    readonly CombatAttributionSnapshot attribution;
    readonly CombatSourceKind sourceKind;
    readonly FollowUpExecutionProvenance followUpProvenance;
    readonly System.Collections.Generic.HashSet<int> applied = new();

    public CooperativeBlockDamageSnapshot(CharacteContext caster, float damage, float critChance, float critMultiplier,
        float stagger, KnockbackSettings knockback, string sourceId, string attackId, ulong chainId, int depth,
        CombatAttributionSnapshot attribution, CombatSourceKind sourceKind, FollowUpExecutionProvenance followUpProvenance)
    {
        this.caster = caster; life = caster.LifeGeneration; this.damage = damage;
        this.critChance = critChance; this.critMultiplier = critMultiplier; this.stagger = stagger;
        this.knockback = knockback; this.sourceId = sourceId; this.attackId = attackId;
        this.chainId = chainId != 0 ? chainId : CombatEventBus.NextChainId(); this.depth = depth;
        this.attribution = attribution; this.sourceKind = sourceKind;
        this.followUpProvenance = followUpProvenance;
    }

    public DamageResult Apply(CharacteContext target)
    {
        if (caster == null || caster.LifeGeneration != life || target == null || target.HealthSystem == null ||
            !target.HealthSystem.IsAlive || !applied.Add(target.GetInstanceID())) return default;
        var health = target.HealthSystem;
        float armor = health is IHasArmor armorHolder ? armorHolder.Armor : 0f;
        var calculation = DamageCalculator.CalculateDamage(WeaponType.Melee, 0f, damage, critChance, critMultiplier, armor);
        var build = new KnockbackBuildContext(caster.transform.position, target.transform.position, caster.transform.forward);
        KnockbackFactory.TryBuild(in knockback, in build, out var kb);
        var context = new DamageContext(calculation.Damage, caster.gameObject, sourceId, attackId, chainId, depth,
            PassiveEventOrigin.External, knockback: kb, stagger: new StaggerPayload(stagger, 1f, sourceId), attribution: attribution);
        var result = health.TakeDamage(in context);
        if (!result.Applied) return result;
        caster.StatusEffects?.NotifyTrigger(EffectTriggerType.OnHit, target.gameObject);
        Publish(PassiveEventType.Hit, target, result, calculation.WasCritical);
        if (result.Killed)
        {
            caster.StatusEffects?.NotifyTrigger(EffectTriggerType.OnKill, target.gameObject);
            Publish(PassiveEventType.Kill, target, result, calculation.WasCritical);
        }
        if (VfxSpawner.Instance != null) VfxSpawner.Instance.SpawnDamageNumber(target.transform.position + Vector3.up, result.AppliedDamage, health);
        return result;
    }

    void Publish(PassiveEventType type, CharacteContext target, DamageResult result, bool critical)
    {
        var bus = caster.CombatEventBus;
        if (bus == null) return;
        var metadata = new CombatEventMetadata(result.RequestedDamage, result.ResolvedDamage, result.AppliedDamage,
            result.HealthBeforeHit, result.MaxHealth, critical, staggerApplied: result.StaggerApplied,
            enteredChainReady: result.EnteredChainReady, sourceKind: sourceKind);
        var parent = new PassiveEventContext(PassiveEventType.None, caster.gameObject, caster.gameObject, target.gameObject,
            sourceId, attackId, result.AppliedDamage, Time.timeAsDouble, chainId, depth, PassiveEventOrigin.External,
            null, null, metadata, CombatEventBus.NextFactId(), followUpProvenance);
        bus.Publish(bus.CreateChildContext(parent, type, caster.gameObject, target.gameObject, sourceId, attackId,
            result.AppliedDamage, PassiveEventOrigin.External, metadata: metadata, actor: attribution.CreditedActor));
    }
}
