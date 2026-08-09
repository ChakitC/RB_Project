using UnityEngine;

[CreateAssetMenu(fileName = "Apply Status Pickup Effect", menuName = "Game/Pickup Effect/Apply Status")]
public class ApplyStatusPickupEffectDef : PickupEffectDef
{
    [SerializeField] private StatusApplicationSpec spec = new();

    public override bool CanApply(GameObject target, in PickupContext context)
    {
        return spec?.effect != null && FindTargetComponent<StatusEffectController>(target) != null;
    }

    public override bool Apply(GameObject target, in PickupContext context)
    {
        StatusApplicationSpec resolvedSpec = spec;
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
