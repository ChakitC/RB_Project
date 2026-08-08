using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Apply Status On Hit")]
public sealed class ApplyStatusOnHitModule : ProjectileModule
{
    // ไม่มี asset ไหนอ้างอิง GUID ของไฟล์นี้เลย (เช็คแล้ว) — nest StatusApplicationSpec ตรงๆ ได้ ไม่ต้องผ่าน
    // legacy-field migration แบบไฟล์อื่นที่มี asset ข้อมูลจริงอยู่แล้ว (ดู StatusApplicationSpec.cs)
    [Serializable]
    public sealed class StatusApplication
    {
        [Range(0f, 1f)] public float chance = 1f;
        public StatusApplicationSpec spec = new();
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
            if (application == null || application.spec?.effect == null)
                continue;

            if (application.chance < 1f && UnityEngine.Random.value > application.chance)
                continue;

            controller.ApplyEffect(
                application.spec,
                source,
                ctx.damageSourceId,
                ctx.chainId,
                ctx.depth + 1,
                ctx.origin,
                ctx.originPassiveId,
                ctx.originRuleId);
        }
    }
}
