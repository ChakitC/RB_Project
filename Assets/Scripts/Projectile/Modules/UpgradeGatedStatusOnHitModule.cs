using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a status on direct hits, but only when the firing summon carries a given upgrade ID.
///
/// Deliberately narrow: it runs on the direct-damage hook alone. Area bursts, chained/split
/// descendants, and damage-over-time ticks never apply it, so the debuff tracks the number of
/// aimed shots that landed rather than the blast radius.
///
/// The status is credited to the summon's <em>owner</em>, not the summon, so debuff attribution
/// and the owner's own scaling stay with the character who deployed it.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Projectile Modules/Upgrade Gated Status On Hit")]
public sealed class UpgradeGatedStatusOnHitModule : ProjectileModule
{
    [SerializeField]
    [Tooltip("Upgrade ID the firing summon must carry. Leave blank to apply unconditionally.")]
    private string requiredUpgradeId;

    [SerializeField, Range(0f, 1f)]
    private float chance = 1f;

    [SerializeField]
    private StatusApplicationSpec spec = new();

    public string RequiredUpgradeId => requiredUpgradeId;
    public StatusApplicationSpec Spec => spec;

    public void CollectValidationIssues(List<string> issues)
    {
        spec?.CollectValidationIssues(issues, $"{name} (Upgrade Gated Status On Hit)");
    }

    public override void OnDamageApplied(
        Projectile p,
        ProjectileContext ctx,
        IProjectileModuleState state,
        in ProjectileHitInfo hit,
        IDamageable target)
    {
        if (p == null || target == null || spec?.effect == null)
            return;

        // Direct hits only.
        if (p.AreaExploded)
            return;

        // Not on chained / split descendants.
        if (ctx.depth > 0)
            return;

        if (!TryResolveSummon(ctx, out SummonedEntityRuntime summon))
            return;

        if (!string.IsNullOrWhiteSpace(requiredUpgradeId) && !summon.HasUpgrade(requiredUpgradeId))
            return;

        if (target is not Component targetComponent)
            return;

        StatusEffectController controller = targetComponent.GetComponentInParent<StatusEffectController>();
        if (controller == null)
            return;

        if (chance < 1f && Random.value > chance)
            return;

        // Credit the owner (the character who deployed the summon), not the summon itself.
        GameObject source = summon.Owner != null
            ? summon.Owner.gameObject
            : (ctx.sourceActor != null ? ctx.sourceActor.gameObject : p.gameObject);

        controller.ApplyEffect(
            spec,
            source,
            ctx.damageSourceId,
            ctx.chainId,
            ctx.depth + 1,
            ctx.origin,
            ctx.originPassiveId,
            ctx.originRuleId);
    }

    static bool TryResolveSummon(ProjectileContext ctx, out SummonedEntityRuntime summon)
    {
        summon = null;
        if (ctx.sourceActor == null)
            return false;

        summon = ctx.sourceActor.GetComponentInParent<SummonedEntityRuntime>();
        return summon != null;
    }
}
