using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Pierce")]
public class PierceModule : ProjectileModule
{
    [Tooltip("ทะลุเพิ่มอีกกี่ตัว (0 = โดนตัวแรกแล้วหาย)")]
    public int extraPierces = 0;

    class State : IProjectileModuleState
    {
        public int hitsLeft;
        public HashSet<int> hitIds = new();
    }

    public override IProjectileModuleState CreateState() => new State();

    public override void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        s.hitsLeft = 1 + Mathf.Max(0, extraPierces);
        s.hitIds.Clear();
    }

    static int GetTargetId(IDamageable target)
    {
        // ถ้า target เป็น Component จะได้ InstanceID ที่ชัวร์กว่า
        if (target is Component c) return c.GetInstanceID();
        // fallback
        return target.GetHashCode();
    }

    public override void OnHit(Projectile p, ProjectileContext ctx, IProjectileModuleState st,
        in ProjectileHitInfo hit, IDamageable target)
    {
        var s = (State)st;

        // ชนฉาก/กำแพง -> ปล่อยให้หายตาม default (หรือจะ p.RequestDespawn() ก็ได้)
        if (target == null)
        {
            // p.RequestDespawn();
            return;
        }

        int id = GetTargetId(target);

        // กัน trigger ค้าง/โดนซ้ำ: อย่าให้หายเพราะมันยังควรบินต่อ
        if (s.hitIds.Contains(id))
        {
            p.PreventDespawnThisHit();
            return;
        }

        s.hitIds.Add(id);

        //ไม่ต้องทำดาเมจที่นี่ เพราะ Projectile ทำไปแล้ว
        // target.TakeDamage(ctx.stats.damage);

        s.hitsLeft--;

        if (s.hitsLeft > 0)
        {
            // ยังเหลือทะลุ -> override default ที่จะหาย
            p.PreventDespawnThisHit();
        }
        else
        {
            // หมดแล้ว -> บังคับให้หาย (กันกรณี default ไม่หาย)
            p.RequestDespawn();
        }
    }
}