using System.Collections.Generic;
using UnityEngine;

public static class WeaponAffixAreaDamage
{
    const int MaxColliders = 64;
    static readonly Collider[] ColliderBuffer = new Collider[MaxColliders];
    static readonly HashSet<int> SeenTargets = new();

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
        if (owner == null || eventBus == null || radius <= 0f || damage <= 0f)
            return 0;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            ColliderBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        SeenTargets.Clear();
        int appliedCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = ColliderBuffer[i];
            if (hit == null || MeleeController.IsCombatOnlyHitbox(hit))
                continue;

            IDamageable target = DamageableResolver.ResolveFrom(hit);
            if (target == null || !target.IsAlive || !IsHostile(owner, hit))
                continue;

            Component targetComponent = target as Component;
            int targetId = targetComponent != null ? targetComponent.GetInstanceID() : hit.GetInstanceID();
            if (!SeenTargets.Add(targetId))
                continue;

            GameObject targetObject = targetComponent != null ? targetComponent.gameObject : hit.gameObject;
            var damageContext = new DamageContext(
                damage,
                owner.gameObject,
                $"{weaponInstanceId}:affix:{affixId}",
                attackId,
                chainId == 0 ? CombatEventBus.NextChainId() : chainId,
                Mathf.Max(0, depth) + 1,
                PassiveEventOrigin.External);
            DamageResult result = target.TakeDamage(in damageContext);
            if (!result.Applied)
                continue;

            appliedCount++;
            Publish(eventBus, owner.gameObject, targetObject, result, weaponInstanceId,
                affixId, attackId, damageContext.ChainId, damageContext.Depth, PassiveEventType.Hit);
            if (result.Killed)
                Publish(eventBus, owner.gameObject, targetObject, result, weaponInstanceId,
                    affixId, attackId, damageContext.ChainId, damageContext.Depth, PassiveEventType.Kill);
        }

        return appliedCount;
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

    static bool IsHostile(CharacteContext owner, Collider candidate)
    {
        CharacteContext target = candidate.GetComponentInParent<CharacteContext>();
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
