using UnityEngine;

// 3.1 สร้างโปรเจกไทล์ระเบิด
[CreateAssetMenu(menuName="Ability/Effects/Spawn Grenade")]
public class SpawnGrenadeEffectDef : EffectDef
{
    public GameObject grenadePrefab;
    public float speed = 20f;

    public override void Apply(AbilityContext ctx, Collider[] _)
    {
        if (!grenadePrefab) return;
        var go = Object.Instantiate(grenadePrefab, ctx.CastOrigin, Quaternion.LookRotation(ctx.CastDirection));
        if (go.TryGetComponent<GrenadeProjectile>(out var g))
            g.Launch(ctx, speed); // โปรเจกไทล์จะไปหาเป้าในรัศมีเองแล้วเรียกเอฟเฟกต์ถัดไป
    }
}

// 3.2 ดาเมจโดยอิง DamageCalculator ที่คุณมี
[CreateAssetMenu(menuName="Ability/Effects/Damage (Calculator)")]
public class DamageByCalculatorEffectDef : EffectDef
{
    public override void Apply(AbilityContext ctx, Collider[] targets)
    {
        foreach (var col in targets)
        {
            if (!col) continue;
            var hitGO = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;

            // ระยะจากจุดระเบิดถึงเป้า
            float distance = Vector3.Distance(ctx.CastOrigin, hitGO.transform.position);
            float armor = 0f;
            if (hitGO.TryGetComponent<IArmorProvider>(out var ap)) armor = ap.Armor;

            float final = DamageCalculator.CalculateFinalDamage(
                ctx.Combat.weaponType,
                distance,
                ctx.Combat.baseDamage,
                ctx.Combat.critRate,
                ctx.Combat.critMultiplier,
                armor
            );

            if (hitGO.TryGetComponent<IDamageable>(out var d) && d.IsAlive)
                d.TakeDamage(final);
        }
    }
}