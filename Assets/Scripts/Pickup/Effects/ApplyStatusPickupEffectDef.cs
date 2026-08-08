using UnityEngine;

[CreateAssetMenu(fileName = "Apply Status Pickup Effect", menuName = "Game/Pickup Effect/Apply Status")]
public class ApplyStatusPickupEffectDef : PickupEffectDef
{
    // Legacy-field fallback (lazy resolve, NOT ISerializationCallbackReceiver — see the "Legacy Migration"
    // block at the bottom of StatusEffects/StatusApplicationSpec.cs for why) — "Attack UP Pickup Effect.asset",
    // "Health Pickup Effect.asset", and "Feno_BulletBag_SpeedUp Pickup Effect.asset" have real `effect`/`stacks`
    // data serialized flat here.
    [SerializeField, HideInInspector] private StatusEffectDef effect;
    [SerializeField, HideInInspector] private int stacks;

    [SerializeField] private StatusApplicationSpec spec = new();

    /// <summary>Call from CanApply()/Apply() only — never during deserialization.</summary>
    StatusApplicationSpec ResolvedSpec()
    {
        if (spec != null && spec.effect != null)
            return spec;

        if (effect == null)
            return spec;

        return new StatusApplicationSpec
        {
            effect = effect,
            stacks = stacks > 0 ? stacks : 1,
            modifiers = spec?.modifiers,
            durationOverride = spec?.durationOverride ?? 0f,
            tickDamageOverride = spec?.tickDamageOverride ?? 0f,
        };
    }

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        return ResolvedSpec()?.effect != null && FindTargetComponent<StatusEffectController>(target) != null;
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        StatusApplicationSpec resolvedSpec = ResolvedSpec();
        if (resolvedSpec?.effect == null)
            return false;

        var controller = FindTargetComponent<StatusEffectController>(target);
        if (controller == null)
            return false;

        GameObject source = context.SourceObject != null ? context.SourceObject : context.PickupObject;

        // Source = pickup GameObject เอง (คนละ actor ทุกครั้งที่หยิบ) — ห้ามเปิด StatusEffectDef.separatePerSource
        // กับ effect ที่ใช้ผ่าน pickup ไม่งั้นการเก็บของหลายชิ้นจะได้ instance แยกแทนที่จะ stack ตามที่ตั้งไว้
        return controller.ApplyEffect(resolvedSpec, source) != null;
    }
}
