using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Apply Status On Hit")]
public sealed class ApplyStatusOnHitModule : ProjectileModule
{
    [Serializable]
    public sealed class StatusApplication
    {
        public StatusEffectDef effect;
        [Min(0)] public int initialStacks = 1;
        [Range(0f, 1f)] public float chance = 1f;
    }

    [SerializeField] private List<StatusApplication> applications = new();

    public override void OnDamageApplied(Projectile p, ProjectileContext ctx, IProjectileModuleState state, in ProjectileHitInfo hit, IDamageable target)
    {
        if (target == null || applications == null || applications.Count == 0)
            return;

        var targetComponent = target as Component;
        if (targetComponent == null)
            return;

        var controller = targetComponent.GetComponentInParent<StatusEffectController>();
        if (controller == null)
            return;

        GameObject source = ctx.sourceActor != null ? ctx.sourceActor.gameObject : p.gameObject;

        for (int i = 0; i < applications.Count; i++)
        {
            var application = applications[i];
            if (application == null || application.effect == null)
                continue;

            if (application.chance < 1f && UnityEngine.Random.value > application.chance)
                continue;

            controller.ApplyEffect(
                application.effect,
                source,
                application.initialStacks,
                ctx.sourceId,
                ctx.chainId,
                ctx.depth + 1,
                ctx.origin,
                ctx.originPassiveId,
                ctx.originRuleId);
        }
    }
}
