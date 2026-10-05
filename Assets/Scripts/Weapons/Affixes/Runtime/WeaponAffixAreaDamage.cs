using System.Collections.Generic;
using UnityEngine;

public static class WeaponAffixAreaDamage
{


    public static int Apply(
        CharacteContext owner,
        CombatEventBus eventBus,
        Vector3 center,
        float radius,
        float damage,
        string weaponInstanceId,
        string affixId,
        string attackId,
        ulong chainId,
        int depth)
    {
        return ApplyResolved(owner, eventBus, center, radius, damage, weaponInstanceId, affixId, attackId, chainId, depth).SuccessfulTargets.Count;
    }

    public static PassiveResult ApplyResolved(CharacteContext owner, CombatEventBus eventBus, Vector3 center,
        float radius, float damage, string weaponInstanceId, string affixId, string attackId, ulong chainId, int depth)
    {
        if (owner == null || eventBus == null || radius <= 0f || damage <= 0f)
            return new PassiveResult(PassiveOutcome.Skipped);

        // Snapshot identities/lifetimes because damage can pool actors and mutate the registry.
        var candidates = new List<(CharacteContext actor, int life)>();
        foreach (var actor in CharacterContextRegistry.ActiveContexts)
            if (actor != null) candidates.Add((actor, actor.LifeGeneration));
        float appliedDamage = 0; var successful = new List<CharacteContext>();
        foreach (var candidate in candidates)
        {
            var actor = candidate.actor;
            if (actor == null || !actor.isActiveAndEnabled || actor.LifeGeneration != candidate.life ||
                !IsHostile(owner, actor) || (actor.transform.position - center).sqrMagnitude > radius * radius) continue;
            IDamageable target = actor.HealthSystem;
            if (target == null || !target.IsAlive) continue;
            GameObject targetObject = actor.gameObject;
            var damageContext = new DamageContext(
                damage,
                owner.gameObject,
                $"{weaponInstanceId}:affix:{affixId}",
                attackId,
                chainId == 0 ? CombatEventBus.NextChainId() : chainId,
                Mathf.Max(0, depth) + 1,
                PassiveEventOrigin.Passive);
            DamageResult result = target.TakeDamage(in damageContext);
            if (!result.Applied)
                continue;

            appliedDamage += result.AppliedDamage; successful.Add(actor);
            Publish(eventBus, owner.gameObject, targetObject, result, weaponInstanceId,
                affixId, attackId, damageContext.ChainId, damageContext.Depth, PassiveEventType.Hit);
            if (result.Killed)
                Publish(eventBus, owner.gameObject, targetObject, result, weaponInstanceId,
                    affixId, attackId, damageContext.ChainId, damageContext.Depth, PassiveEventType.Kill);
        }

        return new PassiveResult(appliedDamage, successful);
    }

    static void Publish(
        CombatEventBus bus,
        GameObject source,
        GameObject target,
        in DamageResult result,
        string weaponInstanceId,
        string affixId,
        string attackId,
        ulong chainId,
        int depth,
        PassiveEventType type)
    {
        var metadata = new CombatEventMetadata(
            result.RequestedDamage,
            result.ResolvedDamage,
            result.AppliedDamage,
            result.HealthBeforeHit,
            result.MaxHealth,
            sourceKind: CombatSourceKind.WeaponAffix,
            weaponInstanceId: weaponInstanceId,
            weaponAffixId: affixId);
        var context = new PassiveEventContext(
            type,
            source,
            source,
            target,
            $"{weaponInstanceId}:affix:{affixId}",
            attackId,
            result.AppliedDamage,
            Time.timeAsDouble,
            chainId,
            depth,
            PassiveEventOrigin.External,
            null,
            null,
            metadata);
        bus.Publish(context);
    }

    static bool IsHostile(CharacteContext owner, CharacteContext target)
    {
        if (target == null || target == owner)
            return false;

        bool ownerFriendly = owner.TargetIdentity == AITargetIdentity.Player ||
                             owner.TargetIdentity == AITargetIdentity.Companion;
        bool targetFriendly = target.TargetIdentity == AITargetIdentity.Player ||
                              target.TargetIdentity == AITargetIdentity.Companion;
        if (ownerFriendly)
            return !targetFriendly;

        return owner.TargetIdentity == AITargetIdentity.Enemy &&
               target.TargetIdentity != AITargetIdentity.Enemy;
    }
}
